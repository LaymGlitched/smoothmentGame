using System;
using System.Linq;
using Pumkin.Extensions;
using Pumkin.HelperFunctions;
using UnityEditor;
using UnityEngine;
using static Pumkin.AvatarTools.PumkinToolsLogger;

namespace Pumkin.AvatarTools
{
    static class ComponentMenu
    {
        static readonly Type[] FixReferencesIgnoredTypes = new Type[]
        {
            typeof(Transform),
        };

        static readonly string[] FixReferencesIgnoredProperties = new string[]
        {
            "m_Script"
        };
        
        [MenuItem("CONTEXT/Component/Pumkin/Make References Local", true)]
        static bool MakeReferencesLocalValidate(MenuCommand command)
        {
            return !FixReferencesIgnoredTypes.Contains(command.context.GetType());
        }

        [MenuItem("CONTEXT/Component/Pumkin/Make References Local")]
        static void MakeReferencesLocal(MenuCommand command)
        {
            var serialComponent = new SerializedObject(command.context);

            bool createGameObjects = false;
            Transform targetHierarchyRoot = null; 
            Transform currentHierarchyRoot = null;
            
            serialComponent.ForEachPropertyVisible(true, x =>
            {
                try
                {
                    if(x.propertyType != SerializedPropertyType.ObjectReference || FixReferencesIgnoredProperties.Contains(x.name))
                        return;

                    var oldComp = x.objectReferenceValue as Component;
                    if(!oldComp)
                        return;

                    Type compType = oldComp.GetType();
                    int compIndex = oldComp.gameObject.GetComponents(compType)
                        .ToList()
                        .IndexOf(oldComp);

                    if(oldComp.gameObject.scene.name == null) // Don't fix if we're referencing an asset
                        return;

                    var transTarget = Helpers.FindTransformInAnotherHierarchy(oldComp.transform, currentHierarchyRoot, targetHierarchyRoot, createGameObjects);
                    if(transTarget == null)
                        return;

                    var targetComps = transTarget.GetComponents(compType);

                    x.objectReferenceValue = targetComps[compIndex];
                }
                catch(Exception e)
                {
                    Log($"_Error fixing reference on {command.context.name}: {e.Message}. Failed on property: {x.propertyPath}", LogType.Error);
                }
            });

            serialComponent.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}