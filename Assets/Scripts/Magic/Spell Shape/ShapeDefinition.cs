using UnityEngine;

public abstract class ShapeDefinition : ScriptableObject
{
    public string DisplayName;
    public GameObject ProjectilePrefab;

    public abstract void Cast(SpellContext context);
}
