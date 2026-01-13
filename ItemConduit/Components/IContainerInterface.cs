using UnityEngine;

namespace ItemConduit.Components
{
    /// <summary>
    /// Interface for objects that can interact with conduits (Container, Fireplace, Smelter, etc).
    /// Provides unified access to ZDO data and inventory regardless of underlying type.
    /// </summary>
    public interface IContainerInterface
    {
        /// <summary>
        /// The ZDO ID of this container.
        /// </summary>
        ZDOID Zdoid { get; }

        /// <summary>
        /// Access to the ZDO for reading/writing custom fields.
        /// </summary>
        ZDO Zdo { get; }

        /// <summary>
        /// The inventory associated with this container (null if not available).
        /// </summary>
        Inventory Inventory { get; }

        /// <summary>
        /// Whether this container is currently valid and accessible.
        /// </summary>
        bool IsValid { get; }

        /// <summary>
        /// The prefab hash for this container type.
        /// </summary>
        int PrefabHash { get; }

        /// <summary>
        /// Transform for parenting wireframes and getting position.
        /// </summary>
        UnityEngine.Transform Transform { get; }

        /// <summary>
        /// Display name for debugging/UI.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Compute the OBB bounds for this container in world space.
        /// </summary>
        Collision.OrientedBoundingBox? ComputeBounds();
    }

    /// <summary>
    /// Type of container for specialized handling.
    /// </summary>
    public enum ContainerType
    {
        Container = 0,
        Fireplace = 1,
        Smelter = 2,
        // Future: Fermenter, CookingStation, etc.
    }
}
