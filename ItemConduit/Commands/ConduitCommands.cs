using System.Collections.Generic;
using System.Linq;
using ItemConduit.Collision;
using ItemConduit.Core;
using ItemConduit.GUI;
using ItemConduit.Utils;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace ItemConduit.Commands
{
    public static class ConduitCommands
    {
        public static void RegisterCommands()
        {
            CommandManager.Instance.AddConsoleCommand(new WireframeCommand());
            CommandManager.Instance.AddConsoleCommand(new RebuildCommand());
            CommandManager.Instance.AddConsoleCommand(new StatsCommand());
            CommandManager.Instance.AddConsoleCommand(new DebugCommand());
        }

        private static ZDO FindNearestConduit(Vector3 position, float radius)
        {
            var sector = ZoneSystem.GetZone(position);
            var zdos = new List<ZDO>();

            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    var checkSector = new Vector2i(sector.x + x, sector.y + y);
                    ZDOMan.instance.FindSectorObjects(checkSector, 1, 0, zdos);
                }
            }

            ZDO nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var zdo in zdos)
            {
                var mode = zdo.GetInt(ZDOFields.IC_Mode, -1);
                if (mode < 0) continue;

                var dist = Vector3.Distance(position, zdo.GetPosition());
                if (dist <= radius && dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = zdo;
                }
            }

            return nearest;
        }

        private static int RebuildNetworksInArea(Vector3 center, float radius)
        {
            var sector = ZoneSystem.GetZone(center);
            var zdos = new List<ZDO>();

            int sectorRadius = Mathf.CeilToInt(radius / 64f) + 1;
            for (int x = -sectorRadius; x <= sectorRadius; x++)
            {
                for (int y = -sectorRadius; y <= sectorRadius; y++)
                {
                    var checkSector = new Vector2i(sector.x + x, sector.y + y);
                    ZDOMan.instance.FindSectorObjects(checkSector, 1, 0, zdos);
                }
            }

            int count = 0;
            foreach (var zdo in zdos)
            {
                var mode = zdo.GetInt(ZDOFields.IC_Mode, -1);
                if (mode < 0) continue;

                var dist = Vector3.Distance(center, zdo.GetPosition());
                if (dist > radius) continue;

                var position = zdo.GetPosition();
                var rotation = zdo.GetRotation();

                var boundStr = zdo.GetString(ZDOFields.IC_Bound, "");
                OrientedBoundingBox bounds;
                if (string.IsNullOrEmpty(boundStr))
                {
                    bounds = new OrientedBoundingBox(position, rotation, new Vector3(2f, 0.1f, 0.1f));
                    zdo.Set(ZDOFields.IC_Bound, bounds.Serialize());
                }
                else
                {
                    bounds = OrientedBoundingBox.Deserialize(boundStr);
                }

                var connected = ConduitSpatialQuery.FindConnectedConduits(bounds, zdo.m_uid);
                NetworkBuilder.SetConnectionList(zdo, connected);
                count++;
            }

            NetworkBuilder.RebuildAllNetworks();
            return count;
        }

        private class WireframeCommand : ConsoleCommand
        {
            public override string Name => "ic_wireframe";
            public override string Help => "[on/off] Toggle conduit wireframe visualization";

            public override void Run(string[] args)
            {
                if (args.Length < 1)
                {
                    var current = ICGUIManager.Instance.IsWireframeEnabled();
                    ICGUIManager.Instance.SetWireframeEnabled(!current);
                    Console.instance.Print($"Wireframe: {(!current ? "ON" : "OFF")}");
                    return;
                }

                var state = args[0].ToLower();
                bool enable = state == "on" || state == "true" || state == "1";
                ICGUIManager.Instance.SetWireframeEnabled(enable);
                Console.instance.Print($"Wireframe: {(enable ? "ON" : "OFF")}");
            }
        }

        private class RebuildCommand : ConsoleCommand
        {
            public override string Name => "ic_rebuild";
            public override string Help => "[radius] Rebuild conduit networks within radius (default: 50)";

            public override void Run(string[] args)
            {
                if (!ZNet.instance.IsServer())
                {
                    Console.instance.Print("This command can only be run on the server");
                    return;
                }

                float radius = 50f;
                if (args.Length >= 1 && float.TryParse(args[0], out float parsed))
                    radius = parsed;

                var player = Player.m_localPlayer;
                if (player == null)
                {
                    Console.instance.Print("No player found");
                    return;
                }

                int rebuilt = RebuildNetworksInArea(player.transform.position, radius);
                Console.instance.Print($"Rebuilt {rebuilt} conduits within {radius}m");
            }
        }

        private class StatsCommand : ConsoleCommand
        {
            public override string Name => "ic_stats";
            public override string Help => "Show conduit network statistics";

            public override void Run(string[] args)
            {
                var networks = ConduitNetworkManager.Instance.GetValidNetworks().ToList();
                var allNetworks = ConduitNetworkManager.Instance.GetAllNetworks().ToList();
                var totalConduits = allNetworks.Sum(n => n.Conduits.Count);
                var totalExtract = allNetworks.Sum(n => n.ExtractNodes.Count);
                var totalInsert = allNetworks.Sum(n => n.InsertNodes.Count);

                Console.instance.Print("--- ItemConduit Stats ---");
                Console.instance.Print($"Valid Networks: {networks.Count}");
                Console.instance.Print($"Total Networks: {allNetworks.Count}");
                Console.instance.Print($"Total Conduits: {totalConduits}");
                Console.instance.Print($"Extract Nodes: {totalExtract}");
                Console.instance.Print($"Insert Nodes: {totalInsert}");
                Console.instance.Print($"Transfer Rate: {Plugin.Instance.ConduitConfig.TransferRate.Value}");
                Console.instance.Print($"Transfer Tick: {Plugin.Instance.ConduitConfig.TransferTick.Value}s");
            }
        }

        private class DebugCommand : ConsoleCommand
        {
            public override string Name => "ic_debug";
            public override string Help => "Show debug info for nearest conduit";
            public override bool IsCheat => true;

            public override void Run(string[] args)
            {
                var player = Player.m_localPlayer;
                if (player == null)
                {
                    Console.instance.Print("No player found");
                    return;
                }

                var nearest = FindNearestConduit(player.transform.position, 10f);
                if (nearest == null)
                {
                    Console.instance.Print("No conduit found nearby");
                    return;
                }

                Console.instance.Print("--- Conduit Debug ---");
                Console.instance.Print($"ZDOID: {nearest.m_uid}");
                Console.instance.Print($"Position: {nearest.GetPosition()}");
                Console.instance.Print($"Mode: {(ConduitMode)nearest.GetInt(ZDOFields.IC_Mode, 0)}");
                Console.instance.Print($"NetworkID: {nearest.GetString(ZDOFields.IC_NetworkID, "N/A")}");
                Console.instance.Print($"Container: {nearest.GetZDOID(ZDOFields.IC_ContainerZDOID)}");
                Console.instance.Print($"Connections: {NetworkBuilder.GetConnectionList(nearest).Count}");
                Console.instance.Print($"Channel: {nearest.GetInt(ZDOFields.IC_Channel, 0)}");
                Console.instance.Print($"Priority: {nearest.GetInt(ZDOFields.IC_Priority, 0)}");
            }
        }
    }
}
