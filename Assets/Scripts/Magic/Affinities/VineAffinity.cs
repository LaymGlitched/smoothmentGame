using GameCode.Magic;
using UnityEngine;

[CreateAssetMenu(menuName = "Magic/Affinities/Vine")]
public class VineAffinity : AffinityDefinition
{
    [Header("Vine Pull Settings")]
    public float PullForceCaster = 20f;
    public float PullForceTarget = 20f;
    public float UpwardAssist = 5f;

    public override void Apply(SpellContext context)
    {
        if (context.Projectile == null) return;

        Rigidbody casterRb = null;
        if (context.Caster != null)
        {
            casterRb = context.Caster.GetComponent<Rigidbody>();
        }

        var pullBehaviour = new VinePullBehaviour
        {
            PullForceCaster = PullForceCaster,
            PullForceTarget = PullForceTarget,
            UpwardAssist = UpwardAssist,
            CasterRb = casterRb
        };
        context.Projectile.AddBehaviour(pullBehaviour);
    }
}
