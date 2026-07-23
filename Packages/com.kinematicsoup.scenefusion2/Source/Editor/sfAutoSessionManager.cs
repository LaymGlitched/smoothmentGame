using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
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
    /// Manages automatic central session hosting and joining per scene using native SceneFusion 2 APIs.
    /// Hooks directly into Unity's scene change events to trigger immediate auto-connection on scene load.
    /// </summary>
    [InitializeOnLoad]
    public class sfAutoSessionManager : ksSingleton<sfAutoSessionManager>
    {
        private static bool m_initialized = false;
        private string m_currentSyncedScene = null;
        private bool m_isConnecting = false;

        static sfAutoSessionManager()
        {
            EditorApplication.delayCall += () =>
            {
                Get().StartAutoSync();
            };
        }

        /// <summary>Starts monitoring scene loading for central cloud session connection.</summary>
        public void StartAutoSync()
        {
            if (m_initialized)
            {
                return;
            }
            m_initialized = true;

            // Hook into all scene change events in Unity
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
            EditorSceneManager.sceneOpened += OnUnitySceneOpened;
            sfUnityEventDispatcher.Get().OnOpenScene += OnOpenScene;
            
            // Check current active scene immediately
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && !string.IsNullOrEmpty(activeScene.path))
            {
                EditorApplication.delayCall += () => AutoConnectForScene(activeScene);
            }
        }

        private void OnActiveSceneChanged(Scene current, Scene next)
        {
            if (!next.IsValid() || string.IsNullOrEmpty(next.path))
            {
                return;
            }
            ksLog.Info(this, "Active scene changed in Edit mode: " + next.name);
            AutoConnectForScene(next);
        }

        private void OnUnitySceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            {
                return;
            }
            ksLog.Info(this, "Unity scene opened: " + scene.name);
            AutoConnectForScene(scene);
        }

        private void OnOpenScene(Scene scene, OpenSceneMode mode)
        {
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            {
                return;
            }

            AutoConnectForScene(scene);
        }

        /// <summary>Auto-connects to or creates the KinematicSoup Cloud session for the given scene.</summary>
        public void AutoConnectForScene(Scene scene)
        {
            sfService service = SceneFusion.Get().Service;
            if (service == null)
            {
                return;
            }

            string targetScene = scene.name;

            // Reset connection lock if opening a new scene
            if (m_currentSyncedScene != targetScene)
            {
                m_isConnecting = false;
                if (service.IsConnected && service.Session != null)
                {
                    ksLog.Info(this, "Switching from '" + m_currentSyncedScene + "' to '" + targetScene + "'. Leaving previous cloud session...");
                    service.LeaveSession();
                }
            }
            else if (service.IsConnected && service.Session != null)
            {
                return;
            }

            if (m_isConnecting)
            {
                return;
            }

            m_isConnecting = true;
            m_currentSyncedScene = targetScene;

            // Validate KinematicSoup Web Token
            object webService = sfWebService.Get();
            string token = null;
            if (webService != null)
            {
                PropertyInfo tokenProp = webService.GetType().GetProperty("SFToken");
                if (tokenProp != null)
                {
                    token = tokenProp.GetValue(webService) as string;
                }
            }

            if (string.IsNullOrEmpty(token))
            {
                m_isConnecting = false;
                ksLog.Warning(this, "KinematicSoup Cloud token missing. Please log into your SceneFusion account in Unity.");
                PromptKinematicSoupLogin();
                return;
            }

            ksLog.Info(this, "Auto-connecting SceneFusion Cloud for scene: " + targetScene);
            ExecuteCloudSessionAutoConnect(service, targetScene);
        }

        /// <summary>Prompts user to log into KinematicSoup account if token is invalid or missing.</summary>
        private void PromptKinematicSoupLogin()
        {
            bool open = EditorUtility.DisplayDialog(
                "KinematicSoup Authentication Required",
                "KinematicSoup Cloud requires an active login token.\n\nPlease log into your KinematicSoup account in the Session window once so SceneFusion can automatically host and join cloud sessions for your scenes.",
                "Open Login Window",
                "Cancel"
            );

            if (open)
            {
                ksWindow.Open(ksWindow.SCENE_FUSION_MAIN, delegate (ksWindow window)
                {
                    window.titleContent = new GUIContent(" Session Login", KS.SceneFusion.sfTextures.Logo);
                    window.minSize = new Vector2(380f, 100f);
                    window.Menu = ScriptableObject.CreateInstance<sfSessionsMenu>();
                });
            }
        }

        private void ExecuteCloudSessionAutoConnect(sfService service, string sceneName)
        {
            try
            {
                int projectId = sfConfig.Get().ProjectId;
                string version = sfConfig.Get().Version.ToString();

                MethodInfo getSessionsMethod = service.GetType().GetMethod("GetSessions");
                if (getSessionsMethod != null)
                {
                    ParameterInfo[] pars = getSessionsMethod.GetParameters();
                    if (pars.Length == 3)
                    {
                        Type delegateType = pars[2].ParameterType;
                        MethodInfo handlerMethod = typeof(sfAutoSessionManager).GetMethod(nameof(OnGetSessionsCallback), BindingFlags.NonPublic | BindingFlags.Instance);
                        Delegate callback = Delegate.CreateDelegate(delegateType, this, handlerMethod, false);

                        if (callback != null)
                        {
                            getSessionsMethod.Invoke(service, new object[] { version, "Unity", callback });
                            return;
                        }
                    }
                }

                StartCloudCentralSession(service, projectId, version, sceneName);
            }
            catch (Exception ex)
            {
                m_isConnecting = false;
                ksLog.Warning(this, "ExecuteCloudSessionAutoConnect exception: " + ex.Message);
            }
        }

        private void OnGetSessionsCallback(sfSessionInfo[] sessions, string error)
        {
            m_isConnecting = false;
            sfService service = SceneFusion.Get().Service;
            if (service == null) return;

            string targetScene = m_currentSyncedScene;
            if (string.IsNullOrEmpty(targetScene)) return;

            int projectId = sfConfig.Get().ProjectId;
            string version = sfConfig.Get().Version.ToString();

            if (!string.IsNullOrEmpty(error) && error.Contains("Invalid token"))
            {
                ksLog.Warning(this, "KinematicSoup Cloud token invalid or expired. Prompting re-login...");
                PromptKinematicSoupLogin();
                return;
            }

            sfSessionInfo matchedSession = null;
            if (sessions != null)
            {
                foreach (sfSessionInfo sInfo in sessions)
                {
                    if (sInfo == null) continue;
                    
                    string sName = GetSessionSceneName(sInfo);
                    if (string.Equals(sName, targetScene, StringComparison.OrdinalIgnoreCase))
                    {
                        matchedSession = sInfo;
                        break;
                    }
                }
            }

            if (matchedSession != null)
            {
                ksLog.Info(this, "Found existing KinematicSoup Cloud session for scene '" + targetScene + "'. Joining automatically...");
                service.JoinSession(matchedSession);
            }
            else
            {
                ksLog.Info(this, "No cloud session found for scene '" + targetScene + "'. Hosting KinematicSoup Cloud session automatically...");
                StartCloudCentralSession(service, projectId, version, targetScene);
            }
        }

        private string GetSessionSceneName(sfSessionInfo sInfo)
        {
            if (sInfo == null) return null;
            try
            {
                PropertyInfo prop = sInfo.GetType().GetProperty("SceneName") ?? sInfo.GetType().GetProperty("Name") ?? sInfo.GetType().GetProperty("SessionName");
                if (prop != null) return prop.GetValue(sInfo) as string;

                FieldInfo field = sInfo.GetType().GetField("SceneName") ?? sInfo.GetType().GetField("Name");
                if (field != null) return field.GetValue(sInfo) as string;

                if (sInfo.RoomInfo != null) return sInfo.RoomInfo.Name;
            }
            catch { }
            return null;
        }

        private void StartCloudCentralSession(sfService service, int projectId, string version, string sceneName)
        {
            m_isConnecting = false;
            try
            {
                MethodInfo startSessionMethod = service.GetType().GetMethod("StartSession");
                if (startSessionMethod != null)
                {
                    startSessionMethod.Invoke(service, new object[] { projectId, version, version, "Unity", sceneName });
                }
            }
            catch (Exception ex)
            {
                ksLog.Warning(this, "StartCloudCentralSession exception: " + ex.Message);
            }
        }
    }
}
