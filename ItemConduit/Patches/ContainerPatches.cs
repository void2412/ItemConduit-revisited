using HarmonyLib;
using ItemConduit.Collision;
using ItemConduit.Components;
using ItemConduit.GUI;
using ItemConduit.Utils;
using UnityEngine;

namespace ItemConduit.Patches
{
    /// <summary>
    /// Patch Container to compute and store OBB in ZDO on client.
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

            // Check if OBB already stored
            var existingBound = zdo.GetString(ZDOFields.IC_Bound, "");
            if (!string.IsNullOrEmpty(existingBound))
            {
                // Already stored, just register for wireframe
                ICGUIManager.Instance?.RegisterContainer(__instance);
                return;
            }

            // Compute OBB from MeshCollider bounds (preferred) or MeshFilter
            Bounds? localBounds = null;

            var meshCollider = __instance.GetComponentInChildren<MeshCollider>();
            if (meshCollider != null && meshCollider.sharedMesh != null)
            {
				Jotunn.Logger.LogDebug($"[ContainerPatches] Mesh Collider found");
                localBounds = meshCollider.sharedMesh.bounds;
            }
            else
            {
                var meshFilter = __instance.GetComponentInChildren<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
					Jotunn.Logger.LogDebug($"[ContainerPatches] Mesh Filter found");
					localBounds = meshFilter.sharedMesh.bounds;
                }
            }

            if (localBounds.HasValue)
            {
                var obb = OrientedBoundingBox.FromBounds(
                    localBounds.Value,
                    __instance.transform.position,
                    __instance.transform.rotation
                );
                zdo.Set(ZDOFields.IC_Bound, obb.Serialize());
                Jotunn.Logger.LogDebug($"[ContainerPatches] Stored OBB for {__instance.name}");

                // Register for wireframe visualization
                ICGUIManager.Instance?.RegisterContainer(__instance);
            }
            else
            {
                Jotunn.Logger.LogWarning($"[ContainerPatches] No MeshCollider or MeshFilter for {__instance.name}");
            }
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
}
