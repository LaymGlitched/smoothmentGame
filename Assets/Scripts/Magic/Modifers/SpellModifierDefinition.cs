using UnityEngine;

public abstract class SpellModifierDefinition : ScriptableObject
{
    public string DisplayName;

    public abstract void Apply(SpellContext context);
}
