using HarmonyLib;
using ItemConduit.Components;
using ItemConduit.Core;
using ItemConduit.GUI;
using UnityEngine;
using System.Text;

namespace ItemConduit.Patches
{
    /// <summary>
    /// Patch Container to add IContainerInterface component and compute OBB.
    /// Server reads IC_Bound from ZDO for collision detection.
    /// </summary>
    [HarmonyPatch(typeof(Container), nameof(Container.Awake))]
    public static class ContainerAwakePatch
    {
        [HarmonyPostfix]
        public static void Postfix(Container __instance)
        {
            // m_nview is set in Container.Awake, but might not be valid yet for prefabs
            var nview = __instance.m_nview;
            if (nview == null || !nview.IsValid()) return;

            var zdo = nview.GetZDO();
            if (zdo == null) return;

            // Add IContainerInterface component if not already present
            var containerInterface = __instance.GetComponent<ContainerInterfaceComponent>();
            if (containerInterface == null)
            {
                containerInterface = __instance.gameObject.AddComponent<ContainerInterfaceComponent>();
            }

            // Store OBB to ZDO using the interface
            if (containerInterface.TryStoreOBB())
            {
                Jotunn.Logger.LogDebug($"[ContainerPatches] Stored OBB for {__instance.name}");

                // Queue for local processing on host/singleplayer
                if (ZNet.instance?.IsServer() == true)
                {
                    ConduitProcessor.QueueContainer(zdo.m_uid);
                }
            }
            else
            {
                Jotunn.Logger.LogWarning($"[ContainerPatches] Failed to compute OBB for {__instance.name}");
            }

            // Register for wireframe visualization
            ICGUIManager.Instance?.RegisterContainer(__instance);
        }
    }

    /// <summary>
    /// Patch Container.OnDestroyed to unregister from wireframe visualization.
    /// </summary>
    [HarmonyPatch(typeof(Container), nameof(Container.OnDestroyed))]
    public static class ContainerOnDestroyedPatch
    {
        [HarmonyPrefix]
        public static void Prefix(Container __instance)
        {
			Jotunn.Logger.LogDebug($"Container Destroyed, Unregistering from GUI manager");
            ICGUIManager.Instance?.UnregisterContainer(__instance);
        }
    }

    /// <summary>
    /// Patch Container.GetHoverText to append ItemConduit info.
    /// </summary>
    [HarmonyPatch(typeof(Container), nameof(Container.GetHoverText))]
    public static class ContainerGetHoverTextPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Container __instance, ref string __result)
        {
            var containerInterface = __instance.GetComponent<ContainerInterfaceComponent>();
            if (containerInterface == null) return;

            var extraText = containerInterface.GetHoverText();
            if (!string.IsNullOrEmpty(extraText))
            {
                __result = __result + "\n" + extraText;
            }
        }
    }
}
