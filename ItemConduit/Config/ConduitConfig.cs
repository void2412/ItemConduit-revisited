using BepInEx.Configuration;

namespace ItemConduit.Config
{
    public class ConduitConfig
    {
        public ConfigEntry<int> TransferRate { get; private set; }
        public ConfigEntry<float> TransferTick { get; private set; }
        public ConfigEntry<bool> WireframeDefault { get; private set; }
        public ConfigEntry<bool> ShowDebug { get; private set; }
        public ConfigEntry<float> ConnectionTolerance { get; private set; }

        public ConduitConfig(ConfigFile config)
        {
            TransferRate = config.Bind(
                "Transfer",
                "TransferRate",
                1,
                new ConfigDescription(
                    "Maximum items transferred per tick per extractor",
                    new AcceptableValueRange<int>(1, 100)
                )
            );

            TransferTick = config.Bind(
                "Transfer",
                "TransferTick",
                1.0f,
                new ConfigDescription(
                    "Seconds between transfer ticks",
                    new AcceptableValueRange<float>(0.1f, 10.0f)
                )
            );

            WireframeDefault = config.Bind(
                "Visualization",
                "WireframeDefault",
                false,
                "Enable wireframe visualization by default"
            );

            ShowDebug = config.Bind(
                "Debug",
                "ShowDebug",
                false,
                "Show debug info (connection ZDOIDs) in hover text"
            );

            ConnectionTolerance = config.Bind(
                "Connection",
                "ConnectionTolerance",
                0.02f,
                new ConfigDescription(
                    "Tolerance added to conduit bounds for end-to-end collision detection (in meters)",
                    new AcceptableValueRange<float>(0.01f, 0.2f)
                )
            );

            // Apply config changes
            TransferTick.SettingChanged += (_, _) =>
            {
                Transfer.TransferManager.Instance.SetTickInterval(TransferTick.Value);
            };
        }
    }
}
