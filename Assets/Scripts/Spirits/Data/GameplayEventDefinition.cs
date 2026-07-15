using UnityEngine;

namespace GameCode.Spirits.Data
{
    /// <summary>
    /// Editor-facing registry for a Gameplay Event.
    /// Used by Property Drawers to populate dropdowns for EventId.
    /// Not intended to be referenced directly by runtime core to maintain decoupling.
    /// </summary>
    [CreateAssetMenu(menuName = "Spirits/Data/Gameplay Event Definition", fileName = "NewEvent")]
    public class GameplayEventDefinition : ScriptableObject
    {
        [Tooltip("The ID used at runtime (e.g., 'PlayerLoadedGame').")]
        [SerializeField] private string id;

        [Tooltip("Display name for the Editor UI.")]
        [SerializeField] private string displayName;

        [Tooltip("Category for grouping in the Editor.")]
        [SerializeField] private string category = "General";

        [TextArea(3, 6)]
        [SerializeField] private string description;

        public EventId Id => new EventId(id);
        public string DisplayName => displayName;
        public string Category => category;
        public string Description => description;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                id = name;
            }
        }
    }
}
