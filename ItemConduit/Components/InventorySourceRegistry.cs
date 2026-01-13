using System.Collections.Generic;

namespace ItemConduit.Components
{
    /// <summary>
    /// Data about an inventory source prefab.
    /// OBB bounds are computed at runtime by IContainerInterface.ComputeBounds() and stored in ZDO's IC_Bound.
    /// </summary>
    public struct InventorySourceData
    {
        public ContainerType Type;
        public int Width;
        public int Height;

        public InventorySourceData(ContainerType type, int width, int height)
        {
            Type = type;
            Width = width;
            Height = height;
        }
    }

    /// <summary>
    /// Centralized registry for all inventory source prefabs (Container, Fireplace, Smelter, etc).
    /// Used for fast ZDO detection and prefab data lookup.
    /// </summary>
    public static class InventorySourceRegistry
    {
        /// <summary>
        /// All registered inventory source prefab hashes.
        /// </summary>
        public static readonly HashSet<int> AllPrefabHashes = new();

        /// <summary>
        /// Prefab hashes by type for type-specific queries.
        /// </summary>
        public static readonly Dictionary<ContainerType, HashSet<int>> PrefabHashesByType = new()
        {
            { ContainerType.Container, new HashSet<int>() },
            { ContainerType.Fireplace, new HashSet<int>() },
            { ContainerType.Smelter, new HashSet<int>() },
        };

        /// <summary>
        /// Detailed data about each prefab.
        /// </summary>
        public static readonly Dictionary<int, InventorySourceData> PrefabData = new();

        /// <summary>
        /// Register a Container prefab.
        /// </summary>
        public static void RegisterContainer(int prefabHash, int width, int height)
        {
            Register(prefabHash, new InventorySourceData(ContainerType.Container, width, height));
        }

        /// <summary>
        /// Register a Fireplace prefab (future).
        /// </summary>
        public static void RegisterFireplace(int prefabHash)
        {
            // Fireplaces typically have 1 fuel slot
            Register(prefabHash, new InventorySourceData(ContainerType.Fireplace, 1, 1));
        }

        /// <summary>
        /// Register a Smelter prefab (future).
        /// </summary>
        public static void RegisterSmelter(int prefabHash, int oreSlots, int fuelSlots)
        {
            Register(prefabHash, new InventorySourceData(ContainerType.Smelter, oreSlots, fuelSlots));
        }

        private static void Register(int prefabHash, InventorySourceData data)
        {
            AllPrefabHashes.Add(prefabHash);
            PrefabHashesByType[data.Type].Add(prefabHash);
            PrefabData[prefabHash] = data;
        }

        /// <summary>
        /// Check if prefab hash is a registered inventory source.
        /// </summary>
        public static bool IsInventorySource(int prefabHash) => AllPrefabHashes.Contains(prefabHash);

        /// <summary>
        /// Get the type of a registered prefab.
        /// </summary>
        public static ContainerType? GetType(int prefabHash)
        {
            return PrefabData.TryGetValue(prefabHash, out var data) ? data.Type : null;
        }

        /// <summary>
        /// Get data for a registered prefab.
        /// </summary>
        public static InventorySourceData? GetData(int prefabHash)
        {
            return PrefabData.TryGetValue(prefabHash, out var data) ? data : null;
        }

        public static void Clear()
        {
            AllPrefabHashes.Clear();
            foreach (var set in PrefabHashesByType.Values)
                set.Clear();
            PrefabData.Clear();
        }
    }
}
