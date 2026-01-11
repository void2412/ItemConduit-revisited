using System.Collections.Generic;
using UnityEngine;

namespace ItemConduit.Components
{
    /// <summary>
    /// Container dimensions for inventory size lookup.
    /// </summary>
    public struct ContainerDimensions
    {
        public int Width;
        public int Height;

        public ContainerDimensions(int width, int height)
        {
            Width = width;
            Height = height;
        }
    }

    /// <summary>
    /// Tracks all container prefab hashes for fast ZDO detection.
    /// Populated during ZNetScene.Awake by scanning prefabs with Container component.
    /// </summary>
    public static class ContainerPrefabs
    {
        /// <summary>
        /// HashSet of all container prefab hashes for fast lookup.
        /// </summary>
        public static readonly HashSet<int> ContainerPrefabHashes = new();

        /// <summary>
        /// Container dimensions by prefab hash (for inventory size).
        /// </summary>
        public static readonly Dictionary<int, ContainerDimensions> ContainerDimensions = new();

        /// <summary>
        /// Container mesh bounds by prefab hash (for OBB collision).
        /// Stores local-space bounds from MeshFilter.
        /// </summary>
        public static readonly Dictionary<int, Bounds> ContainerBounds = new();

        public static void Clear()
        {
            ContainerPrefabHashes.Clear();
            ContainerDimensions.Clear();
            ContainerBounds.Clear();
        }
    }
}
