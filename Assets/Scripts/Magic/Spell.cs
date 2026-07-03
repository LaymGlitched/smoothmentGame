using System.Collections.Generic;
using UnityEngine;
using GameCode.Magic;

[System.Serializable]
[CreateAssetMenu(menuName = "Magic/Spell")]
public class Spell : ScriptableObject
{
    public string Name;

    public ShapeDefinition Shape;

    public AffinityDefinition Affinity;

    public List<SpellModifierDefinition> Modifiers = new();

    public List<MovementSpellOverride> MovementOverrides = new();

    public SpellStats Stats = new();
}
