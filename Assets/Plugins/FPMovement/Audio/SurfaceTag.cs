using UnityEngine;

namespace FPMovement
{
    /// <summary>
    /// Component placed on GameObjects or colliders to explicitly override their <see cref="SurfaceType"/>.
    /// Checked by <see cref="SurfaceDetector"/> before falling back to PhysicMaterial or Terrain texture mapping.
    /// </summary>
    [DisallowMultipleComponent]
    public class SurfaceTag : MonoBehaviour, ISurfaceProvider
    {
        [SerializeField]
        [Tooltip("The surface type of this object or volume.")]
        private SurfaceType surfaceType;

        public SurfaceType SurfaceType
        {
            get => surfaceType;
            set => surfaceType = value;
        }

        public SurfaceType GetSurfaceType() => surfaceType;
    }
}
