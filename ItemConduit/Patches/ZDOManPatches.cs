using System.Collections.Generic;
using System.Numerics;
using HarmonyLib;
using ItemConduit.Components;
using ItemConduit.Core;
using ItemConduit.Utils;
using ItemConduit.Collision;
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
            _conduitZDOIDs.Clear();

            if (pkg == null || pkg.Size() == 0) return;

            originalPos = pkg.GetPos();

            
        }

        [HarmonyPostfix]
        public static void Postfix(ZDOMan __instance, ZRpc rpc, ZPackage pkg)
        {
            try
            {
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


                if (_conduitZDOIDs.Count > 0)
                {
                    Jotunn.Logger.LogDebug($"[ZDOManPatches] Prefix complete: {_conduitZDOIDs.Count} conduits found");
                    foreach (ZDOID id in _conduitZDOIDs)
                    {
                        Jotunn.Logger.LogDebug($"Received ZDOID: {id}");

                        
                        if (ZNet.instance.IsServer())
                        {
                            // Process Conduit node here.
                            ZDO conduitZDO = __instance.GetZDO(id);
                            conduitZDO.Set(ZDOFields.IC_NetworkID, "12345678");
                            Jotunn.Logger.LogDebug($"Network ID editted on server");
                            var obb = conduitZDO.GetString(ZDOFields.IC_Bound, "");

                            if (!obb.IsNullOrWhiteSpace()) Jotunn.Logger.LogDebug($"OBB: {obb}");
                        }

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
}
