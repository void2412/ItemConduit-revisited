using System.Collections.Generic;
using System.Text;
using ItemConduit.Collision;
using ItemConduit.Core;
using ItemConduit.Utils;
using UnityEngine;

namespace ItemConduit.Components
{
    public class Conduit : MonoBehaviour, Hoverable
    {
        private ZNetView m_nview;

        #region ZDO Properties

        public ConduitMode Mode
        {
            get => (ConduitMode)m_nview.GetZDO().GetInt(ZDOFields.IC_Mode, 0);
            set => m_nview.GetZDO().Set(ZDOFields.IC_Mode, (int)value);
        }

        public ZDOID ContainerZDOID
        {
            get => m_nview.GetZDO().GetZDOID(ZDOFields.IC_ContainerZDOID);
            set => m_nview.GetZDO().Set(ZDOFields.IC_ContainerZDOID, value);
        }

        public List<ZDOID> Connections
        {
            get
            {
                var bytes = m_nview.GetZDO().GetByteArray(ZDOFields.IC_ConnectionList, null);
                if (bytes == null || bytes.Length == 0) return new List<ZDOID>();

                var pkg = new ZPackage(bytes);
                var count = pkg.ReadInt();
                var list = new List<ZDOID>(count);
                for (int i = 0; i < count; i++)
                    list.Add(pkg.ReadZDOID());
                return list;
            }
            set
            {
                var pkg = new ZPackage();
                pkg.Write(value.Count);
                foreach (var z in value)
                    pkg.Write(z);
                m_nview.GetZDO().Set(ZDOFields.IC_ConnectionList, pkg.GetArray());
            }
        }

        public string NetworkID
        {
            get => m_nview.GetZDO().GetString(ZDOFields.IC_NetworkID, "");
            set => m_nview.GetZDO().Set(ZDOFields.IC_NetworkID, value);
        }

        public int Channel
        {
            get => m_nview.GetZDO().GetInt(ZDOFields.IC_Channel, 0);
            set => m_nview.GetZDO().Set(ZDOFields.IC_Channel, value);
        }

        public int Priority
        {
            get => m_nview.GetZDO().GetInt(ZDOFields.IC_Priority, 0);
            set => m_nview.GetZDO().Set(ZDOFields.IC_Priority, value);
        }

        public string FilterList
        {
            get => m_nview.GetZDO().GetString(ZDOFields.IC_FilterList, "");
            set => m_nview.GetZDO().Set(ZDOFields.IC_FilterList, value);
        }

        public Utils.FilterMode FilterMode
        {
            get => (Utils.FilterMode)m_nview.GetZDO().GetInt(ZDOFields.IC_FilterMode, 0);
            set => m_nview.GetZDO().Set(ZDOFields.IC_FilterMode, (int)value);
        }

        public int TransferRate
        {
            get => m_nview.GetZDO().GetInt(ZDOFields.IC_TransferRate, 1);
            set => m_nview.GetZDO().Set(ZDOFields.IC_TransferRate, value);
        }

        #endregion

        private void Awake()
        {
            m_nview = GetComponent<ZNetView>();
            if (m_nview == null || !m_nview.IsValid())
            {
                Jotunn.Logger.LogDebug("Conduit missing ZNetView. Possible Ghost Conduit");
                return;
            }

            Jotunn.Logger.LogDebug($"Conduit awaked: {m_nview.GetZDO().m_uid.ToString()}");
        }

        private void Start()
        {
            if (!m_nview.IsValid()) return;

            // Server-side only: Initialize bounds and register with network
            if (ZNet.instance.IsServer())
            {
                var zdo = m_nview.GetZDO();
                bool isNewPlacement = string.IsNullOrEmpty(NetworkID);

                // Calculate and store OBB bounds for new placements
                if (isNewPlacement)
                {
                    var bounds = GetConduitBounds();
                    if (bounds.HasValue)
                    {
                        zdo.Set(ZDOFields.IC_Bound, bounds.Value.Serialize());
                        NetworkBuilder.OnConduitPlaced(zdo);
                    }
                }

                ConduitNetworkManager.Instance.RegisterConduit(
                    zdo.m_uid,
                    Mode,
                    Connections,
                    isNewPlacement
                );
            }
        }

        private OrientedBoundingBox? GetConduitBounds()
        {
            var collider = GetComponent<BoxCollider>();
            if (collider == null)
            {
                Jotunn.Logger.LogWarning($"[Conduit] No BoxCollider found on {gameObject.name}");
                return null;
            }

            var scaledSize = Vector3.Scale(collider.size, transform.localScale);
            var halfExtents = scaledSize / 2f;
            var center = transform.position + transform.rotation * Vector3.Scale(collider.center, transform.localScale);

            // DEBUG: Remove after testing
            Jotunn.Logger.LogDebug($"[OBB Debug] collider.size: {collider.size}");
            Jotunn.Logger.LogDebug($"[OBB Debug] collider.center: {collider.center}");
            Jotunn.Logger.LogDebug($"[OBB Debug] transform.localScale: {transform.localScale}");
            Jotunn.Logger.LogDebug($"[OBB Debug] transform.position: {transform.position}");
            Jotunn.Logger.LogDebug($"[OBB Debug] collider.bounds.center (Unity): {collider.bounds.center}");
            Jotunn.Logger.LogDebug($"[OBB Debug] calculated center: {center}");
            Jotunn.Logger.LogDebug($"[OBB Debug] scaledSize: {scaledSize}, halfExtents: {halfExtents}");

            return new OrientedBoundingBox(center, transform.rotation, halfExtents);
        }

        private void OnDestroy()
        {
            if (m_nview == null || !m_nview.IsValid()) return;

            if (ZNet.instance?.IsServer() == true)
            {
                // Full cleanup: unregister + clean neighbor connection lists
                NetworkBuilder.OnConduitRemoved(m_nview.GetZDO().m_uid);
            }
        }

        /// <summary>
        /// Called from GUI/keybind interaction. Only sets mode in ZDO.
        /// Server detects mode change and handles container detection via OBB-SAT.
        /// </summary>
        public void SetMode(ConduitMode newMode)
        {
            Mode = newMode;

            // Clear container if switching to conduit mode
            if (newMode == ConduitMode.Conduit)
            {
                ContainerZDOID = ZDOID.None;
            }
            // Note: Container detection is server-side via OBB-SAT collision
            // Server will detect and assign ContainerZDOID when processing this conduit
        }

        #region Hoverable

        public string GetHoverText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("<color=yellow><b>Conduit</b></color>");
            sb.AppendLine();

            var modeColor = Mode switch
            {
                ConduitMode.Extract => "orange",
                ConduitMode.Insert => "green",
                _ => "white"
            };
            sb.AppendLine($"Mode: <color={modeColor}>{Mode}</color>");

            if (Mode != ConduitMode.Conduit)
            {
                var hasContainer = !ContainerZDOID.IsNone();
                sb.AppendLine($"Container: {(hasContainer ? "<color=green>Connected</color>" : "<color=red>None</color>")}");
            }

            var netId = NetworkID;
            if (!string.IsNullOrEmpty(netId))
            {
                sb.AppendLine($"Network: {netId.Substring(0, 8)}...");
            }
            sb.AppendLine($"Connections: {Connections.Count}");

            if (Mode != ConduitMode.Conduit)
            {
                sb.AppendLine();
                sb.AppendLine($"Channel: {Channel} | Priority: {Priority}");
                sb.AppendLine($"Filter: {FilterMode} ({GetFilterCount()} items)");
                sb.AppendLine($"Rate: {TransferRate}/tick");
            }

            sb.AppendLine();
            sb.AppendLine("[<color=yellow>E</color>] Configure");

            return sb.ToString();
        }

        public string GetHoverName() => "Conduit";

        private int GetFilterCount()
        {
            var filter = FilterList;
            if (string.IsNullOrEmpty(filter)) return 0;
            return filter.Split(',').Length;
        }

        #endregion

        #region DEBUG: Remove after testing

        private void OnDrawGizmos()
        {
            var collider = GetComponent<BoxCollider>();
            if (collider == null) return;

            var scaledSize = Vector3.Scale(collider.size, transform.localScale);
            var center = transform.position + transform.rotation * Vector3.Scale(collider.center, transform.localScale);

            // Draw OBB wireframe (green)
            Gizmos.color = Color.green;
            Gizmos.matrix = Matrix4x4.TRS(center, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, scaledSize);

            // Draw center point (red sphere)
            Gizmos.color = Color.red;
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.DrawSphere(center, 0.05f);

            // Draw Unity's bounds.center for comparison (blue sphere)
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(collider.bounds.center, 0.03f);
        }

        #endregion
    }
}
