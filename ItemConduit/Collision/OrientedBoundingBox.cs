using System;
using UnityEngine;

namespace ItemConduit.Collision
{
    [Serializable]
    public struct OrientedBoundingBox
    {
        public Vector3 Center;
        public Quaternion Rotation;
        public Vector3 HalfExtents;

        public Vector3 RightAxis => Rotation * Vector3.right;
        public Vector3 UpAxis => Rotation * Vector3.up;
        public Vector3 ForwardAxis => Rotation * Vector3.forward;

        public OrientedBoundingBox(Vector3 center, Quaternion rotation, Vector3 halfExtents)
        {
            Center = center;
            Rotation = rotation;
            HalfExtents = halfExtents;
        }

        public static OrientedBoundingBox FromCollider(Collider collider)
        {
            if (collider is BoxCollider box)
            {
                return new OrientedBoundingBox(
                    box.transform.TransformPoint(box.center),
                    box.transform.rotation,
                    Vector3.Scale(box.size * 0.5f, box.transform.lossyScale)
                );
            }

            // Fallback: use bounds
            var bounds = collider.bounds;
            return new OrientedBoundingBox(
                bounds.center,
                Quaternion.identity,
                bounds.extents
            );
        }

        public string Serialize()
        {
            return $"{Center.x},{Center.y},{Center.z}|" +
                   $"{Rotation.x},{Rotation.y},{Rotation.z},{Rotation.w}|" +
                   $"{HalfExtents.x},{HalfExtents.y},{HalfExtents.z}";
        }

        public static OrientedBoundingBox Deserialize(string str)
        {
            if (string.IsNullOrEmpty(str))
                return default;

            var parts = str.Split('|');
            if (parts.Length != 3)
                return default;

            var centerParts = parts[0].Split(',');
            var rotParts = parts[1].Split(',');
            var extentParts = parts[2].Split(',');

            return new OrientedBoundingBox(
                new Vector3(
                    float.Parse(centerParts[0]),
                    float.Parse(centerParts[1]),
                    float.Parse(centerParts[2])),
                new Quaternion(
                    float.Parse(rotParts[0]),
                    float.Parse(rotParts[1]),
                    float.Parse(rotParts[2]),
                    float.Parse(rotParts[3])),
                new Vector3(
                    float.Parse(extentParts[0]),
                    float.Parse(extentParts[1]),
                    float.Parse(extentParts[2]))
            );
        }
    }
}
