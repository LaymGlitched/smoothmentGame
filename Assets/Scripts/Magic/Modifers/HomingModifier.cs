using GameCode.Magic;
using UnityEngine;

[CreateAssetMenu(menuName = "Magic/Modifiers/Homing")]
public class HomingModifier : SpellModifierDefinition
{
    public float TurnSpeed = 10f;
    public float DetectionRadius = 20f;
    public LayerMask TargetMask = -1;

    public override void Apply(SpellContext context)
    {
        if (context.Projectile == null)
            return;

        var homing = new HomingBehaviour
        {
            TurnSpeed = TurnSpeed,
            DetectionRadius = DetectionRadius,
            TargetMask = TargetMask,
        };

        context.Projectile.AddBehaviour(homing);
    }
}
