using System.Collections.Generic;
using System.Linq;
using GameCode.Spirits.Data;
using UnityEditor;
using UnityEngine;

namespace GameCode.Spirits.Editor.Tools
{
    public class SpiritBrowserWindow : EditorWindow
    {
        private string searchQuery = "";
        private Vector2 scrollPos;
        private int selectedTab = 0;
        private readonly string[] tabs = { "Spirits", "Topics", "Events", "Scenarios" };

        private List<Object> filteredAssets = new List<Object>();

        [MenuItem("Spirits/Spirit Browser", priority = 0)]
        public static void ShowWindow()
        {
            GetWindow<SpiritBrowserWindow>("Spirit Browser");
        }

        private void OnEnable()
        {
            RefreshList();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            
            EditorGUI.BeginChangeCheck();
            searchQuery = EditorGUILayout.TextField("Search", searchQuery, EditorStyles.toolbarSearchField);
            int newTab = GUILayout.Toolbar(selectedTab, tabs);
            if (EditorGUI.EndChangeCheck() || newTab != selectedTab)
            {
                selectedTab = newTab;
                RefreshList();
            }

            EditorGUILayout.Space();

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            foreach (var asset in filteredAssets)
            {
                if (asset == null) continue;

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                
                // Draw a nice label
                string displayName = asset.name;
                string subText = "";
                
                if (asset is TopicDefinition t) { displayName = t.DisplayName; subText = t.Id.Value; }
                else if (asset is GameplayEventDefinition e) { displayName = e.DisplayName; subText = e.Id.Value; }
                else if (asset is SpiritDefinition s) { displayName = s.DisplayName; subText = s.Id.Value; }
                else if (asset is ScenarioDefinition sc) { displayName = sc.Id.Value; subText = "Scenario"; }

                EditorGUILayout.LabelField(displayName, EditorStyles.boldLabel, GUILayout.Width(200));
                EditorGUILayout.LabelField(subText, EditorStyles.miniLabel);

                if (GUILayout.Button("Select", GUILayout.Width(80)))
                {
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                }
                
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            if (GUILayout.Button("Refresh", GUILayout.Height(30)))
            {
                RefreshList();
            }
        }

        private void RefreshList()
        {
            filteredAssets.Clear();

            string typeFilter = tabs[selectedTab] switch
            {
                "Spirits" => "t:SpiritDefinition",
                "Topics" => "t:TopicDefinition",
                "Events" => "t:GameplayEventDefinition",
                "Scenarios" => "t:ScenarioDefinition",
                _ => ""
            };

            string[] guids = AssetDatabase.FindAssets(typeFilter);
            foreach (var guid in guids)
            {
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null)
                {
                    bool match = string.IsNullOrEmpty(searchQuery) || asset.name.ToLower().Contains(searchQuery.ToLower());
                    if (match)
                    {
                        filteredAssets.Add(asset);
                    }
                }
            }
            
            filteredAssets = filteredAssets.OrderBy(a => a.name).ToList();
        }
    }
}
