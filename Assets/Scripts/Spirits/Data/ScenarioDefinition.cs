using System;
using System.Collections.Generic;
using GameCode.Spirits.Data.Conditions;
using UnityEngine;

namespace GameCode.Spirits.Data
{
    public enum ConditionOperator { AND, OR, NOT }

    [Serializable]
    public class ConditionGroup
    {
        public ConditionOperator Operator = ConditionOperator.AND;

        [SerializeReference]
        public List<ConditionNode> Nodes = new List<ConditionNode>();
    }

    /// <summary>
    /// Represents a specific scenario where a triggering event evaluated against conditions
    /// produces a response from Spirits.
    /// </summary>
    [CreateAssetMenu(menuName = "Spirits/Data/Scenario Definition", fileName = "NewScenario")]
    public class ScenarioDefinition : ScriptableObject
    {
        [Tooltip("The unique ID for this scenario.")]
        [SerializeField] private string id;
        
        [Tooltip("The Event that triggers this scenario to evaluate.")]
        [SerializeField] private EventId triggerEvent;

        [Tooltip("The conditions that must be met for this scenario to execute.")]
        [SerializeField] private List<ConditionGroup> conditionGroups = new List<ConditionGroup>();

        [Header("Outcomes")]
        [Tooltip("The resulting communication intents or topics triggered by this scenario passing.")]
        [SerializeField] private List<TopicId> resultingTopics = new List<TopicId>();

        public ScenarioId Id => new ScenarioId(id);
        public EventId TriggerEvent => triggerEvent;
        public IReadOnlyList<ConditionGroup> ConditionGroups => conditionGroups;
        public IReadOnlyList<TopicId> ResultingTopics => resultingTopics;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                id = name;
            }
        }
    }
}
