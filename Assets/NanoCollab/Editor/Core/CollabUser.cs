using System;
using UnityEngine;

namespace NanoCollab
{
    /// <summary>
    /// Represents a connected collaborator's identity and live state.
    /// Mutable struct — updated in-place by sync modules.
    /// </summary>
    [Serializable]
    public struct CollabUser
    {
        public Guid   Id;
        public string Name;
        public Color  Color;

        // Live state (updated by sync modules)
        public Vector3    CameraPosition;
        public Quaternion CameraRotation;
        public string[]   SelectedPaths;
        public float      LatencyMs;

        // Timestamps for interpolation
        public double CameraLastUpdated;

        public CollabUser(Guid id, string name, Color color)
        {
            Id              = id;
            Name            = name;
            Color           = color;
            CameraPosition  = Vector3.zero;
            CameraRotation  = Quaternion.identity;
            SelectedPaths   = Array.Empty<string>();
            LatencyMs       = 0f;
            CameraLastUpdated = 0.0;
        }
    }
}
