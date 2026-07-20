using GameCode.Magic;
using UnityEngine;

[CreateAssetMenu(menuName = "Magic/Shapes/Spark/Static Tether")]
public class StaticTetherShape : ShapeDefinition
{
    public float MaxRange = 20f;
    public float SphereRadius = 1.5f;
    public float PullForce = 40f; // Faster pull
    public float UpwardAssist = 8f;

    public override void Cast(SpellContext context)
    {
        Rigidbody casterRb = context.Caster.GetComponent<Rigidbody>();
        if (casterRb == null) return;

        RaycastHit[] hits = Physics.SphereCastAll(context.CastOrigin.position, SphereRadius, context.Direction, MaxRange, context.HitMask);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        bool hitFound = false;
        RaycastHit targetHit = new RaycastHit();

        foreach (var h in hits)
        {
            if (context.Caster != null && h.collider.transform.IsChildOf(context.Caster.transform)) continue;
            targetHit = h;
            hitFound = true;
            break;
        }

        // SphereCast for forgiving aim
        if (hitFound)
        {
            Vector3 hitPoint = targetHit.point;
            Vector3 pullDir = (hitPoint - casterRb.transform.position).normalized;

            // Halts fall for snappy feel
            Vector3 vel = casterRb.linearVelocity;
            if (vel.y < 0) vel.y = 0;
            casterRb.linearVelocity = vel;

            casterRb.AddForce(pullDir * PullForce + Vector3.up * UpwardAssist, ForceMode.Impulse);
            
            // Apply affinity and modifiers if any
            if (context.Spell.Affinity != null)
            {
                context.Spell.Affinity.Apply(context);
            }
            foreach (var modifier in context.ActiveModifiers)
            {
                modifier.Apply(context);
            }
        }
    }
}
