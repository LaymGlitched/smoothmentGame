#if UNITY_EDITOR || DEVELOPMENT_BUILD

using UnityEngine;
using GameCode.Spirits.Communication;
using GameCode.Spirits.Runtime;
using System.Collections.Generic;
using System.Linq;

namespace GameCode.Spirits.UI
{
    /// <summary>
    /// Developer tool to visualize the reasoning chain that produced a dialogue line.
    /// Has zero runtime cost in release builds.
    /// </summary>
    public class SpiritDebugOverlay : MonoBehaviour
    {
        [Tooltip("Toggle the debug overlay on/off.")]
        [SerializeField] private bool showOverlay = true;
        
        private readonly Dictionary<string, DialogueRequest> latestRequests = new Dictionary<string, DialogueRequest>();

        private void Start()
        {
            if (SpiritDialogueCoordinator.Instance != null)
            {
                // We hook into OnDialogueStarted to capture the final resolved request
                SpiritDialogueCoordinator.Instance.OnDialogueStarted += HandleDialogueStarted;
            }
        }

        private void OnDestroy()
        {
            if (SpiritDialogueCoordinator.Instance != null)
            {
                SpiritDialogueCoordinator.Instance.OnDialogueStarted -= HandleDialogueStarted;
            }
        }

        private void HandleDialogueStarted(DialogueRequest request)
        {
            latestRequests[request.SourceSpirit.Id] = request;
        }

        private void OnGUI()
        {
            if (!showOverlay) return;

            GUILayout.BeginArea(new Rect(10, 10, 400, Screen.height));
            
            foreach (var kvp in latestRequests)
            {
                var request = kvp.Value;
                var spirit = request.SourceSpirit;
                
                // Approximate the reasoning chain by looking at the most recent memory 
                // and highest intensity concern.
                var memory = spirit.Memory.RecentMemory.OrderByDescending(m => m.Timestamp).FirstOrDefault();
                var concern = spirit.Agency.ActiveConcerns.OrderByDescending(c => c.Intensity).FirstOrDefault();

                GUILayout.BeginVertical(GUI.skin.box);
                
                GUIStyle boldStyle = new GUIStyle(GUI.skin.label) { richText = true };
                GUILayout.Label($"<b>{spirit.Definition.DisplayName} - Reasoning Chain</b>", boldStyle);
                GUILayout.Space(5);
                
                // 1. Event & Memory
                if (memory.Timestamp > 0)
                {
                    GUILayout.Label($"Event ➔ {memory.RawEvent.GetType().Name}");
                    GUILayout.Label($"Memory ➔ Significance {memory.Significance:F2} [{memory.Tag}]");
                }
                
                // 2. Concern & Agency
                if (concern != null)
                {
                    GUILayout.Label($"Concern ➔ {concern.Subject} (Intensity: {concern.Intensity:F2})");
                }

                // 3. Intent & Communication
                GUILayout.Label($"Intent ➔ Topic: {request.Priority}");
                
                // 4. Resolution
                GUILayout.Label($"Dialogue Key ➔ {request.TextKey}");
                
                GUILayout.EndVertical();
            }

            GUILayout.EndArea();
        }
    }
}

#endif
