using System.Collections.Generic;
using ItemConduit.Components;
using ItemConduit.Utils;
using UnityEngine;

namespace ItemConduit.Collision
{
    public static class ConduitSpatialQuery
    {
        private const float SearchRadius = 5f;
        private const float ContainerSearchRadius = 5f;

        /// <summary>
        /// Expand OBB by tolerance on the longest axis for reliable end-to-end collision detection.
        /// </summary>
        private static OrientedBoundingBox ExpandByTolerance(OrientedBoundingBox obb, float tolerance)
        {
            var halfExtents = obb.HalfExtents;
            if (halfExtents.x >= halfExtents.y && halfExtents.x >= halfExtents.z)
                halfExtents.x += tolerance;
            else if (halfExtents.y >= halfExtents.x && halfExtents.y >= halfExtents.z)
                halfExtents.y += tolerance;
            else
                halfExtents.z += tolerance;

            return new OrientedBoundingBox(obb.Center, obb.Rotation, halfExtents);
        }

        public static List<ZDOID> FindConnectedConduits(
            OrientedBoundingBox sourceBounds,
            ZDOID sourceId)
        {
            var connected = new List<ZDOID>();
            var sector = ZoneSystem.GetZone(sourceBounds.Center);
            var zdos = new List<ZDO>();

            // Query 3x3 sectors around center (area=1 covers 3x3)
            ZDOMan.instance.FindSectorObjects(sector, 1, 0, zdos);

            // Apply tolerance to source bounds for collision detection
            var tolerance = Plugin.Instance.ConduitConfig.ConnectionTolerance.Value;
            var expandedSource = ExpandByTolerance(sourceBounds, tolerance);

            Jotunn.Logger.LogDebug($"[SpatialQuery] Found {zdos.Count} ZDOs in 3x3 sectors");
            foreach (var zdo in zdos)
            {
                if (zdo.m_uid == sourceId) continue;

                // Check if ZDO is a conduit prefab
                if (!ConduitPrefabs.ConduitPrefabHashes.Contains(zdo.GetPrefab())) continue;

                Jotunn.Logger.LogDebug($"[SpatialQuery] ZDO {zdo.m_uid} prefab={zdo.GetPrefab()} passed prefab check");

                // Check if ZDO has conduit mode set
                var mode = zdo.GetInt(ZDOFields.IC_Mode, -1);
                if (mode < 0) continue;

                // Distance pre-check
                var dist = Vector3.Distance(sourceBounds.Center, zdo.GetPosition());
                if (dist > SearchRadius) continue;

                var boundStr = zdo.GetString(ZDOFields.IC_Bound, "");
                if (string.IsNullOrEmpty(boundStr)) continue;

                // Apply tolerance to target bounds as well
                var targetBounds = OrientedBoundingBox.Deserialize(boundStr);
                var expandedTarget = ExpandByTolerance(targetBounds, tolerance);

                if (OBBCollision.TestOBBOBB(expandedSource, expandedTarget))
                {
                    connected.Add(zdo.m_uid);
                }
            }

            return connected;
        }

        /// <summary>
        /// Find container connected to conduit. Uses raw OBB without tolerance.
        /// </summary>
        public static ZDOID? FindConnectedContainer(
            OrientedBoundingBox conduitBounds,
            ZDOID _)
        {
            var sector = ZoneSystem.GetZone(conduitBounds.Center);
            var zdos = new List<ZDO>();

            // Query 3x3 sectors around center (area=1 covers 3x3)
            ZDOMan.instance.FindSectorObjects(sector, 1, 0, zdos);

            ZDOID? closestContainer = null;
            float closestDistance = float.MaxValue;

            foreach (var zdo in zdos)
            {
                var prefabHash = zdo.GetPrefab();
                // Use ContainerPrefabs from ZNetScenePatches scan
                if (!ContainerPrefabs.ContainerPrefabHashes.Contains(prefabHash))
                    continue;

                var dist = Vector3.Distance(conduitBounds.Center, zdo.GetPosition());
                if (dist > ContainerSearchRadius) continue;

                // Get container OBB from ZDO (computed client-side)
                var containerBoundStr = zdo.GetString(ZDOFields.IC_Bound, "");
                if (string.IsNullOrEmpty(containerBoundStr))
                {
                    Jotunn.Logger.LogDebug($"[SpatialQuery] Container {zdo.m_uid} has no IC_Bound");
                    continue;
                }

                var containerBounds = OrientedBoundingBox.Deserialize(containerBoundStr);
                if (containerBounds.HalfExtents == Vector3.zero) continue;

                // No tolerance for conduit-container collision
                if (OBBCollision.TestOBBOBB(conduitBounds, containerBounds))
                {
                    if (dist < closestDistance)
                    {
                        closestDistance = dist;
                        closestContainer = zdo.m_uid;
                        Jotunn.Logger.LogDebug($"[SpatialQuery] Container {zdo.m_uid} collision detected at dist={dist}");
                    }
                }
            }

            return closestContainer;
        }
    }
}
