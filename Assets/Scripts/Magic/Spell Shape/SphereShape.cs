using GameCode.Magic;
using UnityEngine;

[CreateAssetMenu(menuName = "Magic/Shapes/Sphere")]
public class SphereShape : ShapeDefinition
{
    public GameObject ProjectilePrefab;

    public override void Cast(SpellContext context)
    {
        if (ProjectilePrefab == null)
        {
            Debug.LogError("No projectile prefab assigned to SphereShape!");
            return;
        }

        // Spawn projectile
        GameObject projectileObj = Instantiate(
            ProjectilePrefab,
            context.CastOrigin.position,
            Quaternion.identity
        );

        Physics.IgnoreCollision(
            projectileObj.GetComponent<Collider>(),
            context.Caster.GetComponent<Collider>()
        );

        // Get or add SpellProjectile component
        SpellProjectile projectile = projectileObj.GetComponent<SpellProjectile>();
        if (projectile == null)
        {
            projectile = projectileObj.AddComponent<SpellProjectile>();
        }

        // Initialize with context
        projectile.Initialize(context);
        context.Projectile = projectile;

        // Apply affinity
        if (context.Spell.Affinity != null)
        {
            context.Spell.Affinity.Apply(context);
        }

        // Apply all modifiers
        foreach (var modifier in context.Spell.Modifiers)
        {
            modifier.Apply(context);
        }

        // Destroy after lifetime
        Destroy(projectileObj, context.Spell.Stats.Lifetime);
    }
}
