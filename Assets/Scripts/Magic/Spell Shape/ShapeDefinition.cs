using UnityEngine;

public abstract class ShapeDefinition : ScriptableObject
{
    public string DisplayName;

    public abstract void Cast(SpellContext context);
}
