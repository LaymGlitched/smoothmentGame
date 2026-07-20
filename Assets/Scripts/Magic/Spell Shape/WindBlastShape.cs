using GameCode.Magic;
using GameCode.Shared;
using UnityEngine;

[CreateAssetMenu(menuName = "Magic/Shapes/Wind Blast")]
public class WindBlastShape : ShapeDefinition
{
    public GameObject WindEffectPrefab;
    public float BlastRadius = 10f;
    public float BlastForce = 20f;
    public float UpwardForce = 5f;
    public Color WindColor = new Color(0.3f, 0.7f, 1f, 0.6f);

    [Header("Particles")]
    public int ParticleCount = 100;
    public float ParticleLifetime = 2f;

    public override void Cast(SpellContext context)
    {
        // Get the ground position below the caster
        Vector3 casterPosition = context.Caster.transform.position;
        Vector3 groundPosition = GetGroundPosition(casterPosition);

        // Create the wind blast effect
        CreateWindBlastEffect(groundPosition, context);

        // Apply force to ALL objects in radius (including the player!)
        ApplyForceToObjects(groundPosition, context);

        // Apply affinity
        if (context.Spell.Affinity != null)
        {
            context.Spell.Affinity.Apply(context);
        }

        Debug.Log($"Cast wind blast at {groundPosition} with radius {BlastRadius}");
    }

    private Vector3 GetGroundPosition(Vector3 position)
    {
        RaycastHit hit;
        if (Physics.Raycast(position, Vector3.down, out hit, 10f))
        {
            return hit.point;
        }
        return position + Vector3.down * 0.5f;
    }

    private void CreateWindBlastEffect(Vector3 position, SpellContext context)
    {
        if (WindEffectPrefab != null)
        {
            GameObject effect = GameObject.Instantiate(
                WindEffectPrefab,
                position,
                Quaternion.identity
            );

            effect.transform.localScale = Vector3.one * (BlastRadius / 5f);

            var renderers = effect.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                if (renderer.material != null)
                {
                    renderer.material.color = WindColor;
                }
            }

            GameObject.Destroy(effect, ParticleLifetime);
        }
        else
        {
            CreateParticleEffect(position);
        }
    }

    private void CreateParticleEffect(Vector3 position)
    {
        GameObject particleObj = new GameObject("WindBlastEffect");
        particleObj.transform.position = position;

        ParticleSystem ps = particleObj.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.startLifetime = ParticleLifetime;
        main.startSpeed = 10f;
        main.startSize = 0.5f;
        main.maxParticles = ParticleCount;
        main.startColor = WindColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = BlastRadius;
        shape.radiusThickness = 0.5f;

        var emission = ps.emission;
        emission.SetBursts(
            new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, ParticleCount) }
        );

        var force = ps.forceOverLifetime;
        force.enabled = true;
        force.x = new ParticleSystem.MinMaxCurve(0f, 10f);
        force.y = new ParticleSystem.MinMaxCurve(5f, 20f);
        force.z = new ParticleSystem.MinMaxCurve(0f, 10f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 2f;
        noise.frequency = 1f;

        var renderer = ps.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
            renderer.material.color = WindColor;
        }

        ps.Play();
        GameObject.Destroy(particleObj, ParticleLifetime + 1f);
    }

    private void ApplyForceToObjects(Vector3 center, SpellContext context)
    {
        // Get ALL objects in radius (including the player!)
        Collider[] objects = Physics.OverlapSphere(center, BlastRadius);

        foreach (var obj in objects)
        {
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

                Debug.Log($"Applied {force.magnitude} force to {obj.name}");
            }

            IDamageable damageable = obj.GetComponent<IDamageable>();
            if (damageable != null)
            {
                float damage = BlastForce * 0.05f;
                damageable.TakeDamage(damage, DamageType.Force);
            }
        }
    }
}
