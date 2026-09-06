using System.Collections.Generic;
using UnityEngine;

namespace Combat.Data
{
    [CreateAssetMenu(fileName = "NewCombatant", menuName = "Combat/Combatant Definition")]
    public class CombatantDefinition : ScriptableObject
    {
        public string combatantName;
        public int maxHP = 50;
        public int maxMana = 10;
        public int atk = 10;
        public int def = 5;
        [Range(0f, 1f)] public float critChance = 0.1f;
        public float critMultiplier = 1.5f;
        public List<CombatSkill> skills = new List<CombatSkill>();
        public Color color = Color.white;
    }
}
