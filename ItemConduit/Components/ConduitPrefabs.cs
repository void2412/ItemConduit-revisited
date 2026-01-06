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

            // Ensure BoxCollider exists for OBB collision detection
            var boxCollider = prefab.GetComponent<BoxCollider>();
            if (boxCollider == null)
            {
                boxCollider = prefab.AddComponent<BoxCollider>();
                // Calculate bounds from mesh
                var meshFilter = prefab.GetComponentInChildren<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    var mesh = meshFilter.sharedMesh;
                    boxCollider.center = mesh.bounds.center;
                    boxCollider.size = mesh.bounds.size;
                    Jotunn.Logger.LogDebug($"[ConduitPrefabs] Added BoxCollider to {conduitName}: center={boxCollider.center}, size={boxCollider.size}");
                }
                else
                {
                    // Fallback: default beam size (4m x 0.2m x 0.2m in local space)
                    boxCollider.center = Vector3.zero;
                    boxCollider.size = new Vector3(4f, 0.2f, 0.2f);
                    Jotunn.Logger.LogWarning($"[ConduitPrefabs] No mesh found for {conduitName}, using default BoxCollider size");
                }
            }

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
