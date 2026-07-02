using UnityEngine;

public abstract class AffinityDefinition : ScriptableObject
{
    [Header("Info")]
    public string DisplayName;
    public Sprite Icon;

    /// <summary>
    /// Called after the spell has been created.
    /// Modify the spell however you'd like.
    /// </summary>
    public abstract void Apply(SpellContext context);
}
