using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NanoCollab
{
    /// <summary>
    /// Manages 3D GameObject avatars in the active Scene for remote collaborators and simulated bots.
    /// Spawns avatars under a '----- Collaborators -----' root object in the Hierarchy.
    /// Uses HideFlags.DontSave so avatars never dirty or serialize into scene files.
    /// Supports URP, HDRP, and Built-in Render Pipelines with robust unlit materials.
    /// </summary>
    public sealed class CollaboratorAvatarManager : IDisposable
    {
        private readonly PresenceManager _presence;
        private readonly Guid _localId;
        private GameObject _rootContainer;
        private readonly Dictionary<Guid, AvatarInstance> _avatars = new();

        private sealed class AvatarInstance
        {
            public GameObject Root;
            public Material BodyMat;
            public Material LensMat;
        }

        public CollaboratorAvatarManager(PresenceManager presence, Guid localId)
        {
            _presence = presence;
            _localId  = localId;
            _presence.OnUserJoined += OnUserJoined;
            _presence.OnUserLeft   += OnUserLeft;

            SceneView.duringSceneGui += OnSceneGUI;
        }

        public void Tick()
        {
            EnsureRoot();

            foreach (var kv in _presence.Users)
            {
                var user = kv.Value;
                // Skip local user avatar (don't block own camera view)
                if (user.Id == _localId) continue;

                if (!_avatars.TryGetValue(user.Id, out var avatar) || avatar.Root == null)
                {
                    avatar = CreateAvatar(user);
                    _avatars[user.Id] = avatar;
                }

                UpdateAvatarTransform(avatar, user);
            }

            // Cleanup stale avatars
            var keys = new List<Guid>(_avatars.Keys);
            for (int i = keys.Count - 1; i >= 0; i--)
            {
                var id = keys[i];
                if (!_presence.Users.ContainsKey(id))
                {
                    DestroyAvatar(id);
                }
            }
        }

        private void EnsureRoot()
        {
            if (_rootContainer == null)
            {
                _rootContainer = GameObject.Find("----- Collaborators -----");
                if (_rootContainer == null)
                {
                    _rootContainer = new GameObject("----- Collaborators -----");
                    _rootContainer.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                }
            }
        }

        private AvatarInstance CreateAvatar(CollabUser user)
        {
            EnsureRoot();

            var avatarRoot = new GameObject($"[Collaborator] {user.Name}");
            avatarRoot.transform.SetParent(_rootContainer.transform, false);
            avatarRoot.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

            // Universal Render Pipeline & Built-in shader fallback
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Sprites/Default")
                         ?? Shader.Find("Unlit/Color")
                         ?? Shader.Find("Standard");

            // 1. Sphere Body
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Body";
            body.transform.SetParent(avatarRoot.transform, false);
            body.transform.localScale = Vector3.one * 0.7f;
            body.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());

            var bodyRen = body.GetComponent<Renderer>();
            var bodyMat = new Material(shader);
            SetMaterialColor(bodyMat, user.Color);
            bodyRen.material = bodyMat;

            // 2. Cube Lens
            var lens = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lens.name = "Lens";
            lens.transform.SetParent(avatarRoot.transform, false);
            lens.transform.localPosition = Vector3.forward * 0.4f;
            lens.transform.localScale = new Vector3(0.35f, 0.35f, 0.45f);
            lens.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            UnityEngine.Object.DestroyImmediate(lens.GetComponent<Collider>());

            var lensRen = lens.GetComponent<Renderer>();
            var lensMat = new Material(shader);
            SetMaterialColor(lensMat, Color.Lerp(user.Color, Color.white, 0.5f));
            lensRen.material = lensMat;

            return new AvatarInstance
            {
                Root = avatarRoot,
                BodyMat = bodyMat,
                LensMat = lensMat
            };
        }

        private static void SetMaterialColor(Material mat, Color col)
        {
            if (mat == null) return;
            mat.color = col;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", col);
        }

        private static void UpdateAvatarTransform(AvatarInstance avatar, CollabUser user)
        {
            if (avatar?.Root == null) return;

            // Hide avatar if position is uninitialized (0, 0, 0)
            bool isValid = (user.CameraPosition != Vector3.zero || user.CameraRotation != Quaternion.identity);
            if (avatar.Root.activeSelf != isValid)
                avatar.Root.SetActive(isValid);

            if (isValid)
            {
                avatar.Root.transform.position = user.CameraPosition;
                avatar.Root.transform.rotation = user.CameraRotation;

                SetMaterialColor(avatar.BodyMat, user.Color);
                SetMaterialColor(avatar.LensMat, Color.Lerp(user.Color, Color.white, 0.5f));
            }
        }

        private void OnUserJoined(CollabUser user)
        {
            if (user.Id == _localId) return;
            if (!_avatars.ContainsKey(user.Id))
            {
                _avatars[user.Id] = CreateAvatar(user);
            }
        }

        private void OnUserLeft(CollabUser user)
        {
            DestroyAvatar(user.Id);
        }

        private void DestroyAvatar(Guid id)
        {
            if (_avatars.TryGetValue(id, out var avatar))
            {
                _avatars.Remove(id);
                if (avatar?.Root != null)
                {
                    UnityEngine.Object.DestroyImmediate(avatar.Root);
                }
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            foreach (var kv in _presence.Users)
            {
                var user = kv.Value;
                if (user.Id == _localId) continue;
                if (user.CameraPosition == Vector3.zero && user.CameraRotation == Quaternion.identity) continue;

                var labelWorldPos = user.CameraPosition + Vector3.up * 0.75f;
                var screenPos = HandleUtility.WorldToGUIPoint(labelWorldPos);

                var activeCam = Camera.current ?? SceneView.lastActiveSceneView?.camera;
                bool inFront = activeCam == null || Vector3.Dot(activeCam.transform.forward, labelWorldPos - activeCam.transform.position) > 0;

                if (inFront)
                {
                    var labelStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        normal = { textColor = Color.white },
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 11,
                        fontStyle = FontStyle.Bold
                    };

                    var content = new GUIContent($"📷 {user.Name}");
                    var textSize = labelStyle.CalcSize(content);
                    var rect = new Rect(screenPos.x - (textSize.x + 16) / 2f, screenPos.y - 12, textSize.x + 16, 22);

                    Handles.BeginGUI();

                    var bgCol = user.Color;
                    bgCol.a = 0.9f;
                    EditorGUI.DrawRect(rect, bgCol);
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), Color.black);
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y + rect.height - 1, rect.width, 1), Color.black);

                    GUI.Label(rect, content, labelStyle);
                    Handles.EndGUI();
                }
            }
        }

        public void Dispose()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            _presence.OnUserJoined -= OnUserJoined;
            _presence.OnUserLeft   -= OnUserLeft;

            var keys = new List<Guid>(_avatars.Keys);
            foreach (var id in keys) DestroyAvatar(id);

            if (_rootContainer != null)
            {
                UnityEngine.Object.DestroyImmediate(_rootContainer);
                _rootContainer = null;
            }
        }
    }
}
