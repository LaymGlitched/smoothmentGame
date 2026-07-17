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

    [Tooltip("Optional. If not set, falls back to the shape's projectile prefab if available.")]
    public GameObject HandVisualPrefab;

    public List<SpellModifierDefinition> Modifiers = new();

    public List<MovementSpellOverride> MovementOverrides = new();

    public SpellStats Stats = new();
}
