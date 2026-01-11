using System.Collections;
using HarmonyLib;
using ItemConduit.Components;
using ItemConduit.Core;
using UnityEngine;

namespace ItemConduit.Patches
{
    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
    public static class ZNetScenePatches
    {
        [HarmonyPostfix]
        public static void Postfix(ZNetScene __instance)
        {
            // Scan prefabs for Container components
            ScanContainerPrefabs(__instance);

            // Delay rebuild to ensure all prefabs are registered
            __instance.StartCoroutine(DelayedRebuild());
        }

        private static void ScanContainerPrefabs(ZNetScene scene)
        {
            ContainerPrefabs.Clear();

            foreach (var kvp in scene.m_namedPrefabs)
            {
                var prefab = kvp.Value;
                if (prefab == null) continue;

                var container = prefab.GetComponent<Container>();
                if (container == null) continue;

                int prefabHash = kvp.Key;
                ContainerPrefabs.ContainerPrefabHashes.Add(prefabHash);

                // Store dimensions from Container component
                ContainerPrefabs.ContainerDimensions[prefabHash] = new ContainerDimensions(
                    container.m_width,
                    container.m_height
                );

                // Extract bounds from MeshFilter for OBB
                var meshFilter = prefab.GetComponentInChildren<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    ContainerPrefabs.ContainerBounds[prefabHash] = meshFilter.sharedMesh.bounds;
                    Jotunn.Logger.LogDebug($"[ContainerPrefabs] {prefab.name}: {container.m_width}x{container.m_height}, bounds={meshFilter.sharedMesh.bounds}");
                }
                else
                {
                    // Fallback: try MeshCollider
                    var meshCollider = prefab.GetComponentInChildren<MeshCollider>();
                    if (meshCollider != null && meshCollider.sharedMesh != null)
                    {
                        ContainerPrefabs.ContainerBounds[prefabHash] = meshCollider.sharedMesh.bounds;
                    }
                    else
                    {
                        // Last fallback: BoxCollider
                        var boxCollider = prefab.GetComponentInChildren<BoxCollider>();
                        if (boxCollider != null)
                        {
                            ContainerPrefabs.ContainerBounds[prefabHash] = new Bounds(boxCollider.center, boxCollider.size);
                        }
                    }
                }
            }

            Jotunn.Logger.LogInfo($"[ContainerPrefabs] Registered {ContainerPrefabs.ContainerPrefabHashes.Count} container prefabs");
        }

        private static IEnumerator DelayedRebuild()
        {
            // Wait for prefabs to be fully registered
            yield return new WaitForSeconds(2f);

            if (ZNet.instance.IsServer())
            {
                NetworkBuilder.RebuildAllNetworks();
            }
        }
    }

    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.OnDestroy))]
    public static class ZNetSceneDestroyPatches
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            ConduitNetworkManager.Instance.Clear();
        }
    }
}
