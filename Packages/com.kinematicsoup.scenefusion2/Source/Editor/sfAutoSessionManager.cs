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
    /// </summary>
    [InitializeOnLoad]
    public class sfAutoSessionManager : ksSingleton<sfAutoSessionManager>
    {
        private static bool m_initialized = false;
        private string m_currentSyncedScene = null;
        private bool m_isConnecting = false;
        private bool m_dumpedServiceApi = false;

        static sfAutoSessionManager()
        {
            EditorApplication.delayCall += () =>
            {
                Get().StartAutoSync();
            };
        }

        public void StartAutoSync()
        {
            if (m_initialized)
            {
                return;
            }
            m_initialized = true;

            sfService service = SceneFusion.Get().Service;
            if (service != null)
            {
                service.OnConnect += OnSessionConnectResult;
                service.OnDisconnect += OnSessionDisconnectResult;
            }

            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
            EditorSceneManager.sceneOpened += OnUnitySceneOpened;
            sfUnityEventDispatcher.Get().OnOpenScene += OnOpenScene;
            
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && !string.IsNullOrEmpty(activeScene.path))
            {
                EditorApplication.delayCall += () => AutoConnectForScene(activeScene);
            }
        }

        private void OnSessionConnectResult(sfSession session, string errorMessage)
        {
            m_isConnecting = false;
            if (session != null)
            {
                ksLog.Info(this, "Auto-Sync CONNECTED for scene: " + (m_currentSyncedScene ?? "unknown"));
            }
            else
            {
                ksLog.Warning(this, "Auto-Sync connection failed: " + (errorMessage ?? "unknown error"));
                m_currentSyncedScene = null;
            }
        }

        private void OnSessionDisconnectResult(sfSession session, string errorMessage)
        {
            m_isConnecting = false;
        }

        private void OnActiveSceneChanged(Scene current, Scene next)
        {
            if (next.IsValid() && !string.IsNullOrEmpty(next.path))
            {
                EditorApplication.delayCall += () => AutoConnectForScene(next);
            }
        }

        private void OnUnitySceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (scene.IsValid() && !string.IsNullOrEmpty(scene.path))
            {
                EditorApplication.delayCall += () => AutoConnectForScene(scene);
            }
        }

        private void OnOpenScene(Scene scene, OpenSceneMode mode)
        {
            if (scene.IsValid() && !string.IsNullOrEmpty(scene.path))
            {
                EditorApplication.delayCall += () => AutoConnectForScene(scene);
            }
        }

        public void AutoConnectForScene(Scene scene)
        {
            sfService service = SceneFusion.Get().Service;
            if (service == null) return;

            string targetScene = scene.name;

            // One-time API dump to understand the service internals
            if (!m_dumpedServiceApi)
            {
                m_dumpedServiceApi = true;
                DumpServiceApi(service);
            }

            // If we're already connected to a different scene, disconnect first
            if (m_currentSyncedScene != null && m_currentSyncedScene != targetScene)
            {
                m_isConnecting = false;
                if (service.IsConnected && service.Session != null)
                {
                    ksLog.Info(this, "Switching to '" + targetScene + "'. Leaving previous session...");
                    service.LeaveSession();
                }
            }
            else if (service.IsConnected && service.Session != null && m_currentSyncedScene == targetScene)
            {
                return;
            }

            if (service.IsConnecting || service.IsStartingSession || service.IsJoiningSession || m_isConnecting)
            {
                return;
            }

            // Check SF token
            if (service.WebService == null)
            {
                ksLog.Warning(this, "WebService not initialised. Cannot auto-connect.");
                return;
            }

            string sfToken = service.WebService.SFToken;
            if (string.IsNullOrEmpty(sfToken))
            {
                ksLog.Info(this, "No SF token available. Please log in via Window > Scene Fusion > Session.");
                return;
            }

            m_isConnecting = true;
            m_currentSyncedScene = targetScene;
            ksLog.Info(this, "Auto-connecting for scene: " + targetScene);

            StartSessionForScene(service, targetScene);
        }

        /// <summary>
        /// Dumps the internal API of sfService, sfBaseService, and ksService to understand the connection flow.
        /// </summary>
        private void DumpServiceApi(sfService service)
        {
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            // Dump sfService fields and methods
            Type serviceType = service.GetType();
            ksLog.Info(this, "=== sfService (" + serviceType.FullName + ") ===");
            foreach (FieldInfo f in serviceType.GetFields(flags))
            {
                string val = "";
                try { var v = f.GetValue(f.IsStatic ? null : service); val = v == null ? "null" : v.ToString(); } catch { val = "?"; }
                ksLog.Debug(this, "  field: " + (f.IsStatic ? "static " : "") + f.FieldType.Name + " " + f.Name + " = " + val);
            }
            foreach (MethodInfo m in serviceType.GetMethods(flags))
            {
                ksLog.Debug(this, "  method: " + (m.IsStatic ? "static " : "") + m.ReturnType.Name + " " + m.Name + "(" + GetParamString(m) + ")");
            }

            // Dump sfBaseService fields and methods (parent class)
            Type baseType = serviceType.BaseType;
            if (baseType != null)
            {
                ksLog.Info(this, "=== " + baseType.Name + " (base of sfService) ===");
                foreach (FieldInfo f in baseType.GetFields(flags))
                {
                    string val = "";
                    try { var v = f.GetValue(f.IsStatic ? null : service); val = v == null ? "null" : v.ToString(); } catch { val = "?"; }
                    ksLog.Debug(this, "  field: " + (f.IsStatic ? "static " : "") + f.FieldType.Name + " " + f.Name + " = " + val);
                }
                foreach (MethodInfo m in baseType.GetMethods(flags))
                {
                    ksLog.Debug(this, "  method: " + (m.IsStatic ? "static " : "") + m.ReturnType.Name + " " + m.Name + "(" + GetParamString(m) + ")");
                }

                // One more level up
                Type grandBase = baseType.BaseType;
                if (grandBase != null && grandBase != typeof(object))
                {
                    ksLog.Info(this, "=== " + grandBase.Name + " (grandbase) ===");
                    foreach (MethodInfo m in grandBase.GetMethods(flags))
                    {
                        ksLog.Debug(this, "  method: " + (m.IsStatic ? "static " : "") + m.ReturnType.Name + " " + m.Name + "(" + GetParamString(m) + ")");
                    }
                    foreach (FieldInfo f in grandBase.GetFields(flags))
                    {
                        string val = "";
                        try { var v = f.GetValue(f.IsStatic ? null : service); val = v == null ? "null" : v.ToString(); } catch { val = "?"; }
                        ksLog.Debug(this, "  field: " + (f.IsStatic ? "static " : "") + f.FieldType.Name + " " + f.Name + " = " + val);
                    }
                }
            }

            // Dump WebService info
            if (service.WebService != null)
            {
                Type wsType = service.WebService.GetType();
                ksLog.Info(this, "=== " + wsType.Name + " (WebService) ===");
                foreach (FieldInfo f in wsType.GetFields(flags))
                {
                    string val = "";
                    try { var v = f.GetValue(f.IsStatic ? null : service.WebService); val = v == null ? "null" : v.ToString(); } catch { val = "?"; }
                    ksLog.Debug(this, "  field: " + (f.IsStatic ? "static " : "") + f.FieldType.Name + " " + f.Name + " = " + val);
                }
                foreach (MethodInfo m in wsType.GetMethods(flags))
                {
                    ksLog.Debug(this, "  method: " + (m.IsStatic ? "static " : "") + m.ReturnType.Name + " " + m.Name + "(" + GetParamString(m) + ")");
                }
            }
        }

        private void StartSessionForScene(sfService service, string sceneName)
        {
            try
            {
                if (sfSessionsMenu.PreSessionCheck != null)
                {
                    bool ready = sfSessionsMenu.PreSessionCheck(null);
                    if (!ready)
                    {
                        ksLog.Warning(this, "PreSessionCheck returned false. Aborting auto-connect.");
                        m_isConnecting = false;
                        m_currentSyncedScene = null;
                        return;
                    }
                }

                int projectId = sfConfig.Get().ProjectId;
                string version = sfConfig.Get().Version.ToString();

                MethodInfo startSessionMethod = service.GetType().GetMethod("StartSession",
                    BindingFlags.Public | BindingFlags.Instance);

                if (startSessionMethod != null)
                {
                    ksLog.Info(this, "Calling StartSession(" + projectId + ", " + version + ", \"\", \"Unity\", " + sceneName + ")");
                    startSessionMethod.Invoke(service, new object[] { projectId, version, "", "Unity", sceneName });
                }
                else
                {
                    ksLog.Error(this, "Could not find StartSession method on service.");
                    m_isConnecting = false;
                    m_currentSyncedScene = null;
                }
            }
            catch (Exception ex)
            {
                ksLog.Error(this, "StartSessionForScene exception: " + ex.Message);
                m_isConnecting = false;
                m_currentSyncedScene = null;
            }
        }

        private string GetParamString(MethodInfo m)
        {
            string result = "";
            foreach (var p in m.GetParameters())
            {
                if (result.Length > 0) result += ", ";
                result += p.ParameterType.Name + " " + p.Name;
            }
            return result;
        }
    }
}
