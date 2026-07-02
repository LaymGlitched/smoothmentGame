using GameCode.Magic;
using GameCode.Shared;
using UnityEditor;
using UnityEngine;

public class ExampleSpellCreator : EditorWindow
{
    [MenuItem("Nanodogs/Magic/Create Example Spells")]
    public static void CreateExampleSpells()
    {
        // Create a fireball spell
        Spell fireball = CreateSpell("Fireball", "Sphere");

        // Create Heat affinity
        HeatAffinity heat = ScriptableObject.CreateInstance<HeatAffinity>();
        heat.BurnDamage = 5f;
        heat.BurnDuration = 3f;
        fireball.Affinity = heat;

        // Add homing modifier
        HomingModifier homing = ScriptableObject.CreateInstance<HomingModifier>();
        homing.TurnSpeed = 8f;
        homing.DetectionRadius = 20f;
        fireball.Modifiers.Add(homing);

        // Create a force blast spell
        Spell forceBlast = CreateSpell("Force Blast", "Sphere");

        ForceAffinity force = ScriptableObject.CreateInstance<ForceAffinity>();
        force.KnockbackForce = 25f;
        forceBlast.Affinity = force;

        // Create an explosive spell
        Spell explosive = CreateSpell("Explosion", "Sphere");

        HeatAffinity explosiveHeat = ScriptableObject.CreateInstance<HeatAffinity>();
        explosiveHeat.BurnDamage = 3f;
        explosiveHeat.BurnDuration = 2f;
        explosive.Affinity = explosiveHeat;

        ExplosiveModifier explosiveMod = ScriptableObject.CreateInstance<ExplosiveModifier>();
        explosiveMod.Radius = 8f;
        explosiveMod.DamageMultiplier = 2f;
        explosive.Modifiers.Add(explosiveMod);

        // Create a ricochet spell
        Spell ricochetSpell = CreateSpell("Ricochet Shot", "Sphere");
        HeatAffinity ricochetHeat = ScriptableObject.CreateInstance<HeatAffinity>();
        ricochetHeat.BurnDamage = 2f;
        ricochetHeat.BurnDuration = 2f;
        ricochetSpell.Affinity = ricochetHeat;

        RicochetModifier ricochet = ScriptableObject.CreateInstance<RicochetModifier>();
        ricochet.MaxBounces = 3;
        ricochetSpell.Modifiers.Add(ricochet);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Example spells created! Check the Spells folder.");
    }

    private static Spell CreateSpell(string name, string shapeName)
    {
        // Ensure directory exists
        string directory = "Assets/Spells";
        if (!AssetDatabase.IsValidFolder(directory))
        {
            AssetDatabase.CreateFolder("Assets", "Spells");
        }

        Spell spell = ScriptableObject.CreateInstance<Spell>();
        spell.Name = name;

        // Create a sphere shape if it doesn't exist
        SphereShape sphereShape = AssetDatabase.LoadAssetAtPath<SphereShape>(
            "Assets/Spells/SphereShape.asset"
        );
        if (sphereShape == null)
        {
            sphereShape = ScriptableObject.CreateInstance<SphereShape>();
            AssetDatabase.CreateAsset(sphereShape, "Assets/Spells/SphereShape.asset");
        }
        spell.Shape = sphereShape;

        // Set stats
        spell.Stats = new SpellStats();
        spell.Stats.Power = 20f;
        spell.Stats.Speed = 30f;
        spell.Stats.Lifetime = 5f;
        spell.Stats.ManaCost = 15f;
        spell.Stats.Cooldown = 1.5f;

        // Save the spell
        string path = $"Assets/Spells/{name}.asset";
        AssetDatabase.CreateAsset(spell, path);

        return spell;
    }
}
