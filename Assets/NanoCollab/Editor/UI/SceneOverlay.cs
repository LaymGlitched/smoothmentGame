using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NanoCollab
{
    /// <summary>
    /// Draws remote collaborator cameras (frustum gizmo + name label) and
    /// selection highlights (wireframe box in user color) in the SceneView.
    /// Smoothly interpolates remote camera positions.
    /// </summary>
    public sealed class SceneOverlay
    {
        private const float CameraGizmoSize = 0.6f;
        private const float LerpSpeed       = 12f;

        private readonly PresenceManager _presence;

        // Interpolated positions for smooth camera movement
        private readonly Dictionary<Guid, Vector3>    _interpPos = new();
        private readonly Dictionary<Guid, Quaternion>  _interpRot = new();

        public SceneOverlay(PresenceManager presence)
        {
            _presence = presence;
        }

        /// <summary>
        /// Call from SceneView.duringSceneGui to render all overlays.
        /// </summary>
        public void OnSceneGUI(SceneView sceneView)
        {
            if (_presence.Users.Count == 0) return;

            float dt = Mathf.Min(0.05f, Time.realtimeSinceStartup * 0); // Handles deltaTime
            // Use a fixed smooth factor since EditorApplication doesn't provide delta
            float t = 1f - Mathf.Exp(-LerpSpeed * 0.016f); // assume ~60fps

            foreach (var kv in _presence.Users)
            {
                var user = kv.Value;
                DrawRemoteCamera(user, t);
                DrawSelectionHighlights(user);
            }

            // Keep repainting for smooth interpolation
            sceneView.Repaint();
        }

        private void DrawRemoteCamera(CollabUser user, float t)
        {
            // Interpolate
            if (!_interpPos.TryGetValue(user.Id, out var currentPos))
                currentPos = user.CameraPosition;
            if (!_interpRot.TryGetValue(user.Id, out var currentRot))
                currentRot = user.CameraRotation;

            currentPos = Vector3.Lerp(currentPos, user.CameraPosition, t);
            currentRot = Quaternion.Slerp(currentRot, user.CameraRotation, t);

            _interpPos[user.Id] = currentPos;
            _interpRot[user.Id] = currentRot;

            // Skip if at origin (no data yet)
            if (currentPos == Vector3.zero && currentRot == Quaternion.identity)
                return;

            var color = user.Color;

            // Draw camera frustum
            var matrix = Matrix4x4.TRS(currentPos, currentRot, Vector3.one);
            Handles.matrix = matrix;

            Handles.color = color;
            DrawFrustumGizmo(CameraGizmoSize);

            // Draw camera icon (small sphere)
            Handles.matrix = Matrix4x4.identity;
            Handles.color = color;
            Handles.SphereHandleCap(0, currentPos, Quaternion.identity,
                CameraGizmoSize * 0.35f, EventType.Repaint);

            // Draw name label above camera
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

            // Near plane corners
            var n0 = new Vector3(-halfW * 0.3f, -halfH * 0.3f, near);
            var n1 = new Vector3( halfW * 0.3f, -halfH * 0.3f, near);
            var n2 = new Vector3( halfW * 0.3f,  halfH * 0.3f, near);
            var n3 = new Vector3(-halfW * 0.3f,  halfH * 0.3f, near);

            // Far plane corners
            var f0 = new Vector3(-halfW, -halfH, far);
            var f1 = new Vector3( halfW, -halfH, far);
            var f2 = new Vector3( halfW,  halfH, far);
            var f3 = new Vector3(-halfW,  halfH, far);

            // Near plane
            Handles.DrawLine(n0, n1);
            Handles.DrawLine(n1, n2);
            Handles.DrawLine(n2, n3);
            Handles.DrawLine(n3, n0);

            // Far plane
            Handles.DrawLine(f0, f1);
            Handles.DrawLine(f1, f2);
            Handles.DrawLine(f2, f3);
            Handles.DrawLine(f3, f0);

            // Connecting lines
            Handles.DrawLine(n0, f0);
            Handles.DrawLine(n1, f1);
            Handles.DrawLine(n2, f2);
            Handles.DrawLine(n3, f3);
        }

        private void DrawSelectionHighlights(CollabUser user)
        {
            if (user.SelectedPaths == null || user.SelectedPaths.Length == 0) return;

            Handles.matrix = Matrix4x4.identity;
            var color = user.Color;
            color.a = 0.7f;
            Handles.color = color;

            foreach (var path in user.SelectedPaths)
            {
                var go = GameObject.Find(path);
                if (go == null) continue;

                // Get bounds
                var renderer = go.GetComponent<Renderer>();
                Bounds bounds;
                if (renderer != null)
                {
                    bounds = renderer.bounds;
                }
                else
                {
                    // Fallback: small box at position
                    bounds = new Bounds(go.transform.position, Vector3.one * 0.5f);
                }

                // Draw wireframe box
                Handles.DrawWireCube(bounds.center, bounds.size * 1.05f);

                // Draw a thicker outline
                var thickColor = color;
                thickColor.a = 0.4f;
                Handles.color = thickColor;
                Handles.DrawWireCube(bounds.center, bounds.size * 1.08f);
                Handles.color = color;
            }
        }
    }
}
