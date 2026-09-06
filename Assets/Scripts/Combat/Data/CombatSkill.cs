using UnityEngine;

namespace Combat.Data
{
    public enum SkillType { Attack, Defend, Heal, Buff }

    [CreateAssetMenu(fileName = "NewSkill", menuName = "Combat/Skill")]
    public class CombatSkill : ScriptableObject
    {
        public string skillName;
        public SkillType type;
        public int power;
        [Tooltip("Mana spent when using this skill. Negative values restore mana instead (e.g. Defend).")]
        public int manaCost;

        [Header("Targeting")]
        [Tooltip("Attack skills only: hits every alive enemy instead of one chosen target.")]
        public bool hitsAllEnemies;
        [Tooltip("Heal skills only: heals every alive ally instead of one chosen target.")]
        public bool healsAllAllies;

        [Header("Secondary Effects (optional, layer on top of the primary type)")]
        [Tooltip("Heal skills: mana restored to the target(s) alongside the HP heal.")]
        public int manaRestoreAmount;
        [Tooltip("Buff skills: permanent ATK increase applied to the caster for the rest of the battle.")]
        public int atkBuffAmount;
        [Tooltip("Buff skills: HP the caster heals on themselves alongside the buff.")]
        public int secondaryHealAmount;
        [Tooltip("Defend skills: while active, enemies are forced to target this character.")]
        public bool taunts;

        [TextArea(2, 4)] public string description;
    }
}
