using BepInEx.Configuration;

namespace ItemConduit.Config
{
    public class ConduitConfig
    {
        public ConfigEntry<int> TransferRate { get; private set; }
        public ConfigEntry<float> TransferTick { get; private set; }
        public ConfigEntry<bool> WireframeDefault { get; private set; }

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

            // Apply config changes
            TransferTick.SettingChanged += (_, _) =>
            {
                Transfer.TransferManager.Instance.SetTickInterval(TransferTick.Value);
            };
        }
    }
}
