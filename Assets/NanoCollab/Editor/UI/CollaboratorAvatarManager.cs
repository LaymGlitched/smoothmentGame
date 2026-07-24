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
            _presence.OnUserJoined  += OnUserJoined;
            _presence.OnUserLeft    += OnUserLeft;
            _presence.OnUserUpdated += OnUserUpdated;

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
                         ?? Shader.Find("Unlit/Color")
                         ?? Shader.Find("Standard")
                         ?? Shader.Find("Legacy Shaders/Diffuse");

            Color bodyColor = SanitizeColor(user.Color);
            Color lensColor = Color.Lerp(bodyColor, Color.white, 0.5f);
            lensColor.a = 1f;

            // 1. Sphere Body
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Body";
            body.transform.SetParent(avatarRoot.transform, false);
            body.transform.localScale = Vector3.one * 0.7f;
            body.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());

            var bodyRen = body.GetComponent<Renderer>();
            var bodyMat = new Material(shader);
            SetMaterialColor(bodyMat, bodyColor);
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
            SetMaterialColor(lensMat, lensColor);
            lensRen.material = lensMat;

            return new AvatarInstance
            {
                Root = avatarRoot,
                BodyMat = bodyMat,
                LensMat = lensMat
            };
        }

        /// <summary>
        /// Clamps all color channels to [0, 1] and forces alpha to 1.
        /// This prevents HDR/emission values from reaching URP materials and causing bright flickering.
        /// </summary>
        private static Color SanitizeColor(Color c)
        {
            return new Color(
                Mathf.Clamp01(c.r),
                Mathf.Clamp01(c.g),
                Mathf.Clamp01(c.b),
                1f);
        }

        private static void SetMaterialColor(Material mat, Color col)
        {
            if (mat == null) return;
            Color matCol = col.ToMaterialColor();
            mat.color = matCol;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", matCol);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color", matCol);
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
                SetMaterialColor(avatar.LensMat, Color.Lerp(SanitizeColor(user.Color), Color.white, 0.5f));
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

        private void OnUserUpdated(CollabUser user)
        {
            if (user.Id == _localId) return;
            if (_avatars.TryGetValue(user.Id, out var avatar) && avatar.Root != null)
            {
                // Update the avatar name in hierarchy
                avatar.Root.name = $"[Collaborator] {user.Name}";

                // Update colors immediately
                SetMaterialColor(avatar.BodyMat, user.Color);
                SetMaterialColor(avatar.LensMat, Color.Lerp(SanitizeColor(user.Color), Color.white, 0.5f));
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
                if (string.IsNullOrWhiteSpace(user.Name)) continue;

                var labelWorldPos = user.CameraPosition + Vector3.up * 0.75f;
                var screenPos = HandleUtility.WorldToGUIPoint(labelWorldPos);

                var activeCam = Camera.current ?? SceneView.lastActiveSceneView?.camera;
                bool inFront = activeCam == null || Vector3.Dot(activeCam.transform.forward, labelWorldPos - activeCam.transform.position) > 0;

                if (inFront)
                {
                    var labelStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        normal    = new GUIStyleState { textColor = Color.white },
                        alignment = TextAnchor.MiddleCenter,
                        fontSize  = 11,
                        fontStyle = FontStyle.Bold,
                        clipping  = TextClipping.Overflow,
                        padding   = new RectOffset(4, 4, 0, 0)
                    };

                    var content  = new GUIContent(user.Name);
                    var textSize = labelStyle.CalcSize(content);
                    float padding = 20f;
                    var rect     = new Rect(screenPos.x - (textSize.x + padding) / 2f, screenPos.y - 12, textSize.x + padding, 22);

                    Handles.BeginGUI();

                    var borderCol = user.Color.ToGUIColor();

                    // High-contrast dark badge background
                    EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.15f, 0.88f));

                    // Top user color accent bar
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 2.5f), borderCol);

                    // Border outline
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), new Color(borderCol.r * 0.7f, borderCol.g * 0.7f, borderCol.b * 0.7f, 1f));
                    EditorGUI.DrawRect(new Rect(rect.x + rect.width - 1, rect.y, 1, rect.height), new Color(borderCol.r * 0.7f, borderCol.g * 0.7f, borderCol.b * 0.7f, 1f));
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y + rect.height - 1, rect.width, 1), new Color(borderCol.r * 0.7f, borderCol.g * 0.7f, borderCol.b * 0.7f, 1f));

                    GUI.Label(rect, content, labelStyle);
                    Handles.EndGUI();
                }
            }
        }

        public void Dispose()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            _presence.OnUserJoined  -= OnUserJoined;
            _presence.OnUserLeft    -= OnUserLeft;
            _presence.OnUserUpdated -= OnUserUpdated;

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
