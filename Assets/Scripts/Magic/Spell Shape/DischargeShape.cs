using GameCode.Magic;
using GameCode.Shared;
using UnityEngine;

[CreateAssetMenu(menuName = "Magic/Shapes/Spark/Discharge")]
public class DischargeShape : ShapeDefinition
{
    public float BlastRadius = 8f;
    public float BlastForce = 35f;
    public float UpwardForce = 10f;
    public float Damage = 50f;
    public GameObject ShockwavePrefab;

    public override void Cast(SpellContext context)
    {
        Vector3 center = context.Caster.transform.position;

        if (ShockwavePrefab != null)
        {
            Instantiate(ShockwavePrefab, center, Quaternion.identity);
        }

        Collider[] objects = Physics.OverlapSphere(center, BlastRadius);
        foreach (var obj in objects)
        {
            // Don't affect caster
            if (context.Caster != null && obj.transform.IsChildOf(context.Caster.transform)) continue;

            Rigidbody rb = obj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 direction = (obj.transform.position - center).normalized;
                direction.y += 0.3f;
                direction.Normalize();

                float distance = Vector3.Distance(obj.transform.position, center);
                float falloff = 1f - (distance / BlastRadius);

                Vector3 force = direction * BlastForce * falloff;
                force.y += UpwardForce * falloff;

                rb.AddForce(force, ForceMode.Impulse);
            }

            IDamageable damageable = obj.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(Damage, DamageType.Force);
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
