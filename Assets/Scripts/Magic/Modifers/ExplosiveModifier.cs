using GameCode.Magic;
using Nanodogs.API.Explosion;
using UnityEngine;

[CreateAssetMenu(menuName = "Magic/Modifiers/Explosive")]
public class ExplosiveModifier : SpellModifierDefinition
{
    public float Radius = 5f;
    public float DamageMultiplier = 1.5f;
    public float KnockbackMultiplier = 1.0f;

    public ExplosionSettings Settings;

    public override void Apply(SpellContext context)
    {
        if (context.Projectile == null)
            return;

        var explosive = new ExplosiveBehaviour
        {
            Radius = Radius,
            DamageMultiplier = DamageMultiplier,
            KnockbackMultiplier = KnockbackMultiplier,
            Settings = Settings,
        };

        context.Projectile.AddBehaviour(explosive);
    }
}
