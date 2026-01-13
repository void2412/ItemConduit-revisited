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
        private readonly Dictionary<Conduit, GameObject> _conduitWireframes = new();
        private readonly Dictionary<IContainerInterface, GameObject> _containerWireframes = new();
        private readonly HashSet<Conduit> _registeredConduits = new();
        private readonly HashSet<IContainerInterface> _registeredContainers = new();

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

            foreach (var obj in _conduitWireframes.Values)
            {
                if (obj != null) obj.SetActive(enabled);
            }

            foreach (var obj in _containerWireframes.Values)
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

            if (_conduitWireframes.TryGetValue(conduit, out var wireframeObj))
            {
                if (wireframeObj != null) Destroy(wireframeObj);
                _conduitWireframes.Remove(conduit);
            }
        }

        #endregion

        #region Container Registration

        /// <summary>
        /// Called by ContainerPatches after OBB is stored to register for wireframe visualization.
        /// </summary>
        public void RegisterContainer(IContainerInterface container)
        {
            if (container == null) return;
            _registeredContainers.Add(container);
            CreateContainerWireframe(container);
        }

        /// <summary>
        /// Called when container is destroyed to unregister.
        /// </summary>
        public void UnregisterContainer(IContainerInterface container)
        {
            if (container == null) return;
            _registeredContainers.Remove(container);

            if (_containerWireframes.TryGetValue(container, out var wireframeObj))
            {
                if (wireframeObj != null) Destroy(wireframeObj);
                _containerWireframes.Remove(container);
            }
        }

        private void CreateContainerWireframe(IContainerInterface container)
        {
            if (_containerWireframes.ContainsKey(container)) return;

            if (!container.IsValid) return;

            var zdo = container.Zdo;
            if (zdo == null) return;

            var obbStr = zdo.GetString(Utils.ZDOFields.IC_Bound, "");
            if (string.IsNullOrEmpty(obbStr)) return;

            var obb = Collision.OrientedBoundingBox.Deserialize(obbStr);
            if (obb.HalfExtents == Vector3.zero) return;

            var wireframeObj = new GameObject($"ContainerWireframe_{container.Name}");
            wireframeObj.transform.SetParent(container.Transform, false);

            // Container OBB wireframe (cyan) - 12 edges
            var renderers = CreateEdgeRenderers(wireframeObj.transform, "Edge", Color.cyan, 0.02f);

            UpdateWireframePositions(obb, renderers, null, null);
            _containerWireframes[container] = wireframeObj;
            wireframeObj.SetActive(IsWireframeEnabled());
        }

        #endregion

        #region Conduit Wireframe

        private void CreateWireframe(Conduit conduit)
        {
            if (_conduitWireframes.ContainsKey(conduit)) return;

            var collider = conduit.GetComponentInChildren<BoxCollider>();
            if (collider == null) return;

            var wireframeObj = new GameObject($"Wireframe_{conduit.name}");
            wireframeObj.transform.SetParent(conduit.transform, false);

            // OBB wireframe (green) - 12 edges
            var obbRenderers = CreateEdgeRenderers(wireframeObj.transform, "OBB_Edge", Color.green, 0.02f);

            // Tolerance wireframe (red) - 12 edges
            var toleranceRenderers = CreateEdgeRenderers(wireframeObj.transform, "Tolerance_Edge", Color.red, 0.015f);

            // Compute OBBs from BoxCollider
            var colliderTransform = collider.transform;
            var scaledSize = Vector3.Scale(collider.size, colliderTransform.lossyScale);
            var halfExtents = scaledSize / 2f;
            var center = colliderTransform.TransformPoint(collider.center);
            var rot = colliderTransform.rotation;

            var obb = new Collision.OrientedBoundingBox(center, rot, halfExtents);

            // Calculate tolerance-extended OBB
            var tolerance = Plugin.Instance.ConduitConfig.ConnectionTolerance.Value;
            var toleranceHalfExtents = halfExtents;
            if (toleranceHalfExtents.x >= toleranceHalfExtents.y && toleranceHalfExtents.x >= toleranceHalfExtents.z)
                toleranceHalfExtents.x += tolerance;
            else if (toleranceHalfExtents.y >= toleranceHalfExtents.x && toleranceHalfExtents.y >= toleranceHalfExtents.z)
                toleranceHalfExtents.y += tolerance;
            else
                toleranceHalfExtents.z += tolerance;

            var toleranceObb = new Collision.OrientedBoundingBox(center, rot, toleranceHalfExtents);

            _conduitWireframes[conduit] = wireframeObj;
            UpdateWireframePositions(obb, obbRenderers, toleranceObb, toleranceRenderers);
            wireframeObj.SetActive(IsWireframeEnabled());
        }

        #endregion

        #region Wireframe Helpers

        private LineRenderer[] CreateEdgeRenderers(Transform parent, string prefix, Color color, float width)
        {
            var renderers = new LineRenderer[12];
            for (int i = 0; i < 12; i++)
            {
                var edgeObj = new GameObject($"{prefix}_{i}");
                edgeObj.transform.SetParent(parent, false);

                var lr = edgeObj.AddComponent<LineRenderer>();
                lr.material = _lineMaterial;
                lr.startColor = color;
                lr.endColor = color;
                lr.startWidth = width;
                lr.endWidth = width;
                lr.positionCount = 2;
                lr.useWorldSpace = true;
                renderers[i] = lr;
            }
            return renderers;
        }

        private static readonly int[,] EdgeIndices = {
            {0,1}, {1,2}, {2,3}, {3,0},
            {4,5}, {5,6}, {6,7}, {7,4},
            {0,4}, {1,5}, {2,6}, {3,7}
        };

        private void UpdateWireframePositions(
            Collision.OrientedBoundingBox obb,
            LineRenderer[] obbRenderers,
            Collision.OrientedBoundingBox? toleranceObb,
            LineRenderer[] toleranceRenderers)
        {
            // OBB corners
            var he = obb.HalfExtents;
            Vector3[] corners = {
                obb.Center + obb.Rotation * new Vector3(-he.x, -he.y, -he.z),
                obb.Center + obb.Rotation * new Vector3(he.x, -he.y, -he.z),
                obb.Center + obb.Rotation * new Vector3(he.x, -he.y, he.z),
                obb.Center + obb.Rotation * new Vector3(-he.x, -he.y, he.z),
                obb.Center + obb.Rotation * new Vector3(-he.x, he.y, -he.z),
                obb.Center + obb.Rotation * new Vector3(he.x, he.y, -he.z),
                obb.Center + obb.Rotation * new Vector3(he.x, he.y, he.z),
                obb.Center + obb.Rotation * new Vector3(-he.x, he.y, he.z)
            };

            for (int i = 0; i < 12; i++)
            {
                obbRenderers[i].SetPosition(0, corners[EdgeIndices[i,0]]);
                obbRenderers[i].SetPosition(1, corners[EdgeIndices[i,1]]);
            }

            // Tolerance wireframe (optional)
            if (toleranceObb.HasValue && toleranceRenderers != null && toleranceRenderers.Length == 12)
            {
                var the = toleranceObb.Value.HalfExtents;
                Vector3[] toleranceCorners = {
                    toleranceObb.Value.Center + toleranceObb.Value.Rotation * new Vector3(-the.x, -the.y, -the.z),
                    toleranceObb.Value.Center + toleranceObb.Value.Rotation * new Vector3(the.x, -the.y, -the.z),
                    toleranceObb.Value.Center + toleranceObb.Value.Rotation * new Vector3(the.x, -the.y, the.z),
                    toleranceObb.Value.Center + toleranceObb.Value.Rotation * new Vector3(-the.x, -the.y, the.z),
                    toleranceObb.Value.Center + toleranceObb.Value.Rotation * new Vector3(-the.x, the.y, -the.z),
                    toleranceObb.Value.Center + toleranceObb.Value.Rotation * new Vector3(the.x, the.y, -the.z),
                    toleranceObb.Value.Center + toleranceObb.Value.Rotation * new Vector3(the.x, the.y, the.z),
                    toleranceObb.Value.Center + toleranceObb.Value.Rotation * new Vector3(-the.x, the.y, the.z)
                };

                for (int i = 0; i < 12; i++)
                {
                    toleranceRenderers[i].SetPosition(0, toleranceCorners[EdgeIndices[i,0]]);
                    toleranceRenderers[i].SetPosition(1, toleranceCorners[EdgeIndices[i,1]]);
                }
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
