using System.Collections.Generic;
using System.Linq;
using ItemConduit.Collision;
using ItemConduit.Utils;

namespace ItemConduit.Core
{
    public static class NetworkBuilder
    {
        /// <summary>
        /// Called when a new conduit is placed. Detects connections and containers.
        /// </summary>
        public static void OnConduitPlaced(ZDO zdo, OrientedBoundingBox bounds)
        {
            var zdoid = zdo.m_uid;

            // Find connected conduits via OBB-SAT
            var connected = ConduitSpatialQuery.FindConnectedConduits(bounds, zdoid);
            SetConnectionList(zdo, connected);

            // Update neighbors' connection lists
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

            // Detect container
            DetectAndAssignContainer(zdo, bounds, zdoid);

            // Clear new flag
            zdo.Set(ZDOFields.IC_IsNew, false);

            // Register with network manager
            var mode = (ConduitMode)zdo.GetInt(ZDOFields.IC_Mode, 0);
            ConduitNetworkManager.Instance.RegisterConduit(zdoid, mode, connected, isNewPlacement: true);

            Jotunn.Logger.LogDebug($"[NetworkBuilder] OnConduitPlaced {zdoid}: {connected.Count} connections");
        }

        /// <summary>
        /// Called when an existing conduit is updated (mode change, etc).
        /// </summary>
        public static void OnConduitUpdate(ZDO zdo)
        {
            var zdoid = zdo.m_uid;
            var connected = GetConnectionList(zdo);
            var mode = (ConduitMode)zdo.GetInt(ZDOFields.IC_Mode, 0);

            ConduitNetworkManager.Instance.RegisterConduit(zdoid, mode, connected, isNewPlacement: false);

            Jotunn.Logger.LogDebug($"[NetworkBuilder] OnConduitUpdate {zdoid}: mode={mode}");
        }

        /// <summary>
        /// Detect container via OBB-SAT and assign to conduit ZDO.
        /// Also updates container's IC_ConnectedConduits list.
        /// </summary>
        public static void DetectAndAssignContainer(ZDO zdo, OrientedBoundingBox bounds, ZDOID zdoid)
        {
            var containerId = ConduitSpatialQuery.FindConnectedContainer(bounds, zdoid);
            if (containerId.HasValue)
            {
                zdo.Set(ZDOFields.IC_ContainerZDOID, containerId.Value);

                // Update container's connected conduits list
                var containerZdo = ZDOMan.instance.GetZDO(containerId.Value);
                if (containerZdo != null)
                {
                    var containerConduits = GetContainerConduitList(containerZdo);
                    if (!containerConduits.Contains(zdoid))
                    {
                        containerConduits.Add(zdoid);
                        SetContainerConduitList(containerZdo, containerConduits);
                    }
                }

                Jotunn.Logger.LogInfo($"Conduit {zdoid} linked to container {containerId.Value}");
            }
            else
            {
                zdo.Set(ZDOFields.IC_ContainerZDOID, ZDOID.None);
            }
        }

        /// <summary>
        /// Called when container placed. Finds conduits and updates bidirectional references.
        /// Only links conduits that don't already have a container.
        /// </summary>
        public static void OnContainerPlaced(ZDO containerZdo, OrientedBoundingBox bounds)
        {
            var containerZdoid = containerZdo.m_uid;

            var collidingConduits = ConduitSpatialQuery.FindConduitsConnectedToContainer(bounds, containerZdoid);
            var linkedConduits = new List<ZDOID>();

            foreach (var conduitId in collidingConduits)
            {
                var conduitZdo = ZDOMan.instance.GetZDO(conduitId);
                if (conduitZdo == null) continue;

                // Only link if conduit has no container yet
                var existingContainer = conduitZdo.GetZDOID(ZDOFields.IC_ContainerZDOID);
                if (existingContainer.IsNone())
                {
                    conduitZdo.Set(ZDOFields.IC_ContainerZDOID, containerZdoid);
                    linkedConduits.Add(conduitId);
                    Jotunn.Logger.LogInfo($"Container {containerZdoid} linked to conduit {conduitId}");
                }
            }

            // Only store actually linked conduits (not those already connected to other containers)
            SetContainerConduitList(containerZdo, linkedConduits);

            Jotunn.Logger.LogDebug($"[NetworkBuilder] OnContainerPlaced {containerZdoid}: {collidingConduits.Count} colliding, {linkedConduits.Count} linked");
        }

        /// <summary>
        /// Called when container removed. Clears IC_ContainerZDOID from linked conduits,
        /// then re-detects containers for orphaned conduits (may link to other nearby containers).
        /// </summary>
        public static void OnContainerRemoved(CachedContainerData cached)
        {
            var containerZdoid = cached.Zdoid;

            // First pass: clear container refs and collect orphaned conduit IDs
            var orphanedConduitIds = new List<ZDOID>();
            foreach (var conduitId in cached.ConnectedConduits)
            {
                var conduitZdo = ZDOMan.instance.GetZDO(conduitId);
                if (conduitZdo == null) continue;

                var linkedContainer = conduitZdo.GetZDOID(ZDOFields.IC_ContainerZDOID);
                if (linkedContainer == containerZdoid)
                {
                    conduitZdo.Set(ZDOFields.IC_ContainerZDOID, ZDOID.None);
                    orphanedConduitIds.Add(conduitId);
                    Jotunn.Logger.LogDebug($"[NetworkBuilder] Cleared container ref from conduit {conduitId}");
                }
            }

            Jotunn.Logger.LogDebug($"[NetworkBuilder] OnContainerRemoved {containerZdoid}: cleared {orphanedConduitIds.Count} conduit references");

            // Second pass: re-detect containers for orphaned conduits
            foreach (var conduitId in orphanedConduitIds)
            {
                var conduitZdo = ZDOMan.instance.GetZDO(conduitId);
                if (conduitZdo == null) continue;

                var boundStr = conduitZdo.GetString(ZDOFields.IC_Bound, "");
                if (string.IsNullOrEmpty(boundStr)) continue;

                var bounds = OrientedBoundingBox.Deserialize(boundStr);
                DetectAndAssignContainer(conduitZdo, bounds, conduitId);
            }
        }

        /// <summary>
        /// Called when a conduit is removed. Uses cached data since ZDO is already destroyed.
        /// </summary>
        public static void OnConduitRemoved(CachedConduitData cached)
        {
            if (!ZNet.instance.IsServer()) return;

            var zdoid = cached.Zdoid;

            ConduitNetworkManager.Instance.UnregisterConduit(cached);

            // Use cached connections - ZDO no longer exists
            var connections = cached.Connections;

            Jotunn.Logger.LogDebug($"[NetworkBuilder] OnConduitRemoved {zdoid}: updating {connections.Count} neighbors");

            foreach (var connId in connections)
            {
                var connZdo = ZDOMan.instance.GetZDO(connId);
                if (connZdo == null) continue;

                var existingConns = GetConnectionList(connZdo)
                    .Where(z => z != zdoid)
                    .ToList();

                SetConnectionList(connZdo, existingConns);
                Jotunn.Logger.LogDebug($"[NetworkBuilder] Updated neighbor {connId}: now {existingConns.Count} connections");
            }

            // Remove from container's IC_ConnectedConduits if linked
            var containerZdoid = cached.ContainerZdoid;
            if (!containerZdoid.IsNone())
            {
                var containerZdo = ZDOMan.instance.GetZDO(containerZdoid);
                if (containerZdo != null)
                {
                    var containerConduits = GetContainerConduitList(containerZdo)
                        .Where(z => z != zdoid)
                        .ToList();
                    SetContainerConduitList(containerZdo, containerConduits);
                    Jotunn.Logger.LogDebug($"[NetworkBuilder] Removed conduit {zdoid} from container {containerZdoid} connections");
                }
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

        #region Container Conduit List Helpers

        public static List<ZDOID> GetContainerConduitList(ZDO zdo)
        {
            var bytes = zdo.GetByteArray(ZDOFields.IC_ConnectedConduits, null);
            if (bytes == null || bytes.Length == 0) return new List<ZDOID>();

            var pkg = new ZPackage(bytes);
            var count = pkg.ReadInt();
            var list = new List<ZDOID>(count);
            for (int i = 0; i < count; i++)
                list.Add(pkg.ReadZDOID());
            return list;
        }

        public static void SetContainerConduitList(ZDO zdo, List<ZDOID> conduits)
        {
            var pkg = new ZPackage();
            pkg.Write(conduits.Count);
            foreach (var z in conduits)
                pkg.Write(z);
            zdo.Set(ZDOFields.IC_ConnectedConduits, pkg.GetArray());
            Jotunn.Logger.LogDebug($"[NetworkBuilder] SetContainerConduitList {zdo.m_uid}: wrote {conduits.Count} conduits, bytes={pkg.GetArray().Length}");
        }

        #endregion
    }
}
