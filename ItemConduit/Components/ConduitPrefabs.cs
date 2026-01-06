using System.Collections.Generic;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace ItemConduit.Components
{
    public static class ConduitPrefabs
    {
        private static readonly string[] BasePrefabs = {
            "wood_beam_1",     // 1m horizontal beam
            "wood_beam",       // 2m horizontal beam
            "wood_pole",       // 1m vertical pole
            "wood_pole2"       // 2m vertical pole
        };

        /// <summary>
        /// HashSet of all conduit prefab hashes for fast lookup.
        /// Populated during prefab registration.
        /// </summary>
        public static readonly HashSet<int> ConduitPrefabHashes = new();

        public static void RegisterPrefabs()
        {
            PrefabManager.OnVanillaPrefabsAvailable += CreateConduitPrefabs;
        }

        private static void CreateConduitPrefabs()
        {
            foreach (var prefabName in BasePrefabs)
            {
                CreateConduitFromPrefab(prefabName);
            }

            PrefabManager.OnVanillaPrefabsAvailable -= CreateConduitPrefabs;
        }

        private static void CreateConduitFromPrefab(string prefabName)
        {
            var conduitName = GetICPrefabName(prefabName);

            // Clone the prefab
            var basePrefab = PrefabManager.Instance.GetPrefab(prefabName);
            if (basePrefab == null)
            {
                Jotunn.Logger.LogWarning($"Could not find prefab: {prefabName}");
                return;
            }

            var config = new PieceConfig
            {
                Name = GetDisplayName(prefabName),
                Description = "Item transfer conduit",
                PieceTable = "_HammerPieceTable",
                Category = "Misc",
                AllowedInDungeons = false,
                Requirements = new[]
                {
                    new RequirementConfig("Wood", 2, 0, true)
                }
            };

            var customPiece = new CustomPiece(conduitName, prefabName, config);

            // Modify the prefab
            var prefab = customPiece.PiecePrefab;

            // Remove WearNTear (no decay)
            var wearNTear = prefab.GetComponent<WearNTear>();
            if (wearNTear != null)
                Object.DestroyImmediate(wearNTear);

            // Add Conduit component
            prefab.AddComponent<Conduit>();

            // Register piece
            PieceManager.Instance.AddPiece(customPiece);

            // Store prefab hash for ZDO filtering
            ConduitPrefabHashes.Add(conduitName.GetStableHashCode());

            Jotunn.Logger.LogInfo($"Registered conduit: {conduitName}");
        }

        private static string GetDisplayName(string prefabName)
        {
            return prefabName switch
            {
                "wood_beam_1" => "Conduit Beam 1m",
                "wood_beam" => "Conduit Beam 2m",
                "wood_pole" => "Conduit Pole 1m",
                "wood_pole2" => "Conduit Pole 2m",
                _ => "Conduit"
            };
        }

        private static string GetICPrefabName(string prefabName)
        {
            return prefabName switch
            {
                "wood_beam_1" => "IC_conduit_beam_1m",
                "wood_beam" => "IC_conduit_beam_2m",
                "wood_pole" => "IC_conduit_pole_1m",
                "wood_pole2" => "IC_conduit_pole_2m",
                _ => "IC"
            };
        }
    }
}
