using UnityEngine;

namespace ItemConduit.Utils
{
    public static class Utils
    {
        /// <summary>
        /// Get the prefab name from a GameObject, stripping clone suffix.
        /// </summary>
        public static string GetPrefabName(GameObject obj)
        {
            if (obj == null) return string.Empty;

            var name = obj.name;

            // Remove (Clone) suffix
            if (name.EndsWith("(Clone)"))
            {
                name = name.Substring(0, name.Length - 7);
            }

            return name.Trim();
        }
    }
}
