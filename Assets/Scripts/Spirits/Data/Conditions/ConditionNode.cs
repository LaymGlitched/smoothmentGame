using System;
using UnityEngine;

namespace GameCode.Spirits.Data.Conditions
{
    /// <summary>
    /// Base class for all condition nodes in a Scenario graph.
    /// Derived types can be serialized using [SerializeReference] for polymorphism.
    /// </summary>
    [Serializable]
    public abstract class ConditionNode
    {
        [HideInInspector] public Vector2 EditorPosition;

        /// <summary>
        /// Evaluates whether this condition is met.
        /// </summary>
        public abstract bool Evaluate(Runtime.Spirit contextSpirit = null);
    }
}
