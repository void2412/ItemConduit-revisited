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

        public void RegisterConduit(ZDOID zdoid, ConduitMode mode, List<ZDOID> connections, bool isNewPlacement = false)
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

            // Find or create network (new placement or no existing network)
            string networkId = FindConnectedNetworkId(connections);
            if (networkId == null)
            {
                var network = new ConduitNetwork();
                networkId = network.NetworkId;
                _networks[networkId] = network;
            }

            _networks[networkId].AddConduit(zdoid, mode);
            _conduitToNetwork[zdoid] = networkId;

            // Update ZDO with network ID (server assigns to new placements)
            zdo?.Set(ZDOFields.IC_NetworkID, networkId);

            // Check for network merges
            CheckAndMergeNetworks(connections, networkId);
        }

        public void UnregisterConduit(ZDOID zdoid)
        {
            if (!_conduitToNetwork.TryGetValue(zdoid, out var networkId))
                return;

            // IMPORTANT: Get connections BEFORE removing from network
            var connections = GetConnectionsOf(zdoid);

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

        private string FindConnectedNetworkId(List<ZDOID> connections)
        {
            foreach (var conn in connections)
            {
                if (_conduitToNetwork.TryGetValue(conn, out var netId))
                    return netId;
            }
            return null;
        }

        private void CheckAndMergeNetworks(List<ZDOID> connections, string targetNetworkId)
        {
            var networksToMerge = connections
                .Where(c => _conduitToNetwork.ContainsKey(c))
                .Select(c => _conduitToNetwork[c])
                .Where(n => n != targetNetworkId)
                .Distinct()
                .ToList();

            foreach (var sourceNetId in networksToMerge)
                MergeNetworks(sourceNetId, targetNetworkId);
        }

        private void MergeNetworks(string sourceId, string targetId)
        {
            if (!_networks.TryGetValue(sourceId, out var source))
                return;
            if (!_networks.TryGetValue(targetId, out var target))
                return;

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
        }

        /// <summary>
        /// BFS from neighbors of removed conduit to detect disconnected subgraphs.
        /// If neighbors can't reach each other, network has split.
        /// </summary>
        private void CheckNetworkSplit(string networkId, ZDOID removedConduit, List<ZDOID> neighbors)
        {
            if (neighbors.Count <= 1)
                return; // No split possible with 0-1 neighbors

            var network = _networks[networkId];

            // BFS from first neighbor to find its connected component
            var visited = new HashSet<ZDOID>();
            var queue = new Queue<ZDOID>();

            queue.Enqueue(neighbors[0]);
            visited.Add(neighbors[0]);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var conn in GetConnectionsOf(current))
                {
                    if (conn == removedConduit) continue; // Skip removed
                    if (!network.Conduits.Contains(conn)) continue; // Not in this network
                    if (visited.Add(conn))
                        queue.Enqueue(conn);
                }
            }

            // Check if all other neighbors were reached
            var unreached = neighbors.Skip(1).Where(n => !visited.Contains(n)).ToList();

            if (unreached.Count == 0)
                return; // All neighbors still connected → no split

            // Split detected: Create new network(s) for unreached components
            foreach (var orphan in unreached)
            {
                if (visited.Contains(orphan)) continue; // Already processed

                // BFS to find this orphan's connected component
                var newNetwork = new ConduitNetwork();
                var orphanQueue = new Queue<ZDOID>();
                orphanQueue.Enqueue(orphan);

                while (orphanQueue.Count > 0)
                {
                    var node = orphanQueue.Dequeue();
                    if (!visited.Add(node)) continue; // Already visited
                    if (!network.Conduits.Contains(node)) continue; // Not in original network

                    var mode = GetConduitMode(node);
                    newNetwork.AddConduit(node, mode);
                    _conduitToNetwork[node] = newNetwork.NetworkId;

                    // Update ZDO with new network ID
                    var zdo = ZDOMan.instance.GetZDO(node);
                    zdo?.Set(ZDOFields.IC_NetworkID, newNetwork.NetworkId);

                    foreach (var conn in GetConnectionsOf(node))
                    {
                        if (conn != removedConduit && !visited.Contains(conn))
                            orphanQueue.Enqueue(conn);
                    }
                }

                // Register new network and remove conduits from original
                _networks[newNetwork.NetworkId] = newNetwork;
                foreach (var zdoid in newNetwork.Conduits)
                {
                    network.RemoveConduit(zdoid);
                }

                Jotunn.Logger.LogInfo($"Network split: created {newNetwork.NetworkId.Substring(0, 8)} with {newNetwork.Conduits.Count} conduits");
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
