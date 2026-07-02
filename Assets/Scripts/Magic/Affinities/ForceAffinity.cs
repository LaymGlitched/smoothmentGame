using GameCode.Magic;
using GameCode.Shared;
using UnityEngine;

[CreateAssetMenu(menuName = "Magic/Affinities/Force")]
public class ForceAffinity : AffinityDefinition
{
    public enum WindBlastDirection
    {
        Radial, // Push outward from impact point (default)
        Forward, // Push in the direction the projectile was traveling
        Upward, // Push upward only
        Downward, // Push downward only
        Custom, // Use custom direction
    }

    [Header("Force Settings")]
    public float KnockbackForce = 15f;
    public float UpwardForce = 5f;
    public float ForceRadius = 5f;
    public float WindForce = 10f;
    public float WindRadius = 8f;

    [Header("Visuals")]
    public Color WindColor = new Color(0.5f, 0.8f, 1f, 0.5f);
    public GameObject WindEffectPrefab;

    [Header("Wind Blast (Ground Cast)")]
    public bool IsWindBlast = false;
    public float BlastForce = 20f;

    [Header("Wind Blast Direction")]
    public WindBlastDirection BlastDirection = WindBlastDirection.Radial;
    public Vector3 CustomDirection = Vector3.forward;

    public override void Apply(SpellContext context)
    {
        if (context.Projectile == null)
        {
            Debug.LogWarning("ForceAffinity: No projectile in context!");
            return;
        }

        if (IsWindBlast)
        {
            var windBlast = new WindBlastBehaviour
            {
                BlastForce = BlastForce,
                UpwardForce = UpwardForce,
                BlastRadius = WindRadius,
                WindColor = WindColor,
                WindEffectPrefab = WindEffectPrefab,
                BlastDirection = BlastDirection,
                CustomDirection = CustomDirection,
            };
            context.Projectile.AddBehaviour(windBlast);
        }
        else
        {
            var forceBehaviour = new ForceBehaviour
            {
                KnockbackForce = KnockbackForce,
                UpwardForce = UpwardForce,
                ForceRadius = ForceRadius,
                DamageType = DamageType.Force,
                WindForce = WindForce,
                WindRadius = WindRadius,
                WindColor = WindColor,
                WindEffectPrefab = WindEffectPrefab,
                CreateWindEffect = true,
                BlastDirection = BlastDirection,
                CustomDirection = CustomDirection,
            };
            context.Projectile.AddBehaviour(forceBehaviour);
        }
    }
}
