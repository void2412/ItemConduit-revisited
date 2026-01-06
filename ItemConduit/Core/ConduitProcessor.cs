using System.Collections.Generic;
using ItemConduit.Collision;
using ItemConduit.Utils;

namespace ItemConduit.Core
{
    /// <summary>
    /// Unified conduit processing queue. Handles both remote (RPC_ZDOData)
    /// and local (host/singleplayer) conduit changes.
    /// </summary>
    public static class ConduitProcessor
    {
        private static readonly HashSet<ZDOID> _pendingConduits = new();
        private static readonly HashSet<ZDOID> _pendingConduitsRemoval = new();

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
        /// </summary>
        public static void QueueConduitRemoval(ZDOID zdoid)
        {
            if (zdoid.IsNone()) return;
            _pendingConduitsRemoval.Add(zdoid);
        }

        /// <summary>
        /// Process all queued conduits. Call from Plugin.Update() on server.
        /// </summary>
        public static void ProcessQueue()
        {
            if (!ZNet.instance?.IsServer() ?? true) return;

            // Process removals first
            if (_pendingConduitsRemoval.Count > 0)
            {
                foreach (var zdoid in _pendingConduitsRemoval)
                {
                    NetworkBuilder.OnConduitRemoved(zdoid);
                    Jotunn.Logger.LogDebug($"[ConduitProcessor] Removed conduit {zdoid}");
                }
                _pendingConduitsRemoval.Clear();
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
        }

        private static void ProcessConduit(ZDOID zdoid)
        {
            var zdo = ZDOMan.instance?.GetZDO(zdoid);
            if (zdo == null) return;

            // Get OBB bounds
            var boundStr = zdo.GetString(ZDOFields.IC_Bound, "");
            if (string.IsNullOrEmpty(boundStr))
            {
                Jotunn.Logger.LogWarning($"[ConduitProcessor] No bounds for {zdoid}");
                return;
            }

            var bounds = OrientedBoundingBox.Deserialize(boundStr);

            // Detect connections
            List<ZDOID> connected = new List<ZDOID>();
            bool isNew = zdo.GetBool(ZDOFields.IC_IsNew);
            Jotunn.Logger.LogDebug($"Is New Placement: {isNew}");
            if (isNew)
            {
                connected = ConduitSpatialQuery.FindConnectedConduits(bounds, zdoid);
                NetworkBuilder.SetConnectionList(zdo, connected);

                // Update neighbors' connection lists
                foreach (var connectedId in connected)
                {
                    var connZdo = ZDOMan.instance.GetZDO(connectedId);
                    if (connZdo == null) continue;

                    var existingConns = NetworkBuilder.GetConnectionList(connZdo);
                    if (!existingConns.Contains(zdoid))
                    {
                        existingConns.Add(zdoid);
                        NetworkBuilder.SetConnectionList(connZdo, existingConns);
                    }
                }

                // Detect container
                NetworkBuilder.DetectAndAssignContainer(zdo, bounds, zdoid);

                zdo.Set(ZDOFields.IC_IsNew, false);
                Jotunn.Logger.LogDebug($"Connections Detection Completed: {connected.ToArray().ToString()}");
            }
            else
            {
                connected = NetworkBuilder.GetConnectionList(zdo);
            }
            
            // Register/update in network manager
            var mode = (ConduitMode)zdo.GetInt(ZDOFields.IC_Mode, 0);
            ConduitNetworkManager.Instance.RegisterConduit(zdoid, mode, connected, isNewPlacement: isNew);

            Jotunn.Logger.LogDebug($"Register Conduit Completed.");

            isNew = false;

            Jotunn.Logger.LogDebug($"[ConduitProcessor] Processed {zdoid}: {connected.Count} connections");
        }

        public static void Clear()
        {
            _pendingConduits.Clear();
            _pendingConduitsRemoval.Clear();
        }
    }
}
