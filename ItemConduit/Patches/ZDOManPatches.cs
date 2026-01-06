using System.Collections.Generic;
using System.Numerics;
using HarmonyLib;
using ItemConduit.Components;
using ItemConduit.Core;
using ItemConduit.Utils;

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

        /// <summary>
        /// Read prefab hash from ints collection and skip rest of ZDO data.
        /// Returns prefab hash or 0 if not found.
        /// </summary>
        private static int ReadPrefabAndSkipZDOData(ZPackage pkg)
        {
            int prefabHash = 0;

            // Skip rotation (3 floats)
            pkg.ReadSingle();
            pkg.ReadSingle();
            pkg.ReadSingle();

            byte typeMask = pkg.ReadByte();

            // Floats
            if ((typeMask & 1) != 0)
            {
                byte count = pkg.ReadByte();
                for (int i = 0; i < count; i++)
                {
                    pkg.ReadInt();
                    pkg.ReadSingle();
                }
            }

            // Vec3s
            if ((typeMask & 2) != 0)
            {
                byte count = pkg.ReadByte();
                for (int i = 0; i < count; i++)
                {
                    pkg.ReadInt();
                    pkg.ReadVector3();
                }
            }

            // Quats
            if ((typeMask & 4) != 0)
            {
                byte count = pkg.ReadByte();
                for (int i = 0; i < count; i++)
                {
                    pkg.ReadInt();
                    pkg.ReadQuaternion();
                }
            }

            // Ints - check for prefab hash here
            if ((typeMask & 8) != 0)
            {
                byte count = pkg.ReadByte();
                for (int i = 0; i < count; i++)
                {
                    int key = pkg.ReadInt();
                    int value = pkg.ReadInt();

                    // Prefab is stored with key = hash of "prefab"
                    if (key == PrefabHashKey)
                    {
                        prefabHash = value;
                    }
                }
            }

            // Longs
            if ((typeMask & 16) != 0)
            {
                byte count = pkg.ReadByte();
                for (int i = 0; i < count; i++)
                {
                    pkg.ReadInt();
                    pkg.ReadLong();
                }
            }

            // Strings
            if ((typeMask & 32) != 0)
            {
                byte count = pkg.ReadByte();
                for (int i = 0; i < count; i++)
                {
                    pkg.ReadInt();
                    pkg.ReadString();
                }
            }

            // ByteArrays
            if ((typeMask & 64) != 0)
            {
                byte count = pkg.ReadByte();
                for (int i = 0; i < count; i++)
                {
                    pkg.ReadInt();
                    pkg.ReadByteArray();
                }
            }

            // ZDOIDs
            if ((typeMask & 128) != 0)
            {
                byte count = pkg.ReadByte();
                for (int i = 0; i < count; i++)
                {
                    pkg.ReadString();
                    pkg.ReadZDOID();
                }
            }

            return prefabHash;
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
                List<ZDO> zdoList = new List<ZDO>();
                for (;;)
                {
                    ZDOID zdoid = pkg.ReadZDOID();
                    if(zdoid.IsNone()) break;
                    zdoList.Add(__instance.GetZDO(zdoid));
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
