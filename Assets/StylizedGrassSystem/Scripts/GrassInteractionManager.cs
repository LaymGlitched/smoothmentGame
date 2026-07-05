using UnityEngine;
using System.Collections.Generic;

namespace StylizedGrassSystem
{
    [ExecuteAlways]
    public class GrassInteractionManager : MonoBehaviour
    {
        public static GrassInteractionManager Instance { get; private set; }

        [Header("Settings")]
        public float mapSize = 50f;
        public int mapResolution = 1024;
        public Transform followTarget;

        [Header("References")]
        public Shader interactionShader;
        
        private RenderTexture interactionMap;
        private Material interactionMaterial;
        private Mesh quadMesh;
        
        private HashSet<GrassInteractor> interactors = new HashSet<GrassInteractor>();

        private void OnEnable()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) DestroyImmediate(this);

            Initialize();
        }

        private void OnDisable()
        {
            if (Instance == this) Instance = null;
            Cleanup();
        }

        private void Initialize()
        {
            if (interactionShader == null)
            {
                interactionShader = Shader.Find("Stylized/GrassInteraction");
            }

            if (interactionMaterial == null && interactionShader != null)
            {
                interactionMaterial = new Material(interactionShader);
            }

            if (interactionMap == null)
            {
                interactionMap = new RenderTexture(mapResolution, mapResolution, 0, RenderTextureFormat.ARGB32);
                interactionMap.name = "GrassInteractionMap";
                interactionMap.wrapMode = TextureWrapMode.Clamp;
                interactionMap.filterMode = FilterMode.Bilinear;
            }

            if (quadMesh == null)
            {
                quadMesh = CreateQuad();
            }
        }

        private void Cleanup()
        {
            if (interactionMap != null)
            {
                interactionMap.Release();
                DestroyImmediate(interactionMap);
            }
            if (interactionMaterial != null)
            {
                DestroyImmediate(interactionMaterial);
            }
            if (quadMesh != null)
            {
                DestroyImmediate(quadMesh);
            }
        }

        public void Register(GrassInteractor interactor)
        {
            interactors.Add(interactor);
        }

        public void Unregister(GrassInteractor interactor)
        {
            interactors.Remove(interactor);
        }

        private void LateUpdate()
        {
            if (interactionMap == null || interactionMaterial == null) return;

            Vector3 centerPos = Vector3.zero;
            
            // Follow target or main camera
            if (followTarget != null)
            {
                centerPos = followTarget.position;
            }
            else if (Camera.main != null)
            {
                centerPos = Camera.main.transform.position;
            }

            // Lock to grid to avoid shimmering/jitter
            float texelSize = (mapSize * 2f) / mapResolution;
            centerPos.x = Mathf.Round(centerPos.x / texelSize) * texelSize;
            centerPos.z = Mathf.Round(centerPos.z / texelSize) * texelSize;

            RenderInteractionMap(centerPos);
        }

        private void RenderInteractionMap(Vector3 centerPos)
        {
            RenderTexture activeRT = RenderTexture.active;
            RenderTexture.active = interactionMap;

            // Clear to gray (0.5, 0.5) for direction, 0 for intensity
            GL.Clear(false, true, new Color(0.5f, 0.5f, 0f, 0f));

            // Setup Ortho projection
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, mapResolution, 0, mapResolution);

            // Calculate world to local mapping
            float worldToLocal = mapResolution / (mapSize * 2f);

            foreach (var interactor in interactors)
            {
                if (interactor == null || !interactor.isActiveAndEnabled) continue;

                // Skip interactors that are too high above ground
                float heightAtten = interactor.HeightAttenuation;
                if (heightAtten < 0.01f) continue;

                Vector3 pos = interactor.EffectPosition;
                
                // Check if inside bounds
                if (Mathf.Abs(pos.x - centerPos.x) > mapSize + interactor.radius ||
                    Mathf.Abs(pos.z - centerPos.z) > mapSize + interactor.radius)
                    continue;

                // Map position to render texture pixels
                float pixelX = (pos.x - centerPos.x + mapSize) * worldToLocal;
                float pixelY = (pos.z - centerPos.z + mapSize) * worldToLocal;
                float pixelRadius = interactor.radius * worldToLocal;

                // Shift interaction center in movement direction for natural directional bending
                Vector3 vel = interactor.Velocity;
                float xzSpeed = Mathf.Sqrt(vel.x * vel.x + vel.z * vel.z);
                if (xzSpeed > 0.5f && interactor.velocityInfluence > 0f)
                {
                    // Normalize XZ velocity and scale the offset
                    float invSpeed = 1f / xzSpeed;
                    float velDirX = vel.x * invSpeed;
                    float velDirZ = vel.z * invSpeed;

                    // Offset scales with speed but caps at half the radius to keep it grounded
                    float shiftAmount = Mathf.Min(xzSpeed * 0.08f, interactor.radius * 0.5f) * interactor.velocityInfluence;
                    pixelX += velDirX * shiftAmount * worldToLocal;
                    pixelY += velDirZ * shiftAmount * worldToLocal;
                }

                // Draw quad
                Rect rect = new Rect(pixelX - pixelRadius, pixelY - pixelRadius, pixelRadius * 2f, pixelRadius * 2f);
                
                // Apply height attenuation to bend strength and trail intensity
                interactionMaterial.SetFloat("_BendStrength", interactor.bendStrength * heightAtten);
                interactionMaterial.SetFloat("_TrailIntensity", interactor.trailIntensity * heightAtten);
                interactionMaterial.SetPass(0);
                
                DrawRect(rect);
            }

            GL.PopMatrix();
            RenderTexture.active = activeRT;

            // Set global shader parameters for grass shader
            Shader.SetGlobalTexture("_InteractionMap", interactionMap);
            Shader.SetGlobalVector("_InteractionMapParams", new Vector4(mapSize, 0, centerPos.x, centerPos.z));
        }

        private void DrawRect(Rect rect)
        {
            GL.Begin(GL.QUADS);
            GL.TexCoord2(0, 0); GL.Vertex3(rect.xMin, rect.yMin, 0);
            GL.TexCoord2(0, 1); GL.Vertex3(rect.xMin, rect.yMax, 0);
            GL.TexCoord2(1, 1); GL.Vertex3(rect.xMax, rect.yMax, 0);
            GL.TexCoord2(1, 0); GL.Vertex3(rect.xMax, rect.yMin, 0);
            GL.End();
        }

        private Mesh CreateQuad()
        {
            Mesh m = new Mesh();
            m.vertices = new Vector3[] {
                new Vector3(-0.5f, -0.5f, 0),
                new Vector3(0.5f, -0.5f, 0),
                new Vector3(0.5f, 0.5f, 0),
                new Vector3(-0.5f, 0.5f, 0)
            };
            m.uv = new Vector2[] {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(1, 1),
                new Vector2(0, 1)
            };
            m.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
            return m;
        }
    }
}
