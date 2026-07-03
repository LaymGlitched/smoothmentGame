using GameCode.Magic;
using GameCode.Shared;
using UnityEngine;

[CreateAssetMenu(menuName = "Magic/Affinities/Force Dash")]
public class ForceDashAffinity : AffinityDefinition
{
    [Header("Dash Settings")]
    public float DashForce = 30f;
    public float UpwardForce = 2f;
    public float DashDuration = 0.3f;

    [Header("Visuals")]
    public Color DashColor = new Color(0.3f, 0.7f, 1f, 0.6f);
    public GameObject DashEffectPrefab;

    [Header("Direction")]
    public ForceAffinity.WindBlastDirection DirectionMode = ForceAffinity
        .WindBlastDirection
        .Forward;
    public Vector3 CustomDirection = Vector3.forward;

    public override void Apply(SpellContext context)
    {
        if (context.Projectile == null)
        {
            Debug.LogWarning("ForceDashAffinity: No projectile in context!");
            return;
        }

        // Get the caster
        GameObject caster = context.Caster;
        if (caster == null)
        {
            Debug.LogWarning("ForceDashAffinity: No caster in context!");
            return;
        }

        // Create a new dash behaviour
        var dashBehaviour = new ForceDashBehaviour
        {
            DashForce = DashForce,
            UpwardForce = UpwardForce,
            DashDuration = DashDuration,
            DashEffectPrefab = DashEffectPrefab,
            DashColor = DashColor,
            EffectDuration = 2f,
            DirectionMode = DirectionMode,
            CustomDirection = CustomDirection,
        };

        // Add the behaviour to the projectile
        context.Projectile.AddBehaviour(dashBehaviour);

        Debug.Log($"Applied Force Dash affinity! Force: {DashForce}, Direction: {DirectionMode}");
    }
}
