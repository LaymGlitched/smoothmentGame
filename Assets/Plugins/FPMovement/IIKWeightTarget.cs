namespace FPMovement
{
    /// <summary>
    /// Interface for components that can receive an IK weight from the animation controller,
    /// avoiding the need for reflection.
    /// </summary>
    public interface IIKWeightTarget
    {
        float weight { get; set; }
    }
}
