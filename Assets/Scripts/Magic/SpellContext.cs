using System.Collections.Generic;
using GameCode.Magic;
using UnityEngine;

public class SpellContext
{
    // The spell being cast
    public Spell Spell;

    // Who cast it
    public GameObject Caster;
    public Transform CastOrigin;

    // Where the player is aiming
    public Vector3 Direction;
    public Vector3 TargetPoint;

    // Runtime values
    public float ChargeAmount;
    public float ManaUsed;

    // Useful references
    public LayerMask HitMask;

    // The object currently being built
    public SpellProjectile Projectile;

    // Temporary runtime values
    public Dictionary<string, object> Data = new();
}
