using System.Collections.Generic;
using HarmonyLib;
using ItemConduit.Components;
using ItemConduit.Core;
using BepInEx;

namespace ItemConduit.Patches
{
    /// <summary>
    /// Patch ZDOMan.RPC_ZDOData to detect conduit ZDOs during network sync.
    /// Prefix filters by prefabHash, Postfix processes only conduit ZDOs.
    /// </summary>
    [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.RPC_ZDOData))]
    public static class ZDOManPatches
    {
        // Only conduit ZDOIDs (pre-filtered by prefabHash)
        private static List<ZDOID> _conduitZDOIDs = new List<ZDOID>();

        // Prefab key in ints collection
        private static readonly int PrefabHashKey = "prefab".GetStableHashCode();
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
                    if(zdoid.IsNone()) break;
                    ushort ownerRevisionZdo = pkg.ReadUShort();
                    uint dataRevisionZdo = pkg.ReadUInt();
                    long ownerInternal = pkg.ReadLong();
                    UnityEngine.Vector3 vector = pkg.ReadVector3();
                    pkg.ReadPackage(ref pkg2);
                    ZDO zdo2 = __instance.GetZDO(zdoid);

                    if (zdo2!=null && ConduitPrefabs.ConduitPrefabHashes.Contains(zdo2.GetPrefab()))
                    {
                        Jotunn.Logger.LogDebug($"[ZDOManPatches] Prefix: Found {zdoid} as {zdo2.GetPrefab()}");
                        _conduitZDOIDs.Add(zdoid);
                    }
                }

                // Queue conduits for server-side processing
                if (_conduitZDOIDs.Count > 0 && ZNet.instance.IsServer())
                {
                    Jotunn.Logger.LogDebug($"[ZDOManPatches] Queueing {_conduitZDOIDs.Count} conduits for processing");
                    foreach (ZDOID id in _conduitZDOIDs)
                    {
                        if (__instance.m_deadZDOs.ContainsKey(id))
                        {
                            Jotunn.Logger.LogDebug($"[ZDOManPatches] ZDO already removed");
                            continue;
                        }
                        ConduitProcessor.QueueConduit(id);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Jotunn.Logger.LogWarning($"[ZDOManPatches] Prefix parse error: {ex.Message}");
                _conduitZDOIDs.Clear();
            }
            finally
            {
            }
        }
    }

    /// <summary>
    /// Patch HandleDestroyedZDO to detect conduit removal on dedicated server.
    /// ZDO still exists in prefix, so we can check prefab hash.
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

            if (ConduitPrefabs.ConduitPrefabHashes.Contains(zdo.GetPrefab()))
            {
                Jotunn.Logger.LogDebug($"[HandleDestroyedZDO] Conduit {uid} destroyed, caching data for removal");
                // Pass ZDO so data can be cached before destruction
                ConduitProcessor.QueueConduitRemoval(zdo);
            }
        }
    }
}
