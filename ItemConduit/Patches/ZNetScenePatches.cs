using System.Collections;
using HarmonyLib;
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
            // Delay rebuild to ensure all prefabs are registered
            __instance.StartCoroutine(DelayedRebuild());
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
