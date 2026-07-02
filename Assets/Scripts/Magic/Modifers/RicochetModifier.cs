using GameCode.Magic;
using UnityEngine;

[CreateAssetMenu(menuName = "Magic/Modifiers/Ricochet")]
public class RicochetModifier : SpellModifierDefinition
{
    public int MaxBounces = 3;
    public LayerMask BounceMask = -1;

    public override void Apply(SpellContext context)
    {
        if (context.Projectile == null)
            return;

        var ricochet = new RicochetBehaviour { MaxBounces = MaxBounces, BounceMask = BounceMask };

        context.Projectile.AddBehaviour(ricochet);
    }
}
