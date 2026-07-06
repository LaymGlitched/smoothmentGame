using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class SkinnedMeshBoneRemapper : EditorWindow
{
    public Transform targetRigRoot;
    public List<SkinnedMeshRenderer> targetMeshes = new List<SkinnedMeshRenderer>();

    [MenuItem("Tools/Remap Skinned Mesh Bones")]
    public static void ShowWindow()
    {
        GetWindow<SkinnedMeshBoneRemapper>("Bone Remapper");
    }

    void OnGUI()
    {
        GUILayout.Label("Remap Skinned Mesh Bones", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Use this on FRESH meshes dragged from your project window, not the broken ones. Drag your new model into the scene, put its meshes in the list below, and put 'metarig' in the Target Rig slot.", MessageType.Info);

        targetRigRoot = (Transform)EditorGUILayout.ObjectField("Target Rig (e.g. metarig)", targetRigRoot, typeof(Transform), true);

        GUILayout.Space(10);
        GUILayout.Label("Fresh Meshes to Remap:", EditorStyles.boldLabel);
        
        SerializedObject so = new SerializedObject(this);
        SerializedProperty meshesProp = so.FindProperty("targetMeshes");
        EditorGUILayout.PropertyField(meshesProp, true);
        so.ApplyModifiedProperties();

        GUILayout.Space(10);

        if (GUILayout.Button("Remap Bones", GUILayout.Height(30)))
        {
            RemapBones();
        }
    }

    void RemapBones()
    {
        if (targetRigRoot == null)
        {
            Debug.LogError("Please assign the Target Rig Root.");
            return;
        }

        if (targetMeshes.Count == 0)
        {
            Debug.LogError("Please add at least one SkinnedMeshRenderer to the list.");
            return;
        }

        Dictionary<string, Transform> rigBones = new Dictionary<string, Transform>();
        Transform[] allRigBones = targetRigRoot.GetComponentsInChildren<Transform>(true);
        foreach (Transform bone in allRigBones)
        {
            if (!rigBones.ContainsKey(bone.name))
            {
                rigBones.Add(bone.name, bone);
            }
        }

        foreach (var smr in targetMeshes)
        {
            if (smr == null) continue;

            Transform[] currentBones = smr.bones;
            Transform[] newBones = new Transform[currentBones.Length];
            
            int missingNullBones = 0;
            int nameMismatchBones = 0;
            string mismatchedNames = "";

            for (int i = 0; i < currentBones.Length; i++)
            {
                if (currentBones[i] == null) 
                {
                    missingNullBones++;
                    continue;
                }

                string boneName = currentBones[i].name;

                if (rigBones.ContainsKey(boneName))
                {
                    newBones[i] = rigBones[boneName];
                }
                else
                {
                    nameMismatchBones++;
                    mismatchedNames += boneName + ", ";
                    newBones[i] = currentBones[i]; // keep old one just in case
                }
            }

            smr.bones = newBones;

            if (smr.rootBone != null && rigBones.ContainsKey(smr.rootBone.name))
            {
                smr.rootBone = rigBones[smr.rootBone.name];
            }

            if (missingNullBones > 0)
            {
                Debug.LogError($"[FAILED] {smr.name}: {missingNullBones} bones were completely missing (Null). You must use a FRESH mesh from the Project window, not the broken ones you copied.");
            }
            else if (nameMismatchBones > 0)
            {
                Debug.LogError($"[FAILED] {smr.name}: {nameMismatchBones} bones had names that DO NOT MATCH the target rig. Examples: {mismatchedNames}. You need to ensure the bones in Blender have the exact same names as metarig (like 'spine', 'shoulder.L')");
            }
            else
            {
                Debug.Log($"[SUCCESS] Successfully remapped bones for {smr.name}! All bones matched perfectly.");
            }

            EditorUtility.SetDirty(smr);
        }
    }
}
