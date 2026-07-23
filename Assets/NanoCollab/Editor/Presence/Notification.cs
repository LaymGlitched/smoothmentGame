using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NanoCollab
{
    /// <summary>
    /// Non-intrusive join/leave toast notifications rendered in the SceneView.
    /// Auto-dismiss after 3 seconds with a gentle fade.
    /// </summary>
    public sealed class Notification
    {
        private struct Toast
        {
            public string Text;
            public Color  Color;
            public double SpawnTime;
        }

        private const float Duration = 3.0f;
        private const float FadeTime = 0.5f;

        private readonly List<Toast> _toasts = new();

        public Notification(PresenceManager presence)
        {
            presence.OnUserJoined += user =>
                Show($"{user.Name} joined", user.Color);
            presence.OnUserLeft += user =>
                Show($"{user.Name} left", user.Color);
        }

        public void Show(string text, Color color)
        {
            _toasts.Add(new Toast
            {
                Text      = text,
                Color     = color,
                SpawnTime = EditorApplication.timeSinceStartup
            });
        }

        /// <summary>
        /// Draw active toasts in the SceneView. Call from duringSceneGui.
        /// </summary>
        public void DrawSceneGUI(SceneView sceneView)
        {
            if (_toasts.Count == 0) return;

            double now = EditorApplication.timeSinceStartup;

            Handles.BeginGUI();
            float y = 8f;

            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                var toast = _toasts[i];
                float elapsed = (float)(now - toast.SpawnTime);

                if (elapsed > Duration)
                {
                    _toasts.RemoveAt(i);
                    continue;
                }

                // Fade in/out
                float alpha = 1f;
                if (elapsed < FadeTime)
                    alpha = elapsed / FadeTime;
                else if (elapsed > Duration - FadeTime)
                    alpha = (Duration - elapsed) / FadeTime;

                var style = new GUIStyle(EditorStyles.helpBox)
                {
                    fontSize  = 12,
                    fontStyle = FontStyle.Bold,
                    padding   = new RectOffset(10, 10, 6, 6),
                    alignment = TextAnchor.MiddleLeft,
                };

                var col = toast.Color;
                col.a = alpha;
                style.normal.textColor = col;

                var rect = sceneView.position;
                float width  = 200f;
                float height = 28f;
                float x = rect.width - width - 12f;

                var toastRect = new Rect(x, y, width, height);

                // Semi-transparent background
                var bgColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.15f, 0.15f, 0.15f, alpha * 0.85f);
                GUI.Box(toastRect, "", EditorStyles.helpBox);
                GUI.backgroundColor = bgColor;

                // Color pip
                var pipRect = new Rect(toastRect.x + 6, toastRect.y + 8, 10, 10);
                EditorGUI.DrawRect(pipRect, col);

                // Text
                var textRect = new Rect(toastRect.x + 22, toastRect.y, toastRect.width - 28, toastRect.height);
                GUI.Label(textRect, toast.Text, style);

                y += height + 4f;
            }

            Handles.EndGUI();

            // Force repaint while toasts are active
            if (_toasts.Count > 0)
                sceneView.Repaint();
        }
    }
}
