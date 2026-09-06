using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Combat.Data;
using VisualNovel.Runtime;

namespace Combat.Runtime
{
    public class BattleManager : MonoBehaviour
    {
        public static BattleManager Instance { get; private set; }

        public List<CombatantView> playerParty = new List<CombatantView>();
        public List<CombatantView> enemyParty = new List<CombatantView>();

        public string nextSceneOnVictory = "VisualNovelDemo";
        public float actionPause = 0.35f;

        [Tooltip("If true, winning this battle triggers the True/Good/Bad ending flow instead of just continuing the story.")]
        public bool isFinalBattle = false;

        [Range(0f, 1f)]
        [Tooltip("Enemies below this HP fraction will try to Heal or Defend instead of attacking.")]
        public float enemyLowHpThreshold = 0.3f;

        public event Action<string> OnLog;
        public event Action OnSelectionReset;
        public event Action<bool> OnBattleEnded;
        public event Action OnTurnResolutionStarted;
        public event Action OnPlayerTurnReady;

        private struct PendingAction
        {
            public CombatSkill skill;
            public CombatantView target;
        }

        private readonly Dictionary<CombatantView, PendingAction> pending = new Dictionary<CombatantView, PendingAction>();
        private bool resolving;

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            foreach (var c in playerParty) c.Initialize();
            foreach (var c in enemyParty) c.Initialize();
            OnLog?.Invoke("การต่อสู้เริ่มขึ้น!");
        }

        public void SetPendingAction(CombatantView actor, CombatSkill skill, CombatantView target)
        {
            if (resolving || actor == null || !actor.IsAlive) return;

            if (!actor.HasEnoughMana(skill.manaCost))
            {
                OnLog?.Invoke($"{actor.definition.combatantName} มานาไม่พอสำหรับ {skill.skillName} (ต้องการ {skill.manaCost}, มี {actor.CurrentMana})");
                return;
            }

            pending[actor] = new PendingAction { skill = skill, target = target };
        }

        public bool HasPendingAction(CombatantView actor) => pending.ContainsKey(actor);

        public void ClearPendingAction(CombatantView actor)
        {
            if (!resolving) pending.Remove(actor);
        }

        public bool AllPlayersReady()
        {
            foreach (var p in playerParty)
            {
                if (p.IsAlive && !pending.ContainsKey(p)) return false;
            }
            return true;
        }

        public void ConfirmTurn()
        {
            if (resolving || !AllPlayersReady()) return;
            OnTurnResolutionStarted?.Invoke();
            StartCoroutine(ResolveTurnRoutine());
        }

        IEnumerator ResolveTurnRoutine()
        {
            resolving = true;

            foreach (var p in playerParty)
            {
                if (p.IsAlive) { p.SetDefending(false); p.SetTaunting(false); }
            }

            foreach (var p in playerParty)
            {
                if (!p.IsAlive || !pending.TryGetValue(p, out var action)) continue;
                ExecuteAction(p, action.skill, action.target);
                if (CheckVictory())
                {
                    EndBattle(true);
                    yield break;
                }
                yield return new WaitForSeconds(actionPause);
            }

            pending.Clear();
            OnSelectionReset?.Invoke();

            foreach (var e in enemyParty)
            {
                if (!e.IsAlive) continue;
                e.SetDefending(false);
                e.SetTaunting(false);

                var decision = DecideEnemyAction(e);
                if (decision.skill == null) continue;
                ExecuteAction(e, decision.skill, decision.target);

                if (CheckDefeat())
                {
                    EndBattle(false);
                    yield break;
                }
                yield return new WaitForSeconds(actionPause);
            }

            RegenManaForNewRound();

            resolving = false;
            OnLog?.Invoke("ทุกคนฟื้นฟูมานา 1 หน่วย - เลือกสกิลของทีมคุณสำหรับรอบถัดไป");
            OnPlayerTurnReady?.Invoke();
        }

        void RegenManaForNewRound()
        {
            foreach (var p in playerParty)
            {
                if (p.IsAlive) p.ApplyManaCost(-1);
            }
            foreach (var e in enemyParty)
            {
                if (e.IsAlive) e.ApplyManaCost(-1);
            }
        }

        CombatantView FindMostWoundedAlly(CombatantView self)
        {
            CombatantView best = null;
            float bestRatio = 1f;
            foreach (var ally in enemyParty)
            {
                if (ally == self || !ally.IsAlive || ally.CurrentHP >= ally.definition.maxHP) continue;
                float ratio = (float)ally.CurrentHP / ally.definition.maxHP;
                if (best == null || ratio < bestRatio) { best = ally; bestRatio = ratio; }
            }
            return best;
        }

        (CombatSkill skill, CombatantView target) DecideEnemyAction(CombatantView e)
        {
            float hpRatio = (float)e.CurrentHP / e.definition.maxHP;

            if (hpRatio <= enemyLowHpThreshold)
            {
                var healSkill = e.definition.skills.Find(s => s.type == SkillType.Heal);
                if (healSkill != null && e.HasEnoughMana(healSkill.manaCost))
                {
                    var wounded = FindMostWoundedAlly(e);
                    if (wounded != null) return (healSkill, wounded);
                }

                var defendWhenLow = e.definition.skills.Find(s => s.type == SkillType.Defend);
                if (defendWhenLow != null) return (defendWhenLow, e);
            }

            var attackSkills = e.definition.skills.FindAll(s => s.type == SkillType.Attack && e.HasEnoughMana(s.manaCost));
            var aliveAllies = playerParty.FindAll(x => x.IsAlive);

            if (attackSkills.Count > 0 && aliveAllies.Count > 0)
            {
                // Random skill AND random target, so enemy attacks feel unpredictable instead of always focusing the lowest-HP ally.
                // Unless someone is taunting - a taunt always overrides random targeting.
                var chosenAttack = attackSkills[UnityEngine.Random.Range(0, attackSkills.Count)];
                var taunter = aliveAllies.Find(x => x.IsTaunting);
                var chosenTarget = taunter != null ? taunter : aliveAllies[UnityEngine.Random.Range(0, aliveAllies.Count)];
                return (chosenAttack, chosenTarget);
            }

            var fallbackDefend = e.definition.skills.Find(s => s.type == SkillType.Defend);
            return (fallbackDefend, e);
        }

        List<CombatantView> GetSameParty(CombatantView actor) => playerParty.Contains(actor) ? playerParty : enemyParty;
        List<CombatantView> GetOpposingParty(CombatantView actor) => playerParty.Contains(actor) ? enemyParty : playerParty;

        int RollDamage(CombatantView actor, CombatSkill skill, CombatantView target, out bool isCrit)
        {
            int raw = Mathf.Max(1, actor.definition.atk + actor.AtkBuff + skill.power - target.definition.def);
            isCrit = UnityEngine.Random.value < actor.definition.critChance;
            if (isCrit) raw = Mathf.RoundToInt(raw * actor.definition.critMultiplier);
            return raw;
        }

        void ExecuteAction(CombatantView actor, CombatSkill skill, CombatantView target)
        {
            switch (skill.type)
            {
                case SkillType.Attack:
                    if (skill.hitsAllEnemies)
                    {
                        var targets = GetOpposingParty(actor).FindAll(t => t.IsAlive);
                        if (targets.Count == 0) return;

                        var hits = new List<string>();
                        foreach (var t in targets)
                        {
                            int dmg = RollDamage(actor, skill, t, out bool crit);
                            int hitDealt = t.TakeDamage(dmg);
                            hits.Add($"{t.definition.combatantName} (-{hitDealt}{(crit ? "!" : "")})");
                        }
                        actor.ApplyManaCost(skill.manaCost);
                        OnLog?.Invoke($"{actor.definition.combatantName} ใช้ {skill.skillName} ใส่ทุกคน: {string.Join(", ", hits)}");
                    }
                    else
                    {
                        if (target == null || !target.IsAlive) return;
                        int rawDamage = RollDamage(actor, skill, target, out bool isCrit);
                        int dealt = target.TakeDamage(rawDamage);
                        actor.ApplyManaCost(skill.manaCost);
                        string critSuffix = isCrit ? " (คริติคอล!)" : "";
                        OnLog?.Invoke($"{actor.definition.combatantName} ใช้ {skill.skillName} ใส่ {target.definition.combatantName} (-{dealt} HP){critSuffix}");
                    }
                    break;

                case SkillType.Defend:
                    actor.SetDefending(true);
                    if (skill.taunts) actor.SetTaunting(true);
                    actor.ApplyManaCost(skill.manaCost);
                    string tauntSuffix = skill.taunts ? " (ดึงความสนใจศัตรูทั้งหมด)" : "";
                    OnLog?.Invoke($"{actor.definition.combatantName} ใช้ {skill.skillName} ตั้งท่าป้องกัน{tauntSuffix}");
                    break;

                case SkillType.Heal:
                    if (skill.healsAllAllies)
                    {
                        var allies = GetSameParty(actor).FindAll(a => a.IsAlive);
                        if (allies.Count == 0) return;

                        foreach (var a in allies)
                        {
                            if (skill.power > 0) a.Heal(skill.power);
                            if (skill.manaRestoreAmount > 0) a.ApplyManaCost(-skill.manaRestoreAmount);
                        }
                        actor.ApplyManaCost(skill.manaCost);
                        OnLog?.Invoke($"{actor.definition.combatantName} ใช้ {skill.skillName} ฟื้นฟูทุกคนในทีม");
                    }
                    else
                    {
                        if (target == null || !target.IsAlive) return;
                        if (skill.power > 0) target.Heal(skill.power);
                        if (skill.manaRestoreAmount > 0) target.ApplyManaCost(-skill.manaRestoreAmount);
                        actor.ApplyManaCost(skill.manaCost);
                        string manaSuffix = skill.manaRestoreAmount > 0 ? $" และมานา {skill.manaRestoreAmount}" : "";
                        OnLog?.Invoke($"{actor.definition.combatantName} ใช้ {skill.skillName} ฟื้นฟู HP{manaSuffix} ให้ {target.definition.combatantName}");
                    }
                    break;

                case SkillType.Buff:
                    if (skill.atkBuffAmount != 0) actor.ApplyAtkBuff(skill.atkBuffAmount);
                    if (skill.secondaryHealAmount > 0) actor.Heal(skill.secondaryHealAmount);
                    actor.ApplyManaCost(skill.manaCost);
                    OnLog?.Invoke($"{actor.definition.combatantName} ใช้ {skill.skillName} (พลังโจมตี +{skill.atkBuffAmount}, ฟื้นฟู HP {skill.secondaryHealAmount})");
                    break;
            }
        }

        bool CheckVictory() => enemyParty.TrueForAll(e => !e.IsAlive);
        bool CheckDefeat() => playerParty.TrueForAll(p => !p.IsAlive);

        void EndBattle(bool victory)
        {
            resolving = true;
            OnBattleEnded?.Invoke(victory);

            if (victory)
            {
                OnLog?.Invoke("ชนะการต่อสู้!");

                if (isFinalBattle)
                {
                    var ending = RelationshipManager.Instance != null
                        ? RelationshipManager.Instance.DetermineEnding()
                        : EndingType.Good;
                    GameBootstrapState.HasPendingEnding = true;
                    GameBootstrapState.PendingEndingType = ending;
                }

                StartCoroutine(LoadSceneAfterDelay(nextSceneOnVictory, 1.5f));
            }
            else
            {
                OnLog?.Invoke("พ่ายแพ้... Game Over");
            }
        }

        IEnumerator LoadSceneAfterDelay(string sceneName, float delay)
        {
            yield return new WaitForSeconds(delay);
            SceneManager.LoadScene(sceneName);
        }
    }
}
