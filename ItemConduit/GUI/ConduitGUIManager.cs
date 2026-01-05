using UnityEngine;

namespace ItemConduit.GUI
{
    /// <summary>
    /// Placeholder for Phase 6: GUI and Visualization.
    /// Will handle conduit configuration panel and wireframe rendering.
    /// </summary>
    public class ConduitGUIManager : MonoBehaviour
    {
        public static ConduitGUIManager Instance { get; private set; }

        private bool _wireframeEnabled = false;

        private void Awake()
        {
            Instance = this;
        }

        public bool IsWireframeEnabled() => _wireframeEnabled;
        public void SetWireframeEnabled(bool enabled) => _wireframeEnabled = enabled;
    }
}
