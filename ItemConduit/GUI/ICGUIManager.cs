using System.Collections.Generic;
using ItemConduit.Components;
using UnityEngine;

namespace ItemConduit.GUI
{
    /// <summary>
    /// Handles conduit configuration panel and wireframe rendering.
    /// </summary>
    public class ICGUIManager : MonoBehaviour
    {
        public static ICGUIManager Instance { get; private set; }

        private bool _wireframeEnabled = false;
        private static Material _lineMaterial;
        private readonly Dictionary<Conduit, GameObject> _wireframeObjects = new();
        private readonly HashSet<Conduit> _registeredConduits = new();

        private void Awake()
        {
            Instance = this;
            CreateLineMaterial();
        }

        public bool IsWireframeEnabled() => _wireframeEnabled;
        public void SetWireframeEnabled(bool enabled)
        {
            _wireframeEnabled = enabled;
            Jotunn.Logger.LogInfo($"[ConduitGUIManager] Wireframe: {(enabled ? "ON" : "OFF")}");

            foreach (var obj in _wireframeObjects.Values)
            {
                if (obj != null) obj.SetActive(enabled);
            }
        }

        #region Conduit Registration

        /// <summary>
        /// Called by Conduit.Start() to register for wireframe visualization.
        /// </summary>
        public void RegisterConduit(Conduit conduit)
        {
            if (conduit == null) return;
            _registeredConduits.Add(conduit);
            CreateWireframe(conduit);
        }

        /// <summary>
        /// Called by Conduit.OnDestroy() to unregister.
        /// </summary>
        public void UnregisterConduit(Conduit conduit)
        {
            if (conduit == null) return;
            _registeredConduits.Remove(conduit);

            if (_wireframeObjects.TryGetValue(conduit, out var wireframeObj))
            {
                if (wireframeObj != null) Destroy(wireframeObj);
                _wireframeObjects.Remove(conduit);
            }
        }

        #endregion

        #region OBB Wireframe Visualization

        private void CreateWireframe(Conduit conduit)
        {
            if (_wireframeObjects.ContainsKey(conduit)) return;

            var collider = conduit.GetComponentInChildren<BoxCollider>();
            if (collider == null) return;

            var wireframeObj = new GameObject($"Wireframe_{conduit.name}");
            wireframeObj.transform.SetParent(conduit.transform, false);

            // OBB wireframe (green) - 12 edges
            var renderers = new LineRenderer[12];
            for (int i = 0; i < 12; i++)
            {
                var edgeObj = new GameObject($"OBB_Edge_{i}");
                edgeObj.transform.SetParent(wireframeObj.transform, false);

                var lr = edgeObj.AddComponent<LineRenderer>();
                lr.material = _lineMaterial;
                lr.startColor = Color.green;
                lr.endColor = Color.green;
                lr.startWidth = 0.02f;
                lr.endWidth = 0.02f;
                lr.positionCount = 2;
                lr.useWorldSpace = true;
                renderers[i] = lr;
            }

            _wireframeObjects[conduit] = wireframeObj;
            UpdateWireframePositions(collider, renderers);
            wireframeObj.SetActive(IsWireframeEnabled());
        }

        private void UpdateWireframePositions(BoxCollider collider, LineRenderer[] renderers)
        {
            var colliderTransform = collider.transform;
            var scaledSize = Vector3.Scale(collider.size, colliderTransform.lossyScale);
            var halfExtents = scaledSize / 2f;
            var center = colliderTransform.TransformPoint(collider.center);
            var rot = colliderTransform.rotation;

            Vector3[] corners = {
                center + rot * new Vector3(-halfExtents.x, -halfExtents.y, -halfExtents.z),
                center + rot * new Vector3(halfExtents.x, -halfExtents.y, -halfExtents.z),
                center + rot * new Vector3(halfExtents.x, -halfExtents.y, halfExtents.z),
                center + rot * new Vector3(-halfExtents.x, -halfExtents.y, halfExtents.z),
                center + rot * new Vector3(-halfExtents.x, halfExtents.y, -halfExtents.z),
                center + rot * new Vector3(halfExtents.x, halfExtents.y, -halfExtents.z),
                center + rot * new Vector3(halfExtents.x, halfExtents.y, halfExtents.z),
                center + rot * new Vector3(-halfExtents.x, halfExtents.y, halfExtents.z)
            };

            int[,] edges = {
                {0,1}, {1,2}, {2,3}, {3,0},
                {4,5}, {5,6}, {6,7}, {7,4},
                {0,4}, {1,5}, {2,6}, {3,7}
            };

            for (int i = 0; i < 12; i++)
            {
                renderers[i].SetPosition(0, corners[edges[i,0]]);
                renderers[i].SetPosition(1, corners[edges[i,1]]);
            }
        }

        private static void CreateLineMaterial()
        {
            if (_lineMaterial != null) return;

            var shader = Shader.Find("Sprites/Default");
            _lineMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        #endregion
    }
}
