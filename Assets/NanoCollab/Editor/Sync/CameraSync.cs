using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NanoCollab
{
    /// <summary>
    /// Broadcasts local SceneView camera position/rotation to peers at ~30Hz.
    /// Also supports 'Camera Follow' mode to align local SceneView camera to a peer.
    /// </summary>
    public sealed class CameraSync
    {
        private const float SendInterval     = 1f / 30f; // ~30 Hz
        private const float PositionThreshold = 0.005f;
        private const float RotationThreshold = 0.05f; // degrees

        private readonly Transport       _transport;
        private readonly PresenceManager _presence;
        private readonly Guid            _localId;

        private Vector3    _lastSentPos;
        private Quaternion _lastSentRot;
        private float      _lastSendTime;

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
            if (userId.HasValue)
                Debug.Log($"[NanoCollab] Following camera of collaborator {userId.Value}");
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
                    sceneView.pivot = targetUser.CameraPosition;
                    sceneView.rotation = targetUser.CameraRotation;
                }
                else
                {
                    // User disconnected — stop following
                    FollowUserId = null;
                }
            }

            if (_transport.CurrentMode == Transport.Mode.None) return;

            float now = (float)EditorApplication.timeSinceStartup;
            if (now - _lastSendTime < SendInterval) return;

            var cam = sceneView.camera;
            if (cam == null) return;

            var pos = cam.transform.position;
            var rot = cam.transform.rotation;

            // Delta check
            float posDelta = Vector3.Distance(pos, _lastSentPos);
            float rotDelta = Quaternion.Angle(rot, _lastSentRot);

            if (posDelta < PositionThreshold && rotDelta < RotationThreshold)
                return;

            _lastSentPos  = pos;
            _lastSentRot  = rot;
            _lastSendTime = now;

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
