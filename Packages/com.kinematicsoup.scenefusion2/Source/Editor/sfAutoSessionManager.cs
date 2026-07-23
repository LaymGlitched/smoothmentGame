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
    /// Manages automatic central session hosting and joining per scene.
    /// When any user opens a scene, SceneFusion automatically joins the central session
    /// for that scene, or creates it if it doesn't exist yet. No manual lobby/joining required.
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

        /// <summary>Starts monitoring scene loading for central auto session connection.</summary>
        public void StartAutoSync()
        {
            if (m_initialized)
            {
                return;
            }
            m_initialized = true;

            sfUnityEventDispatcher.Get().OnOpenScene += OnOpenScene;
            
            // Check active scene on startup
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && !string.IsNullOrEmpty(activeScene.path))
            {
                EditorApplication.delayCall += () => AutoConnectForScene(activeScene);
            }
        }

        /// <summary>Triggered when a scene is opened in Unity Editor.</summary>
        private void OnOpenScene(Scene scene, OpenSceneMode mode)
        {
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            {
                return;
            }

            AutoConnectForScene(scene);
        }

        /// <summary>Auto-connects to or creates the central session for the given scene.</summary>
        public void AutoConnectForScene(Scene scene)
        {
            sfService service = SceneFusion.Get().Service;
            if (service == null)
            {
                return;
            }

            string centralSessionName = scene.name;

            // If already connected to a session for this scene, return
            if (service.IsConnected && service.Session != null)
            {
                if (m_currentSyncedScene == scene.name)
                {
                    return;
                }
            }

            if (m_isConnecting)
            {
                return;
            }

            m_currentSyncedScene = scene.name;
            ksLog.Info(this, "Auto-connecting to central session for scene: " + centralSessionName);

            ExecuteCentralSessionConnect(service, centralSessionName);
        }

        /// <summary>Finds existing central session on server to join, or hosts it if not found.</summary>
        private void ExecuteCentralSessionConnect(sfService service, string sessionName)
        {
            m_isConnecting = true;
            try
            {
                object webService = sfWebService.Get();
                if (webService == null)
                {
                    m_isConnecting = false;
                    return;
                }

                Type webType = webService.GetType();
                int projectId = sfConfig.Get().ProjectId;

                // Find session fetching methods on WebService or sfService
                MethodInfo getSessionsMethod = null;
                foreach (MethodInfo m in webType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
                {
                    if (m.Name.Equals("GetSessions", StringComparison.OrdinalIgnoreCase) || 
                        m.Name.Equals("FetchSessions", StringComparison.OrdinalIgnoreCase))
                    {
                        getSessionsMethod = m;
                        break;
                    }
                }

                if (getSessionsMethod != null)
                {
                    ParameterInfo[] pars = getSessionsMethod.GetParameters();
                    if (pars.Length >= 2)
                    {
                        Type delegateType = pars[1].ParameterType;
                        if (typeof(MulticastDelegate).IsAssignableFrom(delegateType))
                        {
                            MethodInfo handlerMethod = typeof(sfAutoSessionManager).GetMethod(nameof(OnSessionsRetrieved), BindingFlags.NonPublic | BindingFlags.Instance);
                            Delegate callback = Delegate.CreateDelegate(delegateType, this, handlerMethod, false);

                            if (callback != null)
                            {
                                getSessionsMethod.Invoke(webService, new object[] { projectId, callback });
                                return;
                            }
                        }
                    }
                }

                // If no delegate method matched, fallback to direct session join/create attempt
                TryDirectSessionCreateOrJoin(service, sessionName, webService);
            }
            catch (Exception ex)
            {
                m_isConnecting = false;
                ksLog.Info(this, "ExecuteCentralSessionConnect note: " + ex.Message);
            }
        }

        /// <summary>Fallback direct session creation/joining.</summary>
        private void TryDirectSessionCreateOrJoin(sfService service, string targetName, object webService)
        {
            m_isConnecting = false;
            try
            {
                ksLog.Info(this, "Auto-creating central session for '" + targetName + "'...");
                MethodInfo createMethod = webService?.GetType().GetMethod("CreateSession") ?? service.GetType().GetMethod("CreateSession");
                if (createMethod != null)
                {
                    ParameterInfo[] pInfo = createMethod.GetParameters();
                    object[] args = new object[pInfo.Length];
                    if (args.Length > 0) args[0] = sfConfig.Get().ProjectId;
                    if (args.Length > 1) args[1] = targetName;
                    createMethod.Invoke(createMethod.IsStatic ? null : (createMethod.DeclaringType.IsAssignableFrom(service.GetType()) ? (object)service : webService), args);
                }
            }
            catch (Exception ex)
            {
                ksLog.Warning(this, "TryDirectSessionCreateOrJoin exception: " + ex.Message);
            }
        }

        /// <summary>Callback executed when session list is returned from SceneFusion web service.</summary>
        private void OnSessionsRetrieved(object sessionsListObj, string error)
        {
            m_isConnecting = false;
            sfService service = SceneFusion.Get().Service;
            if (service == null) return;

            string targetName = m_currentSyncedScene;
            if (string.IsNullOrEmpty(targetName)) return;

            object foundSession = null;
            if (sessionsListObj is IEnumerable list)
            {
                foreach (object sInfo in list)
                {
                    if (sInfo == null) continue;
                    PropertyInfo nameProp = sInfo.GetType().GetProperty("Name") ?? sInfo.GetType().GetProperty("SessionName") ?? sInfo.GetType().GetProperty("RoomName");
                    FieldInfo nameField = sInfo.GetType().GetField("Name") ?? sInfo.GetType().GetField("SessionName");
                    
                    string sName = null;
                    if (nameProp != null) sName = nameProp.GetValue(sInfo) as string;
                    else if (nameField != null) sName = nameField.GetValue(sInfo) as string;

                    if (sName != null && string.Equals(sName, targetName, StringComparison.OrdinalIgnoreCase))
                    {
                        foundSession = sInfo;
                        break;
                    }
                }
            }

            if (foundSession != null)
            {
                ksLog.Info(this, "Central session for '" + targetName + "' found. Joining automatically...");
                MethodInfo joinMethod = service.GetType().GetMethod("JoinSession", new Type[] { foundSession.GetType() });
                if (joinMethod != null)
                {
                    joinMethod.Invoke(service, new object[] { foundSession });
                }
            }
            else
            {
                ksLog.Info(this, "No central session found for '" + targetName + "'. Creating central session automatically...");
                object webService = sfWebService.Get();
                TryDirectSessionCreateOrJoin(service, targetName, webService);
            }
        }
    }
}
