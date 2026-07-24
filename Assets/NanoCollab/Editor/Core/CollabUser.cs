using System;
using UnityEditor;
using UnityEngine;

namespace NanoCollab
{
    /// <summary>
    /// Represents a connected collaborator's identity and live state.
    /// Reference class — updated in-place by sync modules and presence manager.
    /// </summary>
    [Serializable]
    public sealed class CollabUser
    {
        public Guid   Id;
        public string Name;
        public Color  Color;
        public long   SessionStartTimeTicks;

        // Live state
        public Vector3          CameraPosition;
        public Quaternion       CameraRotation;
        public GlobalObjectId[] SelectedObjects;
        public GlobalObjectId   DraggingObject; // Manipulation awareness
        public float            LatencyMs;

        // Timestamps for interpolation
        public double CameraLastUpdated;

        public CollabUser(Guid id, string name, Color color, long sessionStartTimeTicks = 0)
        {
            Id                    = id;
            Name                  = name;
            Color                 = color;
            SessionStartTimeTicks = sessionStartTimeTicks;
            CameraPosition        = Vector3.zero;
            CameraRotation        = Quaternion.identity;
            SelectedObjects       = Array.Empty<GlobalObjectId>();
            DraggingObject        = default;
            LatencyMs             = 0f;
            CameraLastUpdated     = 0.0;
        }
    }
}
