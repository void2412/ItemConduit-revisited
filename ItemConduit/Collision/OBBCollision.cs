using UnityEngine;

namespace ItemConduit.Collision
{
    public static class OBBCollision
    {
        public static bool TestOBBOBB(OrientedBoundingBox a, OrientedBoundingBox b)
        {
            Vector3[] axesA = { a.RightAxis, a.UpAxis, a.ForwardAxis };
            Vector3[] axesB = { b.RightAxis, b.UpAxis, b.ForwardAxis };

            // Test face normals (6 axes)
            foreach (var axis in axesA)
                if (!ProjectionOverlap(a, b, axis))
                    return false;

            foreach (var axis in axesB)
                if (!ProjectionOverlap(a, b, axis))
                    return false;

            // Test cross products (9 axes)
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    Vector3 axis = Vector3.Cross(axesA[i], axesB[j]);
                    if (axis.sqrMagnitude < 0.001f)
                        continue; // Skip degenerate

                    axis.Normalize();
                    if (!ProjectionOverlap(a, b, axis))
                        return false;
                }
            }

            return true; // All overlap = collision
        }

        private static bool ProjectionOverlap(
            OrientedBoundingBox a,
            OrientedBoundingBox b,
            Vector3 axis)
        {
            float aCenter = Vector3.Dot(a.Center, axis);
            float aExtent = ExtentAlongAxis(a, axis);

            float bCenter = Vector3.Dot(b.Center, axis);
            float bExtent = ExtentAlongAxis(b, axis);

            return Mathf.Abs(aCenter - bCenter) <= aExtent + bExtent;
        }

        private static float ExtentAlongAxis(OrientedBoundingBox box, Vector3 axis)
        {
            return box.HalfExtents.x * Mathf.Abs(Vector3.Dot(axis, box.RightAxis)) +
                   box.HalfExtents.y * Mathf.Abs(Vector3.Dot(axis, box.UpAxis)) +
                   box.HalfExtents.z * Mathf.Abs(Vector3.Dot(axis, box.ForwardAxis));
        }
    }
}
