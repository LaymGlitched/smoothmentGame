using GameCode.Magic;
using GameCode.Shared;
using UnityEngine;

[CreateAssetMenu(menuName = "Magic/Shapes/Spark/Pulse Bolt")]
public class PulseBoltShape : ShapeDefinition
{
    public float MaxRange = 50f;
    public float KnockbackForce = 30f;
    public float Damage = 100f; // High enough to kill fodder
    public float UpwardRecoil = 4f; // Upward bump for kickstand
    public GameObject HitEffectPrefab;

    public override void Cast(SpellContext context)
    {
        // Kickstand: Halts fall
        Rigidbody casterRb = context.Caster.GetComponent<Rigidbody>();
        if (casterRb != null)
        {
            Vector3 vel = casterRb.linearVelocity;
            if (vel.y < 0)
            {
                vel.y = 0;
                casterRb.linearVelocity = vel;
                casterRb.AddForce(Vector3.up * UpwardRecoil, ForceMode.Impulse);
            }
        }

        // Raycast
        RaycastHit[] hits = Physics.RaycastAll(context.CastOrigin.position, context.Direction, MaxRange, context.HitMask);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        bool hitFound = false;
        RaycastHit hit = new RaycastHit();
        foreach (var h in hits)
        {
            if (context.Caster != null && h.collider.transform.IsChildOf(context.Caster.transform)) continue;
            hit = h;
            hitFound = true;
            break;
        }

        if (hitFound)
        {
            // Spawn hit effect
            if (HitEffectPrefab != null)
            {
                Instantiate(HitEffectPrefab, hit.point, Quaternion.LookRotation(hit.normal));
            }

            // Handle hit
            GameObject target = hit.collider.gameObject;

            IDamageable damageable = target.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(Damage, DamageType.Force);
            }

            Rigidbody targetRb = target.GetComponent<Rigidbody>();
            if (targetRb != null)
            {
                targetRb.AddForce(context.Direction * KnockbackForce, ForceMode.Impulse);
            }
        }

        // Apply affinity and modifiers
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
