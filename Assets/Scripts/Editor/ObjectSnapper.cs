using UnityEngine;
using UnityEditor;

public static class GroundSnapTool
{
    [MenuItem("Tools/Snap Selected To Ground %#g")] // Ctrl+Shift+G
    private static void SnapSelectedToGround()
    {
        foreach (GameObject obj in Selection.gameObjects)
        {
            if (obj == null) continue;

            SnapObjectToGround(obj);
        }
    }

    private static void SnapObjectToGround(GameObject obj)
    {
        Transform t = obj.transform;

        // Gather all colliders on this object (so we can ignore self-hits)
        Collider[] ownColliders = obj.GetComponentsInChildren<Collider>();

        // Compute world-space bounds to find the pivot->bottom offset
        Bounds? bounds = GetObjectBounds(obj);

        // Where to start the ray from, and how far the pivot is above the object's bottom
        float pivotToBottom;
        Vector3 rayOrigin;

        if (bounds.HasValue)
        {
            pivotToBottom = t.position.y - bounds.Value.min.y;
            rayOrigin = new Vector3(bounds.Value.center.x, bounds.Value.max.y + 0.1f, bounds.Value.center.z);
        }
        else
        {
            // No collider/renderer found — fall back to treating the pivot as the base
            pivotToBottom = 0f;
            rayOrigin = t.position + Vector3.up * 0.5f;
        }

        // Raycast down, skipping any hits that belong to the object itself
        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, 1000f);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            bool isSelf = false;
            foreach (Collider c in ownColliders)
            {
                if (hit.collider == c) { isSelf = true; break; }
            }
            if (isSelf) continue;

            Undo.RecordObject(t, "Snap To Ground");

            // Place the pivot so the object's bottom sits exactly on the hit point
            Vector3 newPos = t.position;
            newPos.y = hit.point.y + pivotToBottom;
            t.position = newPos;

            return; // done with this object
        }
    }

    // Combines all renderer bounds (falls back to collider bounds) into one world-space bounds
    private static Bounds? GetObjectBounds(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        Bounds? result = null;

        foreach (Renderer r in renderers)
        {
            if (!result.HasValue)
                result = r.bounds;
            else
                result.Value.Encapsulate(r.bounds);
        }

        if (result.HasValue) return result;

        // No renderers — try colliders instead
        Collider[] colliders = obj.GetComponentsInChildren<Collider>();
        foreach (Collider c in colliders)
        {
            if (!result.HasValue)
                result = c.bounds;
            else
                result.Value.Encapsulate(c.bounds);
        }

        return result;
    }
}