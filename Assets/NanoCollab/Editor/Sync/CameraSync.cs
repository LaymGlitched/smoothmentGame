using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NanoCollab
{
    /// <summary>
    /// Broadcasts the local SceneView camera position/rotation to peers
    /// and receives remote camera updates. Rate-limited to ~15 updates/sec.
    /// Delta-only: only sends when camera moves beyond threshold.
    /// </summary>
    public sealed class CameraSync
    {
        private const float SendInterval     = 1f / 15f; // ~15 Hz
        private const float PositionThreshold = 0.01f;
        private const float RotationThreshold = 0.1f; // degrees

        private readonly Transport       _transport;
        private readonly PresenceManager _presence;
        private readonly Guid            _localId;

        private Vector3    _lastSentPos;
        private Quaternion _lastSentRot;
        private float      _lastSendTime;

        public CameraSync(Transport transport, PresenceManager presence,
                          MessageRouter router, Guid localId)
        {
            _transport = transport;
            _presence  = presence;
            _localId   = localId;

            router.Register(MsgType.CameraUpdate, OnCameraUpdateReceived);
        }

        /// <summary>
        /// Call from SceneView.duringSceneGui to capture and broadcast local camera.
        /// </summary>
        public void OnSceneGUI(SceneView sceneView)
        {
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
            Serialization.WriteGuid(w, _localId);
            Serialization.WriteVector3(w, pos);
            Serialization.WriteQuaternion(w, rot);

            _transport.Broadcast(MsgType.CameraUpdate, ms.ToArray());
        }

        private void OnCameraUpdateReceived(BinaryReader r)
        {
            var userId = Serialization.ReadGuid(r);
            var pos    = Serialization.ReadVector3(r);
            var rot    = Serialization.ReadQuaternion(r);
            double now = EditorApplication.timeSinceStartup;

            _presence.UpdateUser(userId, user =>
            {
                user.CameraPosition    = pos;
                user.CameraRotation    = rot;
                user.CameraLastUpdated = now;
            });

            // Force SceneView repaint so overlay updates
            SceneView.RepaintAll();
        }
    }
}
