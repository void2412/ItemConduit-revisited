using System.Collections.Generic;
using HarmonyLib;
using ItemConduit.Components;
using ItemConduit.Core;

namespace ItemConduit.Patches
{
    /// <summary>
    /// Patch ZDOMan.RPC_ZDOData to detect conduit and inventory source ZDOs during network sync.
    /// Postfix processes after ZDO.Deserialize so prefab hash is available.
    /// </summary>
    [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.RPC_ZDOData))]
    public static class ZDOManPatches
    {
        private static HashSet<ZDOID> _conduitZDOIDs = new HashSet<ZDOID>();
        private static HashSet<ZDOID> _inventorySourceZDOIDs = new HashSet<ZDOID>();

        private static int originalPos;

        [HarmonyPrefix]
        public static void Prefix(ZDOMan __instance, ZRpc rpc, ZPackage pkg)
        {
            

            if (pkg == null || pkg.Size() == 0) return;

            originalPos = pkg.GetPos();

            
        }

        [HarmonyPostfix]
        public static void Postfix(ZDOMan __instance, ZRpc rpc, ZPackage pkg)
        {
            if (pkg == null || pkg.Size() == 0) return;
            try
            {
                _conduitZDOIDs.Clear();
                _inventorySourceZDOIDs.Clear();
                pkg.SetPos(originalPos);
                int num2 = pkg.ReadInt();
                for (int i = 0; i < num2; i++)
                {
                    ZDOID id = pkg.ReadZDOID();
                    ZDO zdo = __instance.GetZDO(id);
                    if (zdo != null)
                    {
                        zdo.InvalidateSector();
                    }
                }
                ZPackage pkg2 = new ZPackage();
                for (;;)
                {
                    ZDOID zdoid = pkg.ReadZDOID();
                    if (zdoid.IsNone()) break;
                    ushort ownerRevisionZdo = pkg.ReadUShort();
                    uint dataRevisionZdo = pkg.ReadUInt();
                    long ownerInternal = pkg.ReadLong();
                    UnityEngine.Vector3 vector = pkg.ReadVector3();
                    pkg.ReadPackage(ref pkg2);
                    ZDO zdo2 = __instance.GetZDO(zdoid);

                    if (zdo2 == null) continue;

                    int prefabHash = zdo2.GetPrefab();

                    // Check if conduit
                    if (ConduitPrefabs.ConduitPrefabHashes.Contains(prefabHash))
                    {
                        Jotunn.Logger.LogDebug($"[ZDOManPatches] Found conduit {zdoid} from Zpackage");
                        _conduitZDOIDs.Add(zdoid);
                    }
                    // Check if inventory source (Container, Fireplace, Smelter, etc)
                    else if (InventorySourceRegistry.IsInventorySource(prefabHash))
                    {
                        Jotunn.Logger.LogDebug($"[ZDOManPatches] Found inventory source {zdoid} from Zpackage");
                        _inventorySourceZDOIDs.Add(zdoid);
                    }
                }

                if (!ZNet.instance.IsServer()) return;

                // Queue conduits for server-side processing
                if (_conduitZDOIDs.Count > 0)
                {
                    Jotunn.Logger.LogDebug($"[ZDOManPatches] Queueing {_conduitZDOIDs.Count} conduits");
                    foreach (ZDOID id in _conduitZDOIDs)
                    {
                        if (__instance.m_deadZDOs.ContainsKey(id)) continue;
                        ConduitProcessor.QueueConduit(id);
                    }
                }

                // Queue inventory sources for server-side conduit detection
                if (_inventorySourceZDOIDs.Count > 0)
                {
                    Jotunn.Logger.LogDebug($"[ZDOManPatches] Queueing {_inventorySourceZDOIDs.Count} inventory sources");
                    foreach (ZDOID id in _inventorySourceZDOIDs)
                    {
                        if (__instance.m_deadZDOs.ContainsKey(id)) continue;
                        ConduitProcessor.QueueContainer(id);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Jotunn.Logger.LogWarning($"[ZDOManPatches] Postfix parse error: {ex.Message}");
                _conduitZDOIDs.Clear();
                _inventorySourceZDOIDs.Clear();
            }
        }
    }

    /// <summary>
    /// Patch HandleDestroyedZDO to detect conduit and inventory source removal on dedicated server.
    /// ZDO still exists in prefix, so we can check prefab hash and cache data.
    /// </summary>
    [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.HandleDestroyedZDO))]
    public static class HandleDestroyedZDOPatch
    {
        [HarmonyPrefix]
        public static void Prefix(ZDOMan __instance, ZDOID uid)
        {
            if (!ZNet.instance?.IsServer() ?? true) return;

            var zdo = __instance.GetZDO(uid);
            if (zdo == null) return;

            var prefabHash = zdo.GetPrefab();

            // Check if conduit
            if (ConduitPrefabs.ConduitPrefabHashes.Contains(prefabHash))
            {
                Jotunn.Logger.LogDebug($"[HandleDestroyedZDO] Conduit {uid} destroyed, caching data for removal");
                // Pass ZDO so data can be cached before destruction
                ConduitProcessor.QueueConduitRemoval(zdo);
            }
            // Check if inventory source (Container, Fireplace, Smelter, etc)
            else if (InventorySourceRegistry.IsInventorySource(prefabHash))
            {
                // Cache connected conduits list before ZDO destroyed
                var connectedConduits = NetworkBuilder.GetContainerConduitList(zdo);
                Jotunn.Logger.LogDebug($"[HandleDestroyedZDO] Inventory source {uid} destroyed, {connectedConduits.Count} linked conduits");
                ConduitProcessor.QueueContainerRemoval(uid, connectedConduits);
            }
        }
    }
}
