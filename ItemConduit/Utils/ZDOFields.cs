namespace ItemConduit.Utils
{
    public static class ZDOFields
    {
        // String keys for ZDOID operations (uses string API)
        public const string IC_ContainerZDOID = "IC_ContainerZDOID";

        // Hash codes for primitive operations (uses int API)
        public static readonly int IC_Mode = "IC_Mode".GetStableHashCode();
        public static readonly int IC_ConnectionList = "IC_ConnectionList".GetStableHashCode();
        public static readonly int IC_NetworkID = "IC_NetworkID".GetStableHashCode();
        public static readonly int IC_Channel = "IC_Channel".GetStableHashCode();
        public static readonly int IC_Priority = "IC_Priority".GetStableHashCode();
        public static readonly int IC_FilterList = "IC_FilterList".GetStableHashCode();
        public static readonly int IC_FilterMode = "IC_FilterMode".GetStableHashCode();
        public static readonly int IC_Bound = "IC_Bound".GetStableHashCode();
        public static readonly int IC_TransferRate = "IC_TransferRate".GetStableHashCode();
    }

    public enum ConduitMode
    {
        Conduit = 0,
        Extract = 1,
        Insert = 2
    }

    public enum FilterMode
    {
        Whitelist = 0,
        Blacklist = 1
    }
}
