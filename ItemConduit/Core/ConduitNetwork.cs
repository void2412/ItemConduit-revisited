using System;
using System.Collections.Generic;
using System.Linq;
using ItemConduit.Utils;

namespace ItemConduit.Core
{
    public class ConduitNetwork
    {
        public string NetworkId { get; }
        public HashSet<ZDOID> Conduits { get; } = new();
        public HashSet<ZDOID> ExtractNodes { get; } = new();
        public HashSet<ZDOID> InsertNodes { get; } = new();

        public bool IsValid => ExtractNodes.Count > 0 && InsertNodes.Count > 0;

        public ConduitNetwork(string networkId = null)
        {
            NetworkId = networkId ?? Guid.NewGuid().ToString();
        }

        public void AddConduit(ZDOID zdoid, ConduitMode mode)
        {
            Conduits.Add(zdoid);
            if (mode == ConduitMode.Extract)
                ExtractNodes.Add(zdoid);
            else if (mode == ConduitMode.Insert)
                InsertNodes.Add(zdoid);
        }

        public void RemoveConduit(ZDOID zdoid)
        {
            Conduits.Remove(zdoid);
            ExtractNodes.Remove(zdoid);
            InsertNodes.Remove(zdoid);
        }

        public List<ZDOID> GetConduitsByChannel(int channel)
        {
            return Conduits
                .Where(z => GetConduitChannel(z) == channel)
                .ToList();
        }

        private int GetConduitChannel(ZDOID zdoid)
        {
            var zdo = ZDOMan.instance.GetZDO(zdoid);
            return zdo?.GetInt(ZDOFields.IC_Channel, 0) ?? 0;
        }
    }
}
