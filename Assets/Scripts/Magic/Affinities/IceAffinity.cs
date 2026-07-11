using GameCode.Magic;
using UnityEngine;

[CreateAssetMenu(menuName = "Magic/Affinities/Ice")]
public class IceAffinity : AffinityDefinition
{
    [Header("Ice Path")]
    public float PathDuration = 3f;
    public float SlideDeceleration = 0.01f;

    public override void Apply(SpellContext context)
    {
        // Add damage/slow behavior to projectile if there is one
        if (context.Projectile != null)
        {
            // Placeholder for cold damage or slow effects
            // e.g. context.Projectile.AddBehaviour(new SlowBehaviour());
        }

        // Ice Path logic
        if (context.Caster != null)
        {
            var slideController = context.Caster.GetComponent<FPMovement.SlideController>();
            
            // Check if player is sliding when the spell is cast
            if (slideController != null && slideController.IsSliding)
            {
                var icePath = context.Caster.GetComponent<IcePathEffect>();
                if (icePath == null)
                {
                    icePath = context.Caster.AddComponent<IcePathEffect>();
                }
                
                icePath.ApplyIcePath(slideController, PathDuration, SlideDeceleration);
            }
        }
    }
}
