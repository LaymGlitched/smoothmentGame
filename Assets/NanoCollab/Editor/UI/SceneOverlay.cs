using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NanoCollab
{
    /// <summary>
    /// Draws remote collaborator cameras (frustum + name), selection outlines,
    /// and active object manipulation indicators (colored halo + movement label).
    /// </summary>
    public sealed class SceneOverlay
    {
        private const float CameraGizmoSize = 0.6f;
        private const float LerpSpeed       = 14f;

        private readonly PresenceManager _presence;
        private readonly Dictionary<Guid, Vector3>    _interpPos = new();
        private readonly Dictionary<Guid, Quaternion>  _interpRot = new();

        public SceneOverlay(PresenceManager presence)
        {
            _presence = presence;
        }

        public void OnSceneGUI(SceneView sceneView)
        {
            if (_presence.Users.Count == 0) return;

            float t = 1f - Mathf.Exp(-LerpSpeed * 0.016f);

            foreach (var kv in _presence.Users)
            {
                var user = kv.Value;
                DrawRemoteCamera(user, t);
                DrawSelectionHighlights(user);
                DrawManipulationIndicator(user);
            }

            sceneView.Repaint();
        }

        private void DrawRemoteCamera(CollabUser user, float t)
        {
            if (!_interpPos.TryGetValue(user.Id, out var currentPos))
                currentPos = user.CameraPosition;
            if (!_interpRot.TryGetValue(user.Id, out var currentRot))
                currentRot = user.CameraRotation;

            currentPos = Vector3.Lerp(currentPos, user.CameraPosition, t);
            currentRot = Quaternion.Slerp(currentRot, user.CameraRotation, t);

            _interpPos[user.Id] = currentPos;
            _interpRot[user.Id] = currentRot;

            if (currentPos == Vector3.zero && currentRot == Quaternion.identity)
                return;

            var color = user.Color;
            var matrix = Matrix4x4.TRS(currentPos, currentRot, Vector3.one);
            Handles.matrix = matrix;

            Handles.color = color;
            DrawFrustumGizmo(CameraGizmoSize);

            Handles.matrix = Matrix4x4.identity;
            Handles.color = color;
            Handles.SphereHandleCap(0, currentPos, Quaternion.identity, CameraGizmoSize * 0.35f, EventType.Repaint);

            var labelPos = currentPos + Vector3.up * CameraGizmoSize * 1.2f;
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = color },
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
            };
            Handles.Label(labelPos, user.Name, style);
        }

        private static void DrawFrustumGizmo(float size)
        {
            float near = size * 0.3f;
            float far  = size;
            float halfW = size * 0.4f;
            float halfH = size * 0.3f;

            var n0 = new Vector3(-halfW * 0.3f, -halfH * 0.3f, near);
            var n1 = new Vector3( halfW * 0.3f, -halfH * 0.3f, near);
            var n2 = new Vector3( halfW * 0.3f,  halfH * 0.3f, near);
            var n3 = new Vector3(-halfW * 0.3f,  halfH * 0.3f, near);

            var f0 = new Vector3(-halfW, -halfH, far);
            var f1 = new Vector3( halfW, -halfH, far);
            var f2 = new Vector3( halfW,  halfH, far);
            var f3 = new Vector3(-halfW,  halfH, far);

            Handles.DrawLine(n0, n1); Handles.DrawLine(n1, n2); Handles.DrawLine(n2, n3); Handles.DrawLine(n3, n0);
            Handles.DrawLine(f0, f1); Handles.DrawLine(f1, f2); Handles.DrawLine(f2, f3); Handles.DrawLine(f3, f0);
            Handles.DrawLine(n0, f0); Handles.DrawLine(n1, f1); Handles.DrawLine(n2, f2); Handles.DrawLine(n3, f3);
        }

        private void DrawSelectionHighlights(CollabUser user)
        {
            if (user.SelectedObjects == null || user.SelectedObjects.Length == 0) return;

            Handles.matrix = Matrix4x4.identity;
            var color = user.Color;
            color.a = 0.7f;
            Handles.color = color;

            foreach (var gid in user.SelectedObjects)
            {
                var go = gid.ToGameObject();
                if (go == null) continue;

                var bounds = GetBounds(go);
                Handles.DrawWireCube(bounds.center, bounds.size * 1.05f);
            }
        }

        private void DrawManipulationIndicator(CollabUser user)
        {
            if (!user.DraggingObject.IsValid()) return;

            var go = user.DraggingObject.ToGameObject();
            if (go == null) return;

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
        }

        private static Bounds GetBounds(GameObject go)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) return renderer.bounds;
            return new Bounds(go.transform.position, Vector3.one * 0.5f);
        }
    }
}
