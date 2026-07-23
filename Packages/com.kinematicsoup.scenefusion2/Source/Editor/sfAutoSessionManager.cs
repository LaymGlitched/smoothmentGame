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
    /// for that scene, or creates it if it doesn't exist yet.
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

            // If switching scenes, leave existing session first if connected
            if (service.IsConnected && service.Session != null)
            {
                if (m_currentSyncedScene != scene.name)
                {
                    ksLog.Info(this, "Switching scene to '" + scene.name + "'. Leaving previous session...");
                    service.LeaveSession();
                }
                else
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
                int projectId = sfConfig.Get().ProjectId;

                // Log available methods for diagnostic inspection
                ksLog.Info(this, "Inspecting sfService and sfWebService methods for session creation/joining...");

                // Method search on sfService and sfWebService
                bool attempted = TryInvokeSessionConnect(service, webService, projectId, sessionName);
                if (!attempted)
                {
                    ksLog.Warning(this, "Could not find standard Create/Join session method. Retrying...");
                    m_isConnecting = false;
                }
            }
            catch (Exception ex)
            {
                m_isConnecting = false;
                ksLog.Warning(this, "ExecuteCentralSessionConnect error: " + ex.Message);
            }
        }

        private bool TryInvokeSessionConnect(sfService service, object webService, int projectId, string sessionName)
        {
            Type serviceType = service.GetType();
            Type webType = webService != null ? webService.GetType() : null;

            // Search service methods for Join or Create
            MethodInfo[] serviceMethods = serviceType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            foreach (MethodInfo m in serviceMethods)
            {
                ParameterInfo[] p = m.GetParameters();
                if (m.Name.Equals("CreateSession", StringComparison.OrdinalIgnoreCase) || m.Name.Equals("JoinSession", StringComparison.OrdinalIgnoreCase))
                {
                    if (p.Length == 1 && p[0].ParameterType == typeof(string))
                    {
                        ksLog.Info(this, "Invoking " + serviceType.Name + "." + m.Name + "(\"" + sessionName + "\")");
                        m.Invoke(service, new object[] { sessionName });
                        m_isConnecting = false;
                        return true;
                    }
                    else if (p.Length == 2 && p[0].ParameterType == typeof(int) && p[1].ParameterType == typeof(string))
                    {
                        ksLog.Info(this, "Invoking " + serviceType.Name + "." + m.Name + "(" + projectId + ", \"" + sessionName + "\")");
                        m.Invoke(service, new object[] { projectId, sessionName });
                        m_isConnecting = false;
                        return true;
                    }
                }
            }

            if (webType != null)
            {
                MethodInfo[] webMethods = webType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                foreach (MethodInfo m in webMethods)
                {
                    ParameterInfo[] p = m.GetParameters();
                    if (m.Name.Equals("CreateSession", StringComparison.OrdinalIgnoreCase) || m.Name.Equals("JoinSession", StringComparison.OrdinalIgnoreCase))
                    {
                        if (p.Length == 1 && p[0].ParameterType == typeof(string))
                        {
                            ksLog.Info(this, "Invoking " + webType.Name + "." + m.Name + "(\"" + sessionName + "\")");
                            m.Invoke(webService, new object[] { sessionName });
                            m_isConnecting = false;
                            return true;
                        }
                        else if (p.Length == 2 && p[0].ParameterType == typeof(int) && p[1].ParameterType == typeof(string))
                        {
                            ksLog.Info(this, "Invoking " + webType.Name + "." + m.Name + "(" + projectId + ", \"" + sessionName + "\")");
                            m.Invoke(webService, new object[] { projectId, sessionName });
                            m_isConnecting = false;
                            return true;
                        }
                    }
                }
            }

            m_isConnecting = false;
            return false;
        }
    }
}
