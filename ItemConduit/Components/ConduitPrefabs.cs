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
            "wood_beam",     // 2m horizontal beam
            "wood_pole",      // 1m vertical pole
            "wood_pole2"      // 2m vertical pole
        };

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
            var conduitName = $"ic_{prefabName}";

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
    }
}
