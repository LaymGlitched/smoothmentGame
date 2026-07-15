using UnityEditor;
using UnityEngine;
using GameCode.Spirits.Data;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using Reiteki.Localization.Core;

namespace GameCode.Spirits.EditorScripts
{
    [CustomPropertyDrawer(typeof(DialogueEntry))]
    public class DialogueEntryDrawer : PropertyDrawer
    {
        // Simple editor cache to avoid parsing JSON every frame.
        private static Dictionary<string, string> _editorCache;
        private static double _lastCacheTime;

        private void EnsureEditorCache()
        {
            if (Application.isPlaying) return;

            // Refresh cache every 5 seconds to catch edits to the JSON files
            if (_editorCache == null || EditorApplication.timeSinceStartup - _lastCacheTime > 5.0)
            {
                _editorCache = new Dictionary<string, string>();
                _lastCacheTime = EditorApplication.timeSinceStartup;

                string basePath = Path.Combine(Application.streamingAssetsPath, "Localization", "en-US");
                if (Directory.Exists(basePath))
                {
                    string[] files = Directory.GetFiles(basePath, "*.json", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        if (Path.GetFileName(file).Equals("locale.json", System.StringComparison.OrdinalIgnoreCase)) continue;
                        try
                        {
                            string json = File.ReadAllText(file);
                            var entries = JsonConvert.DeserializeObject<Dictionary<string, LocalizedEntry>>(json);
                            if (entries != null)
                            {
                                foreach (var kvp in entries)
                                {
                                    _editorCache[kvp.Key] = kvp.Value.Text;
                                }
                            }
                        }
                        catch { /* Ignore parse errors in editor drawer */ }
                    }
                }
            }
        }

        private string GetPreviewText(string key)
        {
            if (Application.isPlaying && Reiteki.Localization.LocalizationBootstrapper.Instance != null)
            {
                if (Reiteki.Localization.LocalizationBootstrapper.Instance.TryGet(key, out string val))
                    return val;
                return null;
            }
            else
            {
                EnsureEditorCache();
                if (_editorCache != null && _editorCache.TryGetValue(key, out string val))
                    return val;
                return null;
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            float height = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing; // Header

            var iterator = property.Copy();
            var endProperty = iterator.GetEndProperty();
            iterator.NextVisible(true);

            while (!SerializedProperty.EqualContents(iterator, endProperty))
            {
                height += EditorGUI.GetPropertyHeight(iterator, true) + EditorGUIUtility.standardVerticalSpacing;
                
                if (iterator.name == "LocalizationKey" || iterator.name == "localizationKey")
                {
                    SerializedProperty valueProp = iterator.FindPropertyRelative("Value");
                    string key = valueProp != null ? valueProp.stringValue : "";
                    
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        height += 30; // Height for warning box
                    }
                    else
                    {
                        string preview = GetPreviewText(key);
                        if (preview == null)
                        {
                            height += 30; // Height for warning box
                        }
                        else
                        {
                            GUIStyle previewStyle = new GUIStyle(EditorStyles.helpBox) { wordWrap = true };
                            float previewHeight = previewStyle.CalcHeight(new GUIContent(preview), EditorGUIUtility.currentViewWidth - 40);
                            height += previewHeight + EditorGUIUtility.standardVerticalSpacing;
                        }
                    }
                }
                
                iterator.NextVisible(false);
            }

            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            
            Rect foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

            if (property.isExpanded)
            {
                var indent = EditorGUI.indentLevel;
                EditorGUI.indentLevel++;

                float currentY = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                var iterator = property.Copy();
                var endProperty = iterator.GetEndProperty();
                iterator.NextVisible(true);

                while (!SerializedProperty.EqualContents(iterator, endProperty))
                {
                    float propHeight = EditorGUI.GetPropertyHeight(iterator, true);
                    Rect propRect = new Rect(position.x, currentY, position.width, propHeight);
                    
                    EditorGUI.PropertyField(propRect, iterator, true);
                    currentY += propHeight + EditorGUIUtility.standardVerticalSpacing;

                    if (iterator.name == "LocalizationKey" || iterator.name == "localizationKey")
                    {
                        SerializedProperty valueProp = iterator.FindPropertyRelative("Value");
                        string key = valueProp != null ? valueProp.stringValue : "";
                        
                        if (string.IsNullOrWhiteSpace(key))
                        {
                            Rect warningRect = new Rect(position.x + 15, currentY, position.width - 15, 30);
                            EditorGUI.HelpBox(warningRect, "Localization Key cannot be empty.", MessageType.Error);
                            currentY += 30 + EditorGUIUtility.standardVerticalSpacing;
                        }
                        else
                        {
                            string preview = GetPreviewText(key);
                            if (preview == null)
                            {
                                Rect warningRect = new Rect(position.x + 15, currentY, position.width - 15, 30);
                                EditorGUI.HelpBox(warningRect, $"Missing translation for key: {key}", MessageType.Warning);
                                currentY += 30 + EditorGUIUtility.standardVerticalSpacing;
                            }
                            else
                            {
                                GUIStyle previewStyle = new GUIStyle(EditorStyles.helpBox) { wordWrap = true };
                                float previewHeight = previewStyle.CalcHeight(new GUIContent(preview), position.width - 15);
                                Rect previewRect = new Rect(position.x + 15, currentY, position.width - 15, previewHeight);
                                EditorGUI.LabelField(previewRect, preview, previewStyle);
                                currentY += previewHeight + EditorGUIUtility.standardVerticalSpacing;
                            }
                        }
                    }

                    iterator.NextVisible(false);
                }

                EditorGUI.indentLevel = indent;
            }
            
            EditorGUI.EndProperty();
        }
    }
}
