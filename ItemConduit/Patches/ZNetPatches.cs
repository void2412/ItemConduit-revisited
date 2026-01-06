using HarmonyLib;
using ItemConduit.GUI;
using UnityEngine;

namespace ItemConduit.Patches
{
    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Awake))]
    public static class ZNetAwakePatch
    {
        static void Postfix(ZNet __instance)
        {
            // Only create GUI manager on clients
            if (__instance.IsDedicated()) return;

            if (ICGUIManager.Instance != null) return; // Already created

            var guiManager = new GameObject("ICGUIManager");
            guiManager.AddComponent<ICGUIManager>();
            Object.DontDestroyOnLoad(guiManager);

            Jotunn.Logger.LogInfo("[ICGUIManager] Initialized for client");
        }
    }
}
