using ItemConduit.Core;
using UnityEngine;

namespace ItemConduit.Transfer
{
    /// <summary>
    /// Placeholder for Phase 5: Item Transfer System.
    /// Will handle server-side item transfers between extract and insert nodes.
    /// </summary>
    public class TransferManager
    {
        private static TransferManager _instance;
        public static TransferManager Instance => _instance ??= new();

        private float _lastTickTime;
        private float _tickInterval = 1.0f;

        public void Update()
        {
            // Only run on server
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;

            // Check tick interval
            if (Time.time - _lastTickTime < _tickInterval) return;
            _lastTickTime = Time.time;

            // TODO: Phase 5 implementation
            // ProcessTransfers();
        }

        /// <summary>
        /// Placeholder: Process all valid networks and transfer items.
        /// </summary>
        private void ProcessTransfers()
        {
            var networks = ConduitNetworkManager.Instance.GetValidNetworks();
            foreach (var network in networks)
            {
                ProcessNetwork(network);
            }
        }

        /// <summary>
        /// Placeholder: Process a single network's transfers.
        /// </summary>
        private void ProcessNetwork(ConduitNetwork network)
        {
            // TODO: Phase 5 implementation
            // For each extract node:
            //   1. Get container inventory
            //   2. Filter items based on channel/filter settings
            //   3. Sort insert nodes by priority
            //   4. Transfer items to insert nodes
            //   5. Handle overflow/full containers
        }

        /// <summary>
        /// Set transfer tick interval from config.
        /// </summary>
        public void SetTickInterval(float interval)
        {
            _tickInterval = Mathf.Max(0.1f, interval);
        }
    }
}
