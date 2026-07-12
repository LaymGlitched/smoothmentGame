using UnityEngine;

namespace AdvancedPrefabPainter.Editor.Algorithms
{
    public struct SurfaceSample
    {
        public bool isValid;
        public Vector3 point;
        public Vector3 normal;
    }

    public static class SurfaceSampler
    {
        public static SurfaceSample SampleSurface(Vector3 origin, Vector3 direction, float distance, LayerMask hitMask)
        {
            SurfaceSample sample = new SurfaceSample();
            if (Physics.Raycast(origin, direction, out RaycastHit hit, distance, hitMask))
            {
                sample.isValid = true;
                sample.point = hit.point;
                sample.normal = hit.normal;
            }
            else
            {
                sample.isValid = false;
            }
            return sample;
        }

        public static bool CheckSlopeAndHeight(SurfaceSample sample, float minSlope, float maxSlope, bool useHeight, float minHeight, float maxHeight)
        {
            if (!sample.isValid) return false;

            float slope = Vector3.Angle(Vector3.up, sample.normal);
            if (slope < minSlope || slope > maxSlope) return false;

            if (useHeight)
            {
                if (sample.point.y < minHeight || sample.point.y > maxHeight) return false;
            }

            return true;
        }
    }
}
