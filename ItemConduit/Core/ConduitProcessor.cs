using System.Collections.Generic;
using ItemConduit.Collision;
using ItemConduit.Utils;

namespace ItemConduit.Core
{
    /// <summary>
    /// Cached ZDO data for removal processing (ZDO destroyed before we process).
    /// </summary>
    public class CachedConduitData
    {
        public ZDOID Zdoid { get; set; }
        public List<ZDOID> Connections { get; set; }
        public ConduitMode Mode { get; set; }
        public string NetworkID { get; set; }
        public ZDOID ContainerZdoid { get; set; }
    }

    /// <summary>
    /// Cached container data for removal processing.
    /// </summary>
    public class CachedContainerData
    {
        public ZDOID Zdoid { get; }
        public List<ZDOID> ConnectedConduits { get; }

        public CachedContainerData(ZDOID zdoid, List<ZDOID> connectedConduits)
        {
            Zdoid = zdoid;
            ConnectedConduits = connectedConduits;
        }
    }

    /// <summary>
    /// Unified conduit processing queue. Handles both remote (RPC_ZDOData)
    /// and local (host/singleplayer) conduit changes.
    /// </summary>
    public static class ConduitProcessor
    {
        private static readonly HashSet<ZDOID> _pendingConduits = new();
        private static readonly Dictionary<ZDOID, CachedConduitData> _pendingRemovals = new();
        private static readonly HashSet<ZDOID> _pendingContainers = new();
        private static readonly Dictionary<ZDOID, CachedContainerData> _pendingContainerRemovals = new();

        /// <summary>
        /// Queue a conduit for server-side processing.
        /// Call from RPC_ZDOData (remote) or Conduit setters (local).
        /// </summary>
        public static void QueueConduit(ZDOID zdoid)
        {
            if (zdoid.IsNone()) return;
            _pendingConduits.Add(zdoid);
        }

        /// <summary>
        /// Queue a conduit for removal. Call from HandleDestroyedZDO prefix.
        /// Caches ZDO data since ZDO will be destroyed before processing.
        /// </summary>
        public static void QueueConduitRemoval(ZDO zdo)
        {
            if (zdo == null) return;
            var zdoid = zdo.m_uid;
            if (zdoid.IsNone()) return;

            // Cache ZDO data before it's destroyed
            var cached = new CachedConduitData
            {
                Zdoid = zdoid,
                Connections = NetworkBuilder.GetConnectionList(zdo),
                Mode = (ConduitMode)zdo.GetInt(ZDOFields.IC_Mode, 0),
                NetworkID = zdo.GetString(ZDOFields.IC_NetworkID, ""),
                ContainerZdoid = zdo.GetZDOID(ZDOFields.IC_ContainerZDOID)
            };

            _pendingRemovals[zdoid] = cached;
            Jotunn.Logger.LogDebug($"[ConduitProcessor] Cached {zdoid} with {cached.Connections.Count} connections for removal");
        }

        /// <summary>
        /// Queue a container for server-side conduit detection.
        /// Call from ZDOManPatches when container ZDO received.
        /// </summary>
        public static void QueueContainer(ZDOID zdoid)
        {
            if (zdoid.IsNone()) return;
            _pendingContainers.Add(zdoid);
        }

        /// <summary>
        /// Queue container removal with connected conduits list.
        /// </summary>
        public static void QueueContainerRemoval(ZDOID containerZdoid, List<ZDOID> connectedConduits)
        {
            if (containerZdoid.IsNone()) return;
            _pendingContainerRemovals[containerZdoid] = new CachedContainerData(containerZdoid, connectedConduits);
        }

        /// <summary>
        /// Process all queued conduits. Call from Plugin.Update() on server.
        /// </summary>
        public static void ProcessQueue()
        {
            if (!ZNet.instance?.IsServer() ?? true) return;

            // Process removals first (using cached data)
            if (_pendingRemovals.Count > 0)
            {
                foreach (var cached in _pendingRemovals.Values)
                {
                    NetworkBuilder.OnConduitRemoved(cached);
                    Jotunn.Logger.LogDebug($"[ConduitProcessor] Removed conduit {cached.Zdoid}");
                }
                _pendingRemovals.Clear();
            }

            // Process additions/updates
            if (_pendingConduits.Count > 0)
            {
                foreach (var zdoid in _pendingConduits)
                {
                    ProcessConduit(zdoid);
                }
                _pendingConduits.Clear();
            }

            // Process containers (find and update connected conduits)
            if (_pendingContainers.Count > 0)
            {
                foreach (var zdoid in _pendingContainers)
                {
                    ProcessContainer(zdoid);
                }
                _pendingContainers.Clear();
            }

            // Process container removals (clear conduit references)
            if (_pendingContainerRemovals.Count > 0)
            {
                foreach (var cached in _pendingContainerRemovals.Values)
                {
                    NetworkBuilder.OnContainerRemoved(cached);
                }
                _pendingContainerRemovals.Clear();
            }
        }

        private static void ProcessConduit(ZDOID zdoid)
        {
            var zdo = ZDOMan.instance?.GetZDO(zdoid);
            if (zdo == null) return;

            bool isNew = zdo.GetBool(ZDOFields.IC_IsNew);

            if (isNew)
            {
                // Get OBB bounds for new placement
                var boundStr = zdo.GetString(ZDOFields.IC_Bound, "");
                if (string.IsNullOrEmpty(boundStr))
                {
                    Jotunn.Logger.LogWarning($"[ConduitProcessor] No bounds for {zdoid}");
                    return;
                }
                var bounds = OrientedBoundingBox.Deserialize(boundStr);
                NetworkBuilder.OnConduitPlaced(zdo, bounds);
            }
            else
            {
                NetworkBuilder.OnConduitUpdate(zdo);
            }
        }

        private static void ProcessContainer(ZDOID zdoid)
        {
            var zdo = ZDOMan.instance?.GetZDO(zdoid);
            if (zdo == null) return;

            // Only process new containers (IC_IsNew flag set by client)
            bool isNew = zdo.GetBool(ZDOFields.IC_IsNew);
            if (!isNew) return;

            var boundStr = zdo.GetString(ZDOFields.IC_Bound, "");
            if (string.IsNullOrEmpty(boundStr))
            {
                Jotunn.Logger.LogDebug($"[ConduitProcessor] Container {zdoid} has no bounds yet");
                return;
            }

            // Clear flag before processing
            zdo.Set(ZDOFields.IC_IsNew, false);

            var bounds = OrientedBoundingBox.Deserialize(boundStr);
            NetworkBuilder.OnContainerPlaced(zdo, bounds);
        }

        public static void Clear()
        {
            _pendingConduits.Clear();
            _pendingRemovals.Clear();
            _pendingContainers.Clear();
            _pendingContainerRemovals.Clear();
        }
    }
}
