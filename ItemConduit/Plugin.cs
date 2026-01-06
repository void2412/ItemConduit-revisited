using BepInEx;
using HarmonyLib;
using ItemConduit.Commands;
using ItemConduit.Components;
using ItemConduit.Config;
using ItemConduit.Core;
using ItemConduit.GUI;
using ItemConduit.Transfer;
using Jotunn.Utils;
using UnityEngine;

namespace ItemConduit
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.void.itemconduit";
        public const string PluginName = "ItemConduit";
        public const string PluginVersion = "1.0.0";

        public static Plugin Instance { get; private set; }
        public ConduitConfig ConduitConfig { get; private set; }

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;

            // Initialize configuration
            ConduitConfig = new ConduitConfig(Config);

            // Apply Harmony patches
            _harmony = new Harmony(PluginGUID);
            _harmony.PatchAll();

            // Register conduit prefabs
            ConduitPrefabs.RegisterPrefabs();

            // Register console commands
            ConduitCommands.RegisterCommands();

            // Initialize GUI Manager
            var guiManager = new GameObject("ConduitGUIManager");
            guiManager.AddComponent<ICGUIManager>();
            DontDestroyOnLoad(guiManager);

            Logger.LogInfo($"{PluginName} v{PluginVersion} loaded");
        }

        private void Update()
        {
            TransferManager.Instance.Update();
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
