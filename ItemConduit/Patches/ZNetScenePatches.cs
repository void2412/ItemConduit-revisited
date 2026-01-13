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
            // Scan prefabs for inventory source components
            ScanInventorySourcePrefabs(__instance);

            // Delay rebuild to ensure all prefabs are registered
            __instance.StartCoroutine(DelayedRebuild());
        }

        private static void ScanInventorySourcePrefabs(ZNetScene scene)
        {
            InventorySourceRegistry.Clear();

            foreach (var kvp in scene.m_namedPrefabs)
            {
                var prefab = kvp.Value;
                if (prefab == null) continue;

                int prefabHash = kvp.Key;

                // Check for Container component
                var container = prefab.GetComponent<Container>();
                if (container != null)
                {
                    RegisterContainerPrefab(prefab, prefabHash, container);
                    continue;
                }

                // Future: Check for Fireplace component
                // var fireplace = prefab.GetComponent<Fireplace>();
                // if (fireplace != null) { RegisterFireplacePrefab(...); continue; }

                // Future: Check for Smelter component
                // var smelter = prefab.GetComponent<Smelter>();
                // if (smelter != null) { RegisterSmelterPrefab(...); continue; }
            }

            Jotunn.Logger.LogInfo($"[InventorySourceRegistry] Registered {InventorySourceRegistry.AllPrefabHashes.Count} inventory sources");
        }

        private static void RegisterContainerPrefab(GameObject prefab, int prefabHash, Container container)
        {
            // Register container prefab - OBB bounds computed at runtime by ContainerInterfaceComponent
            InventorySourceRegistry.RegisterContainer(prefabHash, container.m_width, container.m_height);
            Jotunn.Logger.LogDebug($"[InventorySourceRegistry] Container {prefab.name}: {container.m_width}x{container.m_height}");
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
