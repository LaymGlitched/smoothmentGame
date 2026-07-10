using UnityEngine;
using UnityEditor;
using UnityEngine.Animations.Rigging;

public class TwoBoneIKAligner : EditorWindow
{
    private float hintDistance = 0.5f;

    [MenuItem("Tools/Animation Rigging/Align Two Bone IK")]
    public static void ShowWindow()
    {
        GetWindow<TwoBoneIKAligner>("Align Two Bone IK");
    }

    private void OnGUI()
    {
        GUILayout.Label("Align Two Bone IK Constraints", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Select one or more GameObjects with TwoBoneIKConstraint components, or a parent GameObject, then click Align.", 
            MessageType.Info);

        hintDistance = EditorGUILayout.FloatField("Hint Distance", hintDistance);

        if (GUILayout.Button("Align Selected"))
        {
            AlignSelected();
        }
    }

    private void AlignSelected()
    {
        if (Selection.gameObjects == null || Selection.gameObjects.Length == 0)
        {
            Debug.LogWarning("No objects selected. Please select a GameObject containing TwoBoneIKConstraint(s).");
            return;
        }

        int alignedCount = 0;

        foreach (GameObject go in Selection.gameObjects)
        {
            TwoBoneIKConstraint[] constraints = go.GetComponentsInChildren<TwoBoneIKConstraint>(true);
            foreach (var constraint in constraints)
            {
                if (AlignConstraint(constraint, hintDistance))
                {
                    alignedCount++;
                }
            }
        }

        Debug.Log($"Successfully aligned {alignedCount} TwoBoneIKConstraint(s).");
    }

    public static bool AlignConstraint(TwoBoneIKConstraint constraint, float hintDist)
    {
        if (constraint.data.root == null || constraint.data.mid == null || constraint.data.tip == null)
        {
            Debug.LogWarning($"Constraint on {constraint.name} is missing root, mid, or tip bone references. Skipping.");
            return false;
        }

        if (constraint.data.target == null || constraint.data.hint == null)
        {
            Debug.LogWarning($"Constraint on {constraint.name} is missing Target or Hint transform. Skipping.");
            return false;
        }

        Transform root = constraint.data.root;
        Transform mid = constraint.data.mid;
        Transform tip = constraint.data.tip;
        Transform target = constraint.data.target;
        Transform hint = constraint.data.hint;

        // Record Undo
        Undo.RecordObject(target, "Align IK Target");
        Undo.RecordObject(hint, "Align IK Hint");
        Undo.RecordObject(constraint, "Align IK Constraint Data");

        // 1. Align Target to Tip
        target.position = tip.position;
        target.rotation = tip.rotation;
        
        // Ensure unexpected scales aren't inherited
        target.localScale = Vector3.one;

        // Force 'Maintain Target Offset' on the constraint
        var data = constraint.data;
        data.maintainTargetPositionOffset = true;
        data.maintainTargetRotationOffset = true;
        constraint.data = data;

        // 2. Calculate Hint Position
        // Vector from root to tip
        Vector3 rootToTip = tip.position - root.position;
        // Vector from root to mid
        Vector3 rootToMid = mid.position - root.position;

        // Project rootToMid onto rootToTip to find the closest point on the root-tip axis
        Vector3 projected = Vector3.Project(rootToMid, rootToTip);

        // The direction from the projected point to the mid joint is the bend direction
        Vector3 bendDir = (rootToMid - projected).normalized;

        // If the limb is perfectly straight, bendDir might be zero. 
        // In this case, we can't reliably guess the bend direction without prior knowledge.
        if (bendDir.sqrMagnitude < 0.001f)
        {
            Debug.LogWarning($"Limb on {constraint.name} is perfectly straight. Hint direction might not be perfectly accurate.");
            // Default to forward relative to mid if we can't determine
            bendDir = mid.forward; 
        }

        // Place hint at a distance from the mid joint in the bend direction
        hint.position = mid.position + bendDir * hintDist;

        // Mark as dirty
        EditorUtility.SetDirty(target);
        EditorUtility.SetDirty(hint);

        return true;
    }
}
