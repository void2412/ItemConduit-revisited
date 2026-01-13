using System.Collections.Generic;
using System.Linq;
using ItemConduit.Utils;

namespace ItemConduit.Core
{
    public class ConduitNetworkManager
    {
        private static ConduitNetworkManager _instance;
        public static ConduitNetworkManager Instance => _instance ??= new();

        private readonly Dictionary<string, ConduitNetwork> _networks = new();
        private readonly Dictionary<ZDOID, string> _conduitToNetwork = new();

        public void RegisterConduit(ZDOID zdoid, ConduitMode mode, HashSet<ZDOID> connections, bool isNewPlacement = false)
        {
            // For existing conduits (world load), check if already has network ID
            var zdo = ZDOMan.instance.GetZDO(zdoid);
            if (!isNewPlacement && zdo != null)
            {
                var existingNetworkId = zdo.GetString(ZDOFields.IC_NetworkID, "");
                if (!string.IsNullOrEmpty(existingNetworkId))
                {
                    // Re-register to existing network
                    if (!_networks.ContainsKey(existingNetworkId))
                        _networks[existingNetworkId] = new ConduitNetwork(existingNetworkId);

                    _networks[existingNetworkId].AddConduit(zdoid, mode);
                    _conduitToNetwork[zdoid] = existingNetworkId;
                    return;
                }
            }

            // Find all connected networks and pick the largest (minimizes ZDO writes on merge)
            var connectedNetworkIds = connections
                .Where(c => _conduitToNetwork.ContainsKey(c))
                .Select(c => _conduitToNetwork[c])
                .Distinct()
                .ToList();

            string networkId;
            if (connectedNetworkIds.Count == 0)
            {
                // No connected networks - create new
                var network = new ConduitNetwork();
                networkId = network.NetworkId;
                _networks[networkId] = network;
            }
            else if (connectedNetworkIds.Count == 1)
            {
                // Single network - just use it
                networkId = connectedNetworkIds[0];
            }
            else
            {
                // Multiple networks - find largest to minimize ZDO writes
                networkId = connectedNetworkIds
                    .OrderByDescending(id => _networks[id].Conduits.Count)
                    .First();

                // Merge smaller networks into largest
                foreach (var sourceNetId in connectedNetworkIds.Where(id => id != networkId))
                    MergeNetworks(sourceNetId, networkId);
            }

            _networks[networkId].AddConduit(zdoid, mode);
            _conduitToNetwork[zdoid] = networkId;

            // Update ZDO with network ID (server assigns to new placements)
            zdo?.Set(ZDOFields.IC_NetworkID, networkId);
        }

        public void UnregisterConduit(CachedConduitData cached)
        {
            var zdoid = cached.Zdoid;

            if (!_conduitToNetwork.TryGetValue(zdoid, out var networkId))
                return;

            // Use cached connections - ZDO already destroyed
            var connections = cached.Connections;

            var network = _networks[networkId];
            network.RemoveConduit(zdoid);
            _conduitToNetwork.Remove(zdoid);

            if (network.Conduits.Count == 0)
            {
                _networks.Remove(networkId);
            }
            else if (connections.Count > 1)
            {
                // Only check split if removed conduit had multiple connections
                CheckNetworkSplit(networkId, zdoid, connections);
            }
        }

        private List<ZDOID> GetConnectionsOf(ZDOID zdoid)
        {
            var zdo = ZDOMan.instance.GetZDO(zdoid);
            if (zdo == null) return new List<ZDOID>();

            var bytes = zdo.GetByteArray(ZDOFields.IC_ConnectionList, null);
            if (bytes == null || bytes.Length == 0) return new List<ZDOID>();

            var pkg = new ZPackage(bytes);
            var count = pkg.ReadInt();
            var list = new List<ZDOID>(count);
            for (int i = 0; i < count; i++)
                list.Add(pkg.ReadZDOID());
            return list;
        }

        public ConduitNetwork GetNetwork(string networkId)
        {
            return _networks.TryGetValue(networkId, out var net) ? net : null;
        }

        public IEnumerable<ConduitNetwork> GetValidNetworks()
        {
            return _networks.Values.Where(n => n.IsValid);
        }

        public IEnumerable<ConduitNetwork> GetAllNetworks()
        {
            return _networks.Values;
        }

        /// <summary>
        /// Merge source network into target. All source conduits get target's network ID.
        /// </summary>
        private void MergeNetworks(string sourceId, string targetId)
        {
            if (!_networks.TryGetValue(sourceId, out var source))
                return;
            if (!_networks.TryGetValue(targetId, out var target))
                return;

            var sourceCount = source.Conduits.Count;
            foreach (var zdoid in source.Conduits)
            {
                var mode = GetConduitMode(zdoid);
                target.AddConduit(zdoid, mode);
                _conduitToNetwork[zdoid] = targetId;

                // Update ZDO
                var zdo = ZDOMan.instance.GetZDO(zdoid);
                zdo?.Set(ZDOFields.IC_NetworkID, targetId);
            }

            _networks.Remove(sourceId);
            Jotunn.Logger.LogInfo($"Network merge: {sourceCount} conduits from {sourceId.Substring(0, 8)} → {targetId.Substring(0, 8)} (target now has {target.Conduits.Count})");
        }

        /// <summary>
        /// BFS from neighbors of removed conduit to detect disconnected subgraphs.
        /// Optimized: Larger component keeps original network ID to minimize ZDO syncs.
        /// </summary>
        private void CheckNetworkSplit(string networkId, ZDOID removedConduit, HashSet<ZDOID> neighbors)
        {
            if (neighbors.Count <= 1)
                return; // No split possible with 0-1 neighbors

            var network = _networks[networkId];

            // Find all connected components via BFS from each unvisited neighbor
            var globalVisited = new HashSet<ZDOID>();
            var components = new List<HashSet<ZDOID>>();

            foreach (var neighbor in neighbors)
            {
                if (globalVisited.Contains(neighbor)) continue;

                // BFS to find this component
                var component = new HashSet<ZDOID>();
                var queue = new Queue<ZDOID>();
                queue.Enqueue(neighbor);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    if (!globalVisited.Add(current)) continue;
                    if (!network.Conduits.Contains(current)) continue;

                    component.Add(current);

                    foreach (var conn in GetConnectionsOf(current))
                    {
                        if (conn == removedConduit) continue;
                        if (!globalVisited.Contains(conn) && network.Conduits.Contains(conn))
                            queue.Enqueue(conn);
                    }
                }

                if (component.Count > 0)
                    components.Add(component);
            }

            // No split if only one component found
            if (components.Count <= 1)
                return;

            // Find largest component - it keeps the original network ID
            var largestIdx = 0;
            for (int i = 1; i < components.Count; i++)
            {
                if (components[i].Count > components[largestIdx].Count)
                    largestIdx = i;
            }

            // Reassign smaller components to new networks (minimizes ZDO writes)
            for (int i = 0; i < components.Count; i++)
            {
                if (i == largestIdx) continue; // Largest keeps original

                var component = components[i];
                var newNetwork = new ConduitNetwork();

                foreach (var zdoid in component)
                {
                    var mode = GetConduitMode(zdoid);
                    newNetwork.AddConduit(zdoid, mode);
                    _conduitToNetwork[zdoid] = newNetwork.NetworkId;
                    network.RemoveConduit(zdoid);

                    // Update ZDO with new network ID
                    var zdo = ZDOMan.instance.GetZDO(zdoid);
                    zdo?.Set(ZDOFields.IC_NetworkID, newNetwork.NetworkId);
                }

                _networks[newNetwork.NetworkId] = newNetwork;
                Jotunn.Logger.LogInfo($"Network split: {newNetwork.Conduits.Count} conduits → new {newNetwork.NetworkId.Substring(0, 8)} (kept {components[largestIdx].Count} in original)");
            }
        }

        private ConduitMode GetConduitMode(ZDOID zdoid)
        {
            var zdo = ZDOMan.instance.GetZDO(zdoid);
            return (ConduitMode)(zdo?.GetInt(ZDOFields.IC_Mode, 0) ?? 0);
        }

        public void Clear()
        {
            _networks.Clear();
            _conduitToNetwork.Clear();
        }
    }
}
