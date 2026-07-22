namespace FPMovement
{
    /// <summary>
    /// Interface for components attached to GameObjects, colliders, or trigger volumes
    /// to directly declare their <see cref="SurfaceType"/>.
    /// </summary>
    public interface ISurfaceProvider
    {
        /// <summary>
        /// Gets the surface type associated with this object.
        /// </summary>
        SurfaceType GetSurfaceType();
    }
}
