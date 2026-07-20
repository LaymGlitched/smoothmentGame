#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using GameCode.Magic;
using System.IO;

public static class SparkAssetGenerator
{
    [InitializeOnLoadMethod]
    private static void GenerateAssets()
    {
        string dir = "Assets/Data/Spells/Spirits/Spark";
        if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
        if (!AssetDatabase.IsValidFolder("Assets/Data/Spells")) AssetDatabase.CreateFolder("Assets/Data", "Spells");
        if (!AssetDatabase.IsValidFolder("Assets/Data/Spells/Spirits")) AssetDatabase.CreateFolder("Assets/Data/Spells", "Spirits");
        if (!AssetDatabase.IsValidFolder("Assets/Data/Spells/Spirits/Spark")) AssetDatabase.CreateFolder("Assets/Data/Spells/Spirits", "Spark");

        bool modified = false;

        // 1. Create Shapes
        PulseBoltShape pulseBoltShape = AssetDatabase.LoadAssetAtPath<PulseBoltShape>($"{dir}/Pulse Bolt Shape.asset");
        if (pulseBoltShape == null)
        {
            pulseBoltShape = ScriptableObject.CreateInstance<PulseBoltShape>();
            pulseBoltShape.DisplayName = "Pulse Bolt Shape";
            AssetDatabase.CreateAsset(pulseBoltShape, $"{dir}/Pulse Bolt Shape.asset");
            modified = true;
        }

        StaticTetherShape staticTetherShape = AssetDatabase.LoadAssetAtPath<StaticTetherShape>($"{dir}/Static Tether Shape.asset");
        if (staticTetherShape == null)
        {
            staticTetherShape = ScriptableObject.CreateInstance<StaticTetherShape>();
            staticTetherShape.DisplayName = "Static Tether Shape";
            AssetDatabase.CreateAsset(staticTetherShape, $"{dir}/Static Tether Shape.asset");
            modified = true;
        }

        DischargeShape dischargeShape = AssetDatabase.LoadAssetAtPath<DischargeShape>($"{dir}/Discharge Shape.asset");
        if (dischargeShape == null)
        {
            dischargeShape = ScriptableObject.CreateInstance<DischargeShape>();
            dischargeShape.DisplayName = "Discharge Shape";
            AssetDatabase.CreateAsset(dischargeShape, $"{dir}/Discharge Shape.asset");
            modified = true;
        }

        // 2. Create Movement Override
        MovementSpellOverride dischargeOverride = AssetDatabase.LoadAssetAtPath<MovementSpellOverride>($"{dir}/Spark Discharge Override.asset");
        if (dischargeOverride == null)
        {
            dischargeOverride = ScriptableObject.CreateInstance<MovementSpellOverride>();
            dischargeOverride.RequiresSliding = true;
            dischargeOverride.OverrideShape = dischargeShape;
            AssetDatabase.CreateAsset(dischargeOverride, $"{dir}/Spark Discharge Override.asset");
            modified = true;
        }

        // 3. Create Spells
        Spell pulseBoltSpell = AssetDatabase.LoadAssetAtPath<Spell>($"{dir}/Pulse Bolt.asset");
        if (pulseBoltSpell == null)
        {
            pulseBoltSpell = ScriptableObject.CreateInstance<Spell>();
            pulseBoltSpell.Name = "Pulse Bolt";
            pulseBoltSpell.Shape = pulseBoltShape;
            pulseBoltSpell.MovementOverrides.Add(dischargeOverride);
            AssetDatabase.CreateAsset(pulseBoltSpell, $"{dir}/Pulse Bolt.asset");
            modified = true;
        }

        Spell staticTetherSpell = AssetDatabase.LoadAssetAtPath<Spell>($"{dir}/Static Tether.asset");
        if (staticTetherSpell == null)
        {
            staticTetherSpell = ScriptableObject.CreateInstance<Spell>();
            staticTetherSpell.Name = "Static Tether";
            staticTetherSpell.Shape = staticTetherShape;
            AssetDatabase.CreateAsset(staticTetherSpell, $"{dir}/Static Tether.asset");
            modified = true;
        }

        if (modified)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Spark spell assets generated successfully in " + dir);
        }
    }
}
#endif
