using GameCode.Magic;
using UnityEngine;

[CreateAssetMenu(menuName = "Magic/Shapes/Dash")]
public class DashShape : ShapeDefinition
{
    [Header("Dash Settings")]
    public float DashForce = 30f;
    public float UpwardForce = 2f;
    public float DashDuration = 0.3f;

    [Header("Visuals")]
    public GameObject DashEffectPrefab;
    public Color DashColor = new Color(0.3f, 0.7f, 1f, 0.6f);
    public float EffectDuration = 1f;

    [Header("Direction")]
    public ForceAffinity.WindBlastDirection DirectionMode = ForceAffinity
        .WindBlastDirection
        .Forward;
    public Vector3 CustomDirection = Vector3.forward;

    public override void Cast(SpellContext context)
    {
        // Get the caster
        GameObject caster = context.Caster;
        if (caster == null)
        {
            Debug.LogError("DashShape: No caster in context!");
            return;
        }

        // Create a temporary projectile to hold the behaviour
        GameObject tempProjectile = new GameObject("DashProjectile");
        tempProjectile.transform.position = caster.transform.position;

        // Add SpellProjectile component
        SpellProjectile projectile = tempProjectile.AddComponent<SpellProjectile>();
        projectile.Spell = context.Spell;
        projectile.Caster = caster;
        projectile.Direction = GetDashDirection(caster);
        projectile.Speed = 0f; // No movement needed
        projectile.Lifetime = 0.1f; // Short lifetime

        context.Projectile = projectile;

        // Add the dash behaviour
        var dashBehaviour = new ForceDashBehaviour
        {
            DashForce = DashForce,
            UpwardForce = UpwardForce,
            DashDuration = DashDuration,
            DashEffectPrefab = DashEffectPrefab,
            DashColor = DashColor,
            EffectDuration = EffectDuration,
            DirectionMode = DirectionMode,
            CustomDirection = CustomDirection,
        };

        projectile.AddBehaviour(dashBehaviour);

        // Apply affinity
        if (context.Spell.Affinity != null)
        {
            context.Spell.Affinity.Apply(context);
        }

        // Destroy the temp projectile after a frame
        GameObject.Destroy(tempProjectile, 0.1f);

        Debug.Log($"Cast Dash spell! Force: {DashForce}, Direction: {DirectionMode}");
    }

    private Vector3 GetDashDirection(GameObject caster)
    {
        switch (DirectionMode)
        {
            case ForceAffinity.WindBlastDirection.Forward:
                return caster.transform.forward;

            case ForceAffinity.WindBlastDirection.Upward:
                return Vector3.up;

            case ForceAffinity.WindBlastDirection.Downward:
                return Vector3.down;

            case ForceAffinity.WindBlastDirection.Radial:
                return caster.transform.forward;

            case ForceAffinity.WindBlastDirection.Custom:
                return CustomDirection.normalized;

            default:
                return caster.transform.forward;
        }
    }
}
