using UnityEngine;

namespace GameCode.Spirits.Data
{
    /// <summary>
    /// Editor-facing registry for a Topic.
    /// Used by Property Drawers to populate dropdowns for TopicId.
    /// Not intended to be referenced directly by runtime core to maintain decoupling.
    /// </summary>
    [CreateAssetMenu(menuName = "Spirits/Data/Topic Definition", fileName = "NewTopic")]
    public class TopicDefinition : ScriptableObject
    {
        [Tooltip("The ID used at runtime (e.g., 'VesselSafety').")]
        [SerializeField] private string id;

        [Tooltip("Display name for the Editor UI.")]
        [SerializeField] private string displayName;

        [Tooltip("Category for grouping in the Editor (e.g., 'Combat', 'Exploration').")]
        [SerializeField] private string category = "General";

        [TextArea(3, 6)]
        [SerializeField] private string description;

        [Tooltip("Color tint for the Topic in the node editor.")]
        [SerializeField] private Color editorColor = Color.white;

        public TopicId Id => new TopicId(id);
        public string DisplayName => displayName;
        public string Category => category;
        public string Description => description;
        public Color EditorColor => editorColor;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                id = name;
            }
        }
    }
}
