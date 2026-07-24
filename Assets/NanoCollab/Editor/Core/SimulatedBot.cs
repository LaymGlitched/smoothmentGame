using System;
using UnityEditor;
using UnityEngine;

namespace NanoCollab
{
    /// <summary>
    /// Simulated collaborator bot for 1-click single-editor testing.
    /// Simulates real-time camera movement, selection outlines, and drag manipulation
    /// around the current SceneView focus area without requiring a second PC or Unity instance.
    /// </summary>
    public sealed class SimulatedBot
    {
        private readonly PresenceManager _presence;
        private readonly Guid _botId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        private bool _active;
        private Vector3 _orbitCenter = Vector3.up * 1.5f;

        public bool IsActive => _active;
        public Guid BotId => _botId;

        public SimulatedBot(PresenceManager presence)
        {
            _presence = presence;
        }

        public void ToggleBot()
        {
            if (_active) DespawnBot();
            else SpawnBot();
        }

        public void SpawnBot()
        {
            if (_active) return;
            _active = true;

            var view = SceneView.lastActiveSceneView;
            if (view != null)
            {
                _orbitCenter = view.pivot;
            }
            else
            {
                _orbitCenter = Vector3.up * 1.5f;
            }

            var user = _presence.AddUser(_botId, "Bot (Simulated)", DateTime.UtcNow.Ticks);
            _presence.UpdateUser(_botId, u =>
            {
                u.Color = new Color(0.93f, 0.60f, 1.00f); // Lavender
                u.CameraPosition = _orbitCenter + new Vector3(0, 3, -6);
                u.CameraRotation = Quaternion.Euler(20, 0, 0);
                u.LatencyMs = 15f;
            });

            Debug.Log($"[NanoCollab] Spawned Simulated Bot Collaborator at {_orbitCenter} for single-editor testing.");
        }

        public void DespawnBot()
        {
            if (!_active) return;
            _active = false;
            _presence.RemoveUser(_botId);
            Debug.Log("[NanoCollab] Despawned Simulated Bot Collaborator.");
        }

        public void Tick()
        {
            if (!_active) return;

            float t = (float)EditorApplication.timeSinceStartup;

            // Smooth orbiting camera movement around SceneView focus center
            float radius = 6f;
            float speed  = 0.7f;
            float angle  = t * speed;

            Vector3 botPos = _orbitCenter + new Vector3(Mathf.Sin(angle) * radius, 2f + Mathf.Sin(t * 1.4f) * 1.2f, Mathf.Cos(angle) * radius);
            Quaternion botRot = Quaternion.LookRotation(_orbitCenter - botPos);

            _presence.UpdateUser(_botId, u =>
            {
                u.CameraPosition = botPos;
                u.CameraRotation = botRot;
                u.LatencyMs      = 12f + Mathf.Sin(t * 2f) * 3f;
            });

            SceneView.RepaintAll();
        }
    }
}
