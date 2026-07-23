using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NanoCollab
{
    /// <summary>
    /// Entry point. Creates the SessionManager on editor load and wires up
    /// scene change / quit callbacks. This is the only [InitializeOnLoad] class.
    /// </summary>
    [InitializeOnLoad]
    public static class NanoCollabBoot
    {
        private static SessionManager _session;

        static NanoCollabBoot()
        {
            // Defer initialization to first update to avoid editor startup race conditions
            EditorApplication.update += Initialize;
        }

        private static void Initialize()
        {
            EditorApplication.update -= Initialize;

            if (!NanoCollabSettings.instance.Enabled)
            {
                Debug.Log("[NanoCollab] Disabled in preferences. Skipping initialization.");
                return;
            }

            _session = new SessionManager();

            EditorApplication.update += OnUpdate;
            EditorApplication.quitting += OnQuitting;

            // Scene change detection
            EditorSceneManager.activeSceneChangedInEditMode += OnSceneChanged;

            // Trigger initial scene check
            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid() && !string.IsNullOrEmpty(scene.path))
                _session.OnSceneChanged();

            Debug.Log("[NanoCollab] Initialized. Open a scene to start collaborating.");
        }

        private static void OnUpdate()
        {
            _session?.Tick();
        }

        private static void OnSceneChanged(Scene oldScene, Scene newScene)
        {
            _session?.OnSceneChanged();
        }

        private static void OnQuitting()
        {
            _session?.Dispose();
            _session = null;
        }
    }
}
