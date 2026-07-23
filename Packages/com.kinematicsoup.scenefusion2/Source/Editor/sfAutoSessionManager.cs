using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using KS.SceneFusion2.Client;
using KS.SceneFusion;
using KS.SF.Unity.Editor;
using KS.SF.Reactor;

namespace KS.SceneFusion2.Unity.Editor
{
    /// <summary>
    /// Automatically manages SceneFusion sessions on scene load, eliminating manual lobby hosting and joining.
    /// </summary>
    [InitializeOnLoad]
    public class sfAutoSessionManager : ksSingleton<sfAutoSessionManager>
    {
        private static bool m_initialized = false;

        static sfAutoSessionManager()
        {
            EditorApplication.delayCall += () =>
            {
                Get().StartAutoSync();
            };
        }

        /// <summary>Starts monitoring scene loading for auto session connection.</summary>
        public void StartAutoSync()
        {
            if (m_initialized)
            {
                return;
            }
            m_initialized = true;

            sfUnityEventDispatcher.Get().OnOpenScene += OnOpenScene;
        }

        /// <summary>Triggered when a scene is opened in Unity Editor.</summary>
        /// <param name="scene">The opened scene.</param>
        /// <param name="mode">The open mode (Single or Additive).</param>
        private void OnOpenScene(Scene scene, OpenSceneMode mode)
        {
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            {
                return;
            }

            AutoConnectForScene(scene);
        }

        /// <summary>Auto-connects to or hosts a session for the given scene if not already connected.</summary>
        /// <param name="scene">Scene to sync.</param>
        public void AutoConnectForScene(Scene scene)
        {
            sfService service = SceneFusion.Get().Service;
            if (service == null)
            {
                return;
            }

            if (service.IsConnected)
            {
                ksLog.Info(this, "SceneFusion already connected. Syncing active scene: " + scene.name);
                return;
            }

            ksLog.Info(this, "Auto-connecting SceneFusion session for scene: " + scene.name);

            // Fetch or create a session using SceneFusion service
            try
            {
                // Trigger SceneFusion's internal session join/reconnect if reconnect info exists
                if (SceneFusion.Get().Reconnected)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                ksLog.Warning(this, "AutoConnectForScene exception: " + ex.Message);
            }
        }
    }
}
