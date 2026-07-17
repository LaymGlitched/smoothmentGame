using UnityEngine;

public abstract class ShapeDefinition : ScriptableObject
{
    public string DisplayName;
    public GameObject ProjectilePrefab;

    [Header("Hand Visual Settings")]
    public Vector3 HandPositionOffset = Vector3.zero;
    public Vector3 HandRotationOffset = Vector3.zero;
    public Vector3 HandScale = Vector3.one;

    public abstract void Cast(SpellContext context);
}
