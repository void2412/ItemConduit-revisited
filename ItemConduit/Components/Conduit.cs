using System.Collections.Generic;
using System.Text;
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
            get => m_nview.GetZDO().GetZDOID(ZDOFields.IC_ContainerZDOID_Key);
            set => m_nview.GetZDO().Set(ZDOFields.IC_ContainerZDOID_Key, value);
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
            if (m_nview == null)
            {
                Jotunn.Logger.LogError("Conduit missing ZNetView");
                return;
            }
        }

        private void Start()
        {
            if (!m_nview.IsValid()) return;

            // Server-side only: Register with network manager
            // NetworkID is left empty on client - server assigns it during registration
            if (ZNet.instance.IsServer())
            {
                // Empty NetworkID indicates new placement - server will assign
                bool isNewPlacement = string.IsNullOrEmpty(NetworkID);

                ConduitNetworkManager.Instance.RegisterConduit(
                    m_nview.GetZDO().m_uid,
                    Mode,
                    Connections,
                    isNewPlacement
                );
            }
        }

        private void OnDestroy()
        {
            if (m_nview == null || !m_nview.IsValid()) return;

            if (ZNet.instance?.IsServer() == true)
            {
                ConduitNetworkManager.Instance.UnregisterConduit(
                    m_nview.GetZDO().m_uid
                );
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
    }
}
