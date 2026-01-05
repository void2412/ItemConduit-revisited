using System.Collections.Generic;
using ItemConduit.Utils;
using UnityEngine;

namespace ItemConduit.Collision
{
    public static class ConduitSpatialQuery
    {
        private const float SearchRadius = 10f;
        private const float ContainerSearchRadius = 5f;

        // Container prefab hashes (chest variants)
        private static readonly HashSet<int> ContainerPrefabHashes = new();
        private static bool _containerHashesInitialized = false;

        private static void InitContainerHashes()
        {
            if (_containerHashesInitialized) return;

            var containerPrefabs = new[] {
                "piece_chest_wood", "piece_chest", "piece_chest_private",
                "piece_chest_blackmetal", "piece_chest_treasure"
            };

            foreach (var prefab in containerPrefabs)
                ContainerPrefabHashes.Add(prefab.GetStableHashCode());

            _containerHashesInitialized = true;
        }

        public static List<ZDOID> FindConnectedConduits(
            OrientedBoundingBox sourceBounds,
            ZDOID sourceId)
        {
            var connected = new List<ZDOID>();
            var sector = ZoneSystem.GetZone(sourceBounds.Center);
            var zdos = new List<ZDO>();

            // Query nearby sectors
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    var checkSector = new Vector2i(sector.x + x, sector.y + y);
                    ZDOMan.instance.FindSectorObjects(checkSector, 1, 0, zdos);
                }
            }

            foreach (var zdo in zdos)
            {
                if (zdo.m_uid == sourceId) continue;

                // Check if ZDO is a conduit
                var mode = zdo.GetInt(ZDOFields.IC_Mode, -1);
                if (mode < 0) continue;

                // Distance pre-check
                var dist = Vector3.Distance(sourceBounds.Center, zdo.GetPosition());
                if (dist > SearchRadius) continue;

                var boundStr = zdo.GetString(ZDOFields.IC_Bound, "");
                if (string.IsNullOrEmpty(boundStr)) continue;

                var targetBounds = OrientedBoundingBox.Deserialize(boundStr);
                if (OBBCollision.TestOBBOBB(sourceBounds, targetBounds))
                {
                    connected.Add(zdo.m_uid);
                }
            }

            return connected;
        }

        public static ZDOID? FindConnectedContainer(
            OrientedBoundingBox conduitBounds,
            ZDOID _)
        {
            InitContainerHashes();

            var sector = ZoneSystem.GetZone(conduitBounds.Center);
            var zdos = new List<ZDO>();

            // Query nearby sectors
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    var checkSector = new Vector2i(sector.x + x, sector.y + y);
                    ZDOMan.instance.FindSectorObjects(checkSector, 1, 0, zdos);
                }
            }

            ZDOID? closestContainer = null;
            float closestDistance = float.MaxValue;

            foreach (var zdo in zdos)
            {
                var prefabHash = zdo.GetPrefab();
                if (!ContainerPrefabHashes.Contains(prefabHash))
                    continue;

                var dist = Vector3.Distance(conduitBounds.Center, zdo.GetPosition());
                if (dist > ContainerSearchRadius) continue;

                var containerBounds = new OrientedBoundingBox(
                    zdo.GetPosition(),
                    zdo.GetRotation(),
                    new Vector3(0.5f, 0.5f, 0.5f)
                );

                if (OBBCollision.TestOBBOBB(conduitBounds, containerBounds))
                {
                    if (dist < closestDistance)
                    {
                        closestDistance = dist;
                        closestContainer = zdo.m_uid;
                    }
                }
            }

            return closestContainer;
        }
    }
}
