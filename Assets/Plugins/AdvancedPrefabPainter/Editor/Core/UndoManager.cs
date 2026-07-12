using UnityEditor;
using UnityEngine;

namespace AdvancedPrefabPainter.Editor.Core
{
    public static class UndoManager
    {
        public static void RecordPaintAction(Object target, string actionName)
        {
            if (target != null)
            {
                Undo.RecordObject(target, actionName);
            }
        }

        public static void SetDirty(Object target)
        {
            if (target != null)
            {
                EditorUtility.SetDirty(target);
            }
        }
    }
}
