using GameCode.Shared;
using UnityEngine;

namespace GameCode.Magic
{
    public class TrailBehaviour : SpellBehaviourBase
    {
        public Color TrailColor { get; set; } = Color.white;
        public Material CustomTrailMaterial = null;
        private TrailRenderer trailRenderer;

        public override void OnAttach(GameObject projectileObject)
        {
            trailRenderer = projectileObject.GetComponent<TrailRenderer>();
            if (trailRenderer == null)
            {
                trailRenderer = projectileObject.AddComponent<TrailRenderer>();
            }

            trailRenderer.startColor = TrailColor;
            trailRenderer.endColor = TrailColor;
            trailRenderer.startWidth = 0.3f;
            trailRenderer.endWidth = 0.1f;
            trailRenderer.time = 0.5f;
            trailRenderer.numCornerVertices = 3;

            if (CustomTrailMaterial != null)
            {
                trailRenderer.material = CustomTrailMaterial;
            }
            else
            {
                trailRenderer.material = new Material(
                    Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply")
                );
            }
        }
    }
}
