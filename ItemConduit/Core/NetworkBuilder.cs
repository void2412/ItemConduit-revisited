using System.Collections.Generic;
using System.Linq;
using ItemConduit.Collision;
using ItemConduit.Utils;

namespace ItemConduit.Core
{
    public static class NetworkBuilder
    {
        /// <summary>
        /// Called when a conduit is placed. Detects connections and containers.
        /// </summary>
        public static void OnConduitPlaced(ZDO zdo)
        {
            if (!ZNet.instance.IsServer()) return;

            var zdoid = zdo.m_uid;
            var position = zdo.GetPosition();
            var rotation = zdo.GetRotation();

            // Get or create OBB bounds
            var boundStr = zdo.GetString(ZDOFields.IC_Bound, "");
            OrientedBoundingBox bounds;

            if (string.IsNullOrEmpty(boundStr))
            {
                // Create default bounds for beam (4m x 0.2m x 0.2m)
                bounds = new OrientedBoundingBox(
                    position,
                    rotation,
                    new UnityEngine.Vector3(2f, 0.1f, 0.1f)
                );
                zdo.Set(ZDOFields.IC_Bound, bounds.Serialize());
            }
            else
            {
                bounds = OrientedBoundingBox.Deserialize(boundStr);
            }

            // Find connected conduits via OBB-SAT
            var connected = ConduitSpatialQuery.FindConnectedConduits(bounds, zdoid);

            // Update connection list (ZPackage serialization)
            SetConnectionList(zdo, connected);

            // Always detect container on placement (regardless of mode)
            DetectAndAssignContainer(zdo, bounds, zdoid);

            // Register with network manager
            var mode = (ConduitMode)zdo.GetInt(ZDOFields.IC_Mode, 0);
            ConduitNetworkManager.Instance.RegisterConduit(zdoid, mode, connected, isNewPlacement: true);

            // Update connected conduits' connection lists
            foreach (var connectedId in connected)
            {
                var connZdo = ZDOMan.instance.GetZDO(connectedId);
                if (connZdo == null) continue;

                var existingConns = GetConnectionList(connZdo);
                if (!existingConns.Contains(zdoid))
                {
                    existingConns.Add(zdoid);
                    SetConnectionList(connZdo, existingConns);
                }
            }
        }

        /// <summary>
        /// Detect container via OBB-SAT and assign to conduit ZDO.
        /// </summary>
        public static void DetectAndAssignContainer(ZDO zdo, OrientedBoundingBox bounds, ZDOID zdoid)
        {
            var container = ConduitSpatialQuery.FindConnectedContainer(bounds, zdoid);
            if (container.HasValue)
            {
                zdo.Set(ZDOFields.IC_ContainerZDOID_Key, container.Value);
                Jotunn.Logger.LogInfo($"Conduit {zdoid} linked to container {container.Value}");
            }
            else
            {
                zdo.Set(ZDOFields.IC_ContainerZDOID_Key, ZDOID.None);
            }
        }

        /// <summary>
        /// Called when a conduit is removed.
        /// </summary>
        public static void OnConduitRemoved(ZDOID zdoid)
        {
            if (!ZNet.instance.IsServer()) return;

            ConduitNetworkManager.Instance.UnregisterConduit(zdoid);

            var zdo = ZDOMan.instance.GetZDO(zdoid);
            if (zdo == null) return;

            var connections = GetConnectionList(zdo);

            foreach (var connId in connections)
            {
                var connZdo = ZDOMan.instance.GetZDO(connId);
                if (connZdo == null) continue;

                var existingConns = GetConnectionList(connZdo)
                    .Where(z => z != zdoid)
                    .ToList();

                SetConnectionList(connZdo, existingConns);
            }
        }

        /// <summary>
        /// Rebuild all networks from ZDO data.
        /// </summary>
        public static void RebuildAllNetworks()
        {
            if (!ZNet.instance.IsServer()) return;

            ConduitNetworkManager.Instance.Clear();

            // Use ZDOMan's internal dictionary
            var conduitZdos = ZDOMan.instance.m_objectsByID.Values
                .Where(z => z != null && z.GetInt(ZDOFields.IC_Mode, -1) >= 0)
                .ToList();

            Jotunn.Logger.LogInfo($"Rebuilding networks from {conduitZdos.Count} conduits");

            foreach (var zdo in conduitZdos)
            {
                var mode = (ConduitMode)zdo.GetInt(ZDOFields.IC_Mode, 0);
                var connections = GetConnectionList(zdo);

                ConduitNetworkManager.Instance.RegisterConduit(zdo.m_uid, mode, connections);
            }

            var networks = ConduitNetworkManager.Instance.GetValidNetworks().Count();
            Jotunn.Logger.LogInfo($"Rebuilt {networks} valid networks");
        }

        #region ZPackage Connection List Helpers

        public static List<ZDOID> GetConnectionList(ZDO zdo)
        {
            var bytes = zdo.GetByteArray(ZDOFields.IC_ConnectionList, null);
            if (bytes == null || bytes.Length == 0) return new List<ZDOID>();

            var pkg = new ZPackage(bytes);
            var count = pkg.ReadInt();
            var list = new List<ZDOID>(count);
            for (int i = 0; i < count; i++)
                list.Add(pkg.ReadZDOID());
            return list;
        }

        public static void SetConnectionList(ZDO zdo, List<ZDOID> connections)
        {
            var pkg = new ZPackage();
            pkg.Write(connections.Count);
            foreach (var z in connections)
                pkg.Write(z);
            zdo.Set(ZDOFields.IC_ConnectionList, pkg.GetArray());
        }

        #endregion
    }
}
