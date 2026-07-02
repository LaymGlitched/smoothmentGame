using GameCode.Magic;
using GameCode.Shared;
using UnityEngine;

[CreateAssetMenu(menuName = "Magic/Affinities/Heat")]
public class HeatAffinity : AffinityDefinition
{
    public float BurnDamage = 5f;
    public float BurnDuration = 4f;

    [Header("Visuals")]
    public Color TrailColor = Color.red; // Make this editable in Inspector
    public Material TrailMaterial;

    public override void Apply(SpellContext context)
    {
        if (context.Projectile == null)
            return;

        // Boost damage
        context.Projectile.Damage += context.Spell.Stats.Power * 0.2f;

        // Get or create damage behaviour
        var damageBehaviour = context.Projectile.GetBehaviour<DamageBehaviour>();
        if (damageBehaviour == null)
        {
            damageBehaviour = new DamageBehaviour();
            context.Projectile.AddBehaviour(damageBehaviour);
        }

        // Configure
        damageBehaviour.DamageType = DamageType.Fire;
        damageBehaviour.BurnDamage = BurnDamage;
        damageBehaviour.BurnDuration = BurnDuration;

        // Add trail with customizable color
        var trail = new TrailBehaviour
        {
            TrailColor = TrailColor,
            CustomTrailMaterial = TrailMaterial,
        };
        context.Projectile.AddBehaviour(trail);
    }
}
