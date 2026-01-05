using HarmonyLib;
using ItemConduit.Collision;
using ItemConduit.Components;
using ItemConduit.Core;
using ItemConduit.Utils;
using UnityEngine;

namespace ItemConduit.Patches
{
    /// <summary>
    /// Patch Piece placement to detect connections on conduit placement.
    /// </summary>
    [HarmonyPatch(typeof(Piece), nameof(Piece.SetCreator))]
    public static class PiecePlacedPatches
    {
        [HarmonyPostfix]
        public static void Postfix(Piece __instance)
        {
            // Check if this piece has a Conduit component
            var conduit = __instance.GetComponent<Conduit>();
            if (conduit == null) return;

            // Only process on server
            if (!ZNet.instance.IsServer()) return;

            var nview = __instance.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid()) return;

            var zdo = nview.GetZDO();
            if (zdo == null) return;

            // Initialize OBB bounds based on piece size
            var bounds = GetConduitBounds(__instance);
            zdo.Set(ZDOFields.IC_Bound, bounds.Serialize());

            // Call network builder to detect connections
            NetworkBuilder.OnConduitPlaced(zdo);
        }

        private static OrientedBoundingBox GetConduitBounds(Piece piece)
        {
            var position = piece.transform.position;
            var rotation = piece.transform.rotation;

            // Determine size based on prefab name
            var prefabName = Utils.Utils.GetPrefabName(piece.gameObject);
            Vector3 halfExtents;

            if (prefabName.Contains("beam1"))
                halfExtents = new Vector3(1f, 0.1f, 0.1f); // 2m beam
            else if (prefabName.Contains("beam2"))
                halfExtents = new Vector3(2f, 0.1f, 0.1f); // 4m beam
            else if (prefabName.Contains("pole2"))
                halfExtents = new Vector3(0.1f, 1f, 0.1f); // 2m pole
            else if (prefabName.Contains("pole"))
                halfExtents = new Vector3(0.1f, 0.5f, 0.1f); // 1m pole
            else
                halfExtents = new Vector3(1f, 0.1f, 0.1f); // Default

            return new OrientedBoundingBox(position, rotation, halfExtents);
        }
    }

    /// <summary>
    /// Patch WearNTear destruction to handle conduit removal.
    /// </summary>
    [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Destroy))]
    public static class WearNTearDestroyPatches
    {
        [HarmonyPrefix]
        public static void Prefix(WearNTear __instance)
        {
            // Check if this piece has a Conduit component
            var conduit = __instance.GetComponent<Conduit>();
            if (conduit == null) return;

            // Only process on server
            if (!ZNet.instance.IsServer()) return;

            var nview = __instance.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid()) return;

            var zdo = nview.GetZDO();
            if (zdo == null) return;

            // Call network builder to handle removal
            NetworkBuilder.OnConduitRemoved(zdo.m_uid);
        }
    }

    /// <summary>
    /// Alternative patch for Piece destruction (since we remove WearNTear).
    /// </summary>
    [HarmonyPatch(typeof(Piece), nameof(Piece.OnDestroy))]
    public static class PieceDestroyPatches
    {
        [HarmonyPrefix]
        public static void Prefix(Piece __instance)
        {
            // Check if this piece has a Conduit component
            var conduit = __instance.GetComponent<Conduit>();
            if (conduit == null) return;

            // Only process on server
            if (!ZNet.instance?.IsServer() == true) return;

            var nview = __instance.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid()) return;

            var zdo = nview.GetZDO();
            if (zdo == null) return;

            // Call network builder to handle removal
            NetworkBuilder.OnConduitRemoved(zdo.m_uid);
        }
    }
}
