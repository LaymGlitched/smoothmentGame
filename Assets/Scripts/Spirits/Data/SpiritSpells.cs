using System.Collections.Generic;
using UnityEngine;
using GameCode.Magic;

namespace GameCode.Spirits.Data
{
    [CreateAssetMenu(menuName = "Spirits/Spirit Spells")]
    public class SpiritSpells : ScriptableObject
    {
        public List<Spell> Spells = new List<Spell>();
        public List<MovementSpellOverride> MovementOverrides = new List<MovementSpellOverride>();
    }
}
