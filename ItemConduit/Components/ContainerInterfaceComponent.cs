using System.Text;
using ItemConduit.Collision;
using ItemConduit.Core;
using ItemConduit.Utils;
using UnityEngine;

namespace ItemConduit.Components
{
    /// <summary>
    /// MonoBehaviour that implements IContainerInterface for vanilla Container.
    /// Added to Container GameObjects via ContainerPatches.
    /// </summary>
    public class ContainerInterfaceComponent : MonoBehaviour, IContainerInterface, Hoverable
    {
        private Container _container;
        private ZNetView _nview;

        public ContainerType Type => ContainerType.Container;

        public ZDOID Zdoid => _nview?.GetZDO()?.m_uid ?? ZDOID.None;

        public ZDO Zdo => _nview?.GetZDO();

        public Inventory Inventory => _container?.GetInventory();

        public bool IsValid => _nview != null && _nview.IsValid() && _container != null;

        public int PrefabHash => Zdo?.GetPrefab() ?? 0;

        private void Awake()
        {
            _container = GetComponent<Container>();
            _nview = GetComponent<ZNetView>();
        }

        public OrientedBoundingBox? ComputeBounds()
        {
            if (!IsValid) return null;

            // Try MeshCollider first (preferred for accurate bounds)
            var meshCollider = GetComponentInChildren<MeshCollider>();
            if (meshCollider != null && meshCollider.sharedMesh != null)
            {
                return OrientedBoundingBox.FromBounds(
                    meshCollider.sharedMesh.bounds,
                    transform.position,
                    transform.rotation
                );
            }

            // Fallback to MeshFilter
            var meshFilter = GetComponentInChildren<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                return OrientedBoundingBox.FromBounds(
                    meshFilter.sharedMesh.bounds,
                    transform.position,
                    transform.rotation
                );
            }

            // Last fallback: BoxCollider
            var boxCollider = GetComponentInChildren<BoxCollider>();
            if (boxCollider != null)
            {
                var bounds = new Bounds(boxCollider.center, boxCollider.size);
                return OrientedBoundingBox.FromBounds(
                    bounds,
                    transform.position,
                    transform.rotation
                );
            }

            return null;
        }

        /// <summary>
        /// Store OBB to ZDO if not already stored.
        /// Sets IC_IsNew flag for new containers.
        /// </summary>
        public bool TryStoreOBB()
        {
            var zdo = Zdo;
            if (zdo == null) return false;

            // Check if already stored
            var existingBound = zdo.GetString(ZDOFields.IC_Bound, "");
            if (!string.IsNullOrEmpty(existingBound)) return true;

            var obb = ComputeBounds();
            if (!obb.HasValue) return false;

            zdo.Set(ZDOFields.IC_Bound, obb.Value.Serialize());
            zdo.Set(ZDOFields.IC_IsNew, true);
            return true;
        }

        #region Hoverable

        public string GetHoverText()
        {
            if (_nview == null || !_nview.IsValid()) return "";

            var zdo = _nview.GetZDO();
            if (zdo == null) return "";

            var sb = new StringBuilder();
            sb.AppendLine($"<color=#00FFFF>ZDOID: {zdo.m_uid}</color>");

            // Show connected conduits list
            var connectedConduits = NetworkBuilder.GetContainerConduitList(zdo);
            if (connectedConduits.Count > 0)
            {
                sb.AppendLine($"<color=yellow>Connected Conduits ({connectedConduits.Count}):</color>");
                foreach (var conduitId in connectedConduits)
                {
                    sb.AppendLine($"  <color=green>{conduitId}</color>");
                }
            }
            else
            {
                sb.AppendLine("<color=red>No connected conduits</color>");
            }

            return sb.ToString();
        }

        public string GetHoverName()
        {
            return _container != null ? _container.m_name : "Container";
        }

        #endregion
    }
}
