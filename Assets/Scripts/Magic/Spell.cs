using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "Magic/Spell")]
public class Spell : ScriptableObject
{
    public string Name;

    public ShapeDefinition Shape;

    public AffinityDefinition Affinity;

    public List<SpellModifierDefinition> Modifiers = new();

    public SpellStats Stats = new();
}
