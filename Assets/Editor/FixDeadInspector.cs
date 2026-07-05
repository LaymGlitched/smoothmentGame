// Quick script to refresh the Inspector when it becomes unresponsive
using UnityEditor;
using UnityEngine;
[InitializeOnLoad]
public class FixDeadInspector
{
    static FixDeadInspector()
    {
        Selection.selectionChanged += () => {
            ActiveEditorTracker.sharedTracker.ForceRebuild();
        };
    }
}