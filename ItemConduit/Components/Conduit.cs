using System.Collections.Generic;
using System.Text;
using ItemConduit.Collision;
using ItemConduit.Core;
using ItemConduit.GUI;
using ItemConduit.Utils;
using UnityEngine;
using static ItemConduit.Plugin;

namespace ItemConduit.Components
{
    public class Conduit : MonoBehaviour, Hoverable
    {
        private ZNetView m_nview;

        #region ZDO Properties

        public ConduitMode Mode
        {
            get => (ConduitMode)m_nview.GetZDO().GetInt(ZDOFields.IC_Mode, 0);
            set
            {
                m_nview.GetZDO().Set(ZDOFields.IC_Mode, (int)value);
                // Queue for local processing on host/singleplayer
                if (ZNet.instance?.IsServer() == true)
                {
                    ConduitProcessor.QueueConduit(m_nview.GetZDO().m_uid);
                }
            }
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
            if (!m_nview.IsValid())
            {
                Jotunn.Logger.LogDebug("Conduit missing ZNetView. Possible Ghost Conduit");
                return;
            }

            Jotunn.Logger.LogDebug($"Conduit awaked: {m_nview.GetZDO().m_uid.ToString()}");
        }

        private void Start()
        {
            if (!m_nview.IsValid()) return;

            // Register for GUI visualization
            ICGUIManager.Instance?.RegisterConduit(this);

            var zdo = m_nview.GetZDO();

            //Set Init Flag for server
            zdo.Set(ZDOFields.IC_IsNew, true);

			// Initialize default mode if not set (required for collision detection)
			if (zdo.GetInt(ZDOFields.IC_Mode, -1) < 0)
			{
				zdo.Set(ZDOFields.IC_Mode, (int)ConduitMode.Conduit);
			}

			//Set OBB bound
			var bounds = GetConduitBounds();
            if (bounds.HasValue)
            {
                zdo.Set(ZDOFields.IC_Bound, bounds.Value.Serialize());
            }

            // Queue for processing on host/singleplayer (new placement or world load)
            if (ZNet.instance?.IsServer() == true)
            {
                ConduitProcessor.QueueConduit(zdo.m_uid);
            }
        }


        private OrientedBoundingBox? GetConduitBounds()
        {
            var collider = this.GetComponentInChildren<BoxCollider>();
            if (collider == null)
            {
                Jotunn.Logger.LogWarning($"[Conduit] No BoxCollider found on {gameObject.name}");
                return null;
            }

            // Use collider's transform (may be on child object)
            // Store raw OBB - tolerance is applied server-side during collision detection
            var colliderTransform = collider.transform;
            var scaledSize = Vector3.Scale(collider.size, colliderTransform.lossyScale);
            var halfExtents = scaledSize / 2f;
            var center = colliderTransform.TransformPoint(collider.center);

            return new OrientedBoundingBox(center, colliderTransform.rotation, halfExtents);
        }

        private void OnDestroy()
        {
            // Unregister from GUI
            ICGUIManager.Instance?.UnregisterConduit(this);

            if (m_nview == null || !m_nview.IsValid()) return;

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
            if (m_nview == null || !m_nview.IsValid()) return "";

            var sb = new StringBuilder();


            var modeColor = Mode switch
            {
                ConduitMode.Extract => "orange",
                ConduitMode.Insert => "green",
				ConduitMode.Conduit => "yellow",
                _ => "white"
            };
            sb.AppendLine($"<color={modeColor}>{Mode}</color>");
            sb.AppendLine($"<color=#00FFFF>ZDOID: {m_nview.GetZDO().m_uid}</color>");

            if (Mode != ConduitMode.Conduit)
            {
                var hasContainer = !ContainerZDOID.IsNone();
                sb.AppendLine($"Container: {(hasContainer ? "<color=green>Connected</color>" : "<color=red>None</color>")}");
            }

            var netId = NetworkID;
            if (!string.IsNullOrEmpty(netId))
            {
                sb.AppendLine($"Network: {netId.Substring(0,8)}");
            }

            var connections = Connections;
            sb.AppendLine($"Connections: {connections.Count}");
            if (Plugin.Instance.ConduitConfig.ShowDebug.Value && connections.Count > 0)
            {
                foreach (var conn in connections)
                    sb.AppendLine($"  - {conn}");
            }

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

    }
}
