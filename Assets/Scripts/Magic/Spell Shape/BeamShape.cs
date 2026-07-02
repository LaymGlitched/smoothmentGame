using GameCode.Magic;
using UnityEngine;

[CreateAssetMenu(menuName = "Magic/Shapes/Beam")]
public class BeamShape : ShapeDefinition
{
    public GameObject BeamPrefab;
    public float BeamDuration = 0.5f;
    public float MaxRange = 30f;

    public override void Cast(SpellContext context)
    {
        if (BeamPrefab == null)
        {
            Debug.LogError("No beam prefab assigned to BeamShape!");
            return;
        }

        // Spawn beam
        GameObject beamObj = Instantiate(
            BeamPrefab,
            context.CastOrigin.position,
            Quaternion.identity
        );

        // Get beam component (you'd create this)
        // Beam beam = beamObj.GetComponent<Beam>();
        // if (beam != null)
        // {
        //     beam.Initialize(context);
        //     beam.MaxRange = MaxRange;
        // }

        // Apply affinity and modifiers
        if (context.Spell.Affinity != null)
        {
            context.Spell.Affinity.Apply(context);
        }

        foreach (var modifier in context.Spell.Modifiers)
        {
            modifier.Apply(context);
        }

        // Destroy after duration
        Destroy(beamObj, BeamDuration);
    }
}
