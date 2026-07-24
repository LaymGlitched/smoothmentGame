using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NanoCollab
{
    /// <summary>
    /// Draws selection outlines and active object manipulation indicators in SceneView.
    /// Camera 3D avatars are rendered directly by CollaboratorAvatarManager in the Scene Hierarchy.
    /// </summary>
    public sealed class SceneOverlay
    {
        private readonly PresenceManager _presence;
        private readonly Guid _localId;

        public SceneOverlay(PresenceManager presence, Guid localId)
        {
            _presence = presence;
            _localId  = localId;
        }

        public void OnSceneGUI(SceneView sceneView)
        {
            if (_presence.Users.Count == 0) return;

            foreach (var kv in _presence.Users)
            {
                var user = kv.Value;
                if (user.Id == _localId) continue;

                DrawSelectionHighlights(user);
                DrawManipulationIndicator(user);
            }

            sceneView.Repaint();
        }

        private void DrawSelectionHighlights(CollabUser user)
        {
            if (user.SelectedObjects == null || user.SelectedObjects.Length == 0) return;

            var oldZTest = Handles.zTest;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

            Handles.matrix = Matrix4x4.identity;
            var color = user.Color;
            color.a = 0.85f;
            Handles.color = color;

            foreach (var gid in user.SelectedObjects)
            {
                var go = gid.ToGameObject();
                if (go == null) continue;

                var bounds = GetBounds(go);
                Handles.DrawWireCube(bounds.center, bounds.size * 1.05f);
            }

            Handles.zTest = oldZTest;
        }

        private void DrawManipulationIndicator(CollabUser user)
        {
            if (!user.DraggingObject.IsValid()) return;

            var go = user.DraggingObject.ToGameObject();
            if (go == null) return;

            var oldZTest = Handles.zTest;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

            Handles.matrix = Matrix4x4.identity;
            var color = user.Color;

            var bounds = GetBounds(go);

            Handles.color = color;
            Handles.DrawWireCube(bounds.center, bounds.size * 1.12f);

            var labelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = color },
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
            };
            Vector3 topPos = bounds.center + Vector3.up * (bounds.extents.y + 0.4f);
            Handles.Label(topPos, $"✎ {user.Name} moving...", labelStyle);

            Handles.zTest = oldZTest;
        }

        private static Bounds GetBounds(GameObject go)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) return renderer.bounds;
            return new Bounds(go.transform.position, Vector3.one * 0.5f);
        }
    }
}
