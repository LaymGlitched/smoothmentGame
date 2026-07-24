using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NanoCollab
{
    /// <summary>
    /// Broadcasts local SceneView camera position/rotation to peers at ~30Hz (and heartbeat every 1s).
    /// Also supports 'Camera Follow' mode to align local SceneView camera to a peer using sceneView.LookAt.
    /// </summary>
    public sealed class CameraSync
    {
        private const float SendInterval     = 1f / 30f; // ~30 Hz
        private const float HeartbeatInterval= 1.0f;     // Force send every 1s so new peers get initial position
        private const float PositionThreshold = 0.005f;
        private const float RotationThreshold = 0.05f; // degrees

        private readonly Transport       _transport;
        private readonly PresenceManager _presence;
        private readonly Guid            _localId;

        private Vector3    _lastSentPos;
        private Quaternion _lastSentRot;
        private float      _lastSendTime;
        private float      _lastHeartbeatTime;

        // Camera Follow state
        public Guid? FollowUserId { get; private set; }

        public CameraSync(Transport transport, PresenceManager presence, Guid localId)
        {
            _transport = transport;
            _presence  = presence;
            _localId   = localId;

            _transport.RegisterHandler(MsgType.CameraUpdate, OnCameraUpdateReceived);
        }

        public void SetFollowUser(Guid? userId)
        {
            FollowUserId = userId;
            if (userId.HasValue && _presence.TryGetUser(userId.Value, out var u))
                Debug.Log($"[NanoCollab] Following camera of collaborator '{u.Name}' ({userId.Value})");
            else
                Debug.Log("[NanoCollab] Stopped following camera.");
        }

        /// <summary>
        /// Call from SceneView.duringSceneGui to capture local camera and apply camera follow.
        /// </summary>
        public void OnSceneGUI(SceneView sceneView)
        {
            // Update Camera Follow if active
            if (FollowUserId.HasValue)
            {
                if (_presence.TryGetUser(FollowUserId.Value, out var targetUser))
                {
                    if (targetUser.CameraPosition != Vector3.zero || targetUser.CameraRotation != Quaternion.identity)
                    {
                        sceneView.LookAt(targetUser.CameraPosition, targetUser.CameraRotation);
                    }
                }
                else
                {
                    FollowUserId = null;
                }
            }

            var cam = sceneView.camera;
            if (cam == null) return;

            var pos = cam.transform.position;
            var rot = cam.transform.rotation;

            // Update local presence user camera position
            _presence.UpdateUser(_localId, user =>
            {
                user.CameraPosition = pos;
                user.CameraRotation = rot;
            });

            if (_transport.CurrentMode == Transport.Mode.None) return;

            float now = (float)EditorApplication.timeSinceStartup;
            if (now - _lastSendTime < SendInterval) return;

            // Delta & heartbeat check
            float posDelta = Vector3.Distance(pos, _lastSentPos);
            float rotDelta = Quaternion.Angle(rot, _lastSentRot);
            bool isHeartbeat = (now - _lastHeartbeatTime >= HeartbeatInterval);

            if (posDelta < PositionThreshold && rotDelta < RotationThreshold && !isHeartbeat)
                return;

            _lastSentPos       = pos;
            _lastSentRot       = rot;
            _lastSendTime      = now;
            _lastHeartbeatTime = now;

            // Serialize and send
            using var ms = new MemoryStream(48);
            using var w  = new BinaryWriter(ms);
            w.WriteGuid(_localId);
            w.WriteVector3(pos);
            w.WriteQuaternion(rot);

            _transport.Broadcast(MsgType.CameraUpdate, ms.ToArray());
        }

        private void OnCameraUpdateReceived(BinaryReader r)
        {
            var userId = r.ReadGuid();
            var pos    = r.ReadVector3();
            var rot    = r.ReadQuaternion();
            double now = EditorApplication.timeSinceStartup;

            _presence.UpdateUser(userId, user =>
            {
                user.CameraPosition    = pos;
                user.CameraRotation    = rot;
                user.CameraLastUpdated = now;
            });

            SceneView.RepaintAll();
        }
    }
}
