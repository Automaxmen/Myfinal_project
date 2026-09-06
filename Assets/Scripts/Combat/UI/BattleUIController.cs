using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Combat.Data;
using Combat.Runtime;
using VisualNovel.UI;
using Settings;

namespace Combat.UI
{
    public class BattleUIController : MonoBehaviour
    {
        [Serializable]
        public class PartyRowUI
        {
            public CombatantView combatant;
            public Text nameHpText;
            public Button[] skillButtons;
            public Button allyTargetButton;
        }

        [Serializable]
        public class EnemyTargetUI
        {
            public CombatantView combatant;
            public Text nameHpText;
            public Button targetButton;
        }

        private enum NavMode { Skill, EnemyTarget, AllyTarget, Confirm }

        public PartyRowUI[] partyRows;
        public EnemyTargetUI[] enemyTargets;
        public Button confirmButton;
        public Text logText;
        public GameObject gameOverPanel;
        public Button backToMenuButton;
        public Button menuButton;
        public SettingsMenuController settingsMenu;

        [Header("Skill Info Panel (slides in from the right)")]
        public RectTransform skillInfoPanel;
        public Text skillInfoText;
        public float infoPanelSlideDuration = 0.25f;
        public float infoPanelOffscreenOffset = 460f;

        [Header("Selection Panel (slides in from the left, shown only while choosing moves)")]
        public RectTransform selectionPanel;
        public float selectionPanelSlideDuration = 0.25f;
        public float selectionPanelOffscreenOffset = 500f;

        [Header("HP Bars")]
        public Image[] enemyHpFills;

        [Header("Party Mana Bars (in the selection grid, blue)")]
        public Image[] partyManaFills;
        public Text[] partyManaNumberText;

        [Header("Overhead Party HP Bars (floating above each character)")]
        public Image[] partyOverheadFills;
        public Text[] partyOverheadLabels;
        public Text[] partyOverheadHpText;

        [Header("Overhead Enemy HP Text (numbers overlaid directly on the bar)")]
        public Text[] enemyOverheadHpText;

        [Header("Enemy Target Glow")]
        public Color enemyGlowColor = new Color(1f, 0.2f, 0.15f, 1f);
        public Color allyGlowColor = new Color(0.3f, 1f, 0.4f, 1f);

        private CombatantView awaitingTargetFor;
        private CombatSkill awaitingSkill;
        private PartyRowUI awaitingRow;
        private int awaitingSkillIndex;
        private bool awaitingIsAllyTarget;

        private NavMode navMode = NavMode.Skill;
        private int focusRow;
        private int focusCol;
        private int focusTargetIndex;
        private int focusAllyIndex;
        private Outline[,] skillOutlines;
        private Outline[] enemyOutlines;
        private Outline[] allyOutlines;
        private Outline confirmOutline;
        private Vector2 infoPanelShownPos;
        private Vector2 infoPanelHiddenPos;
        private Vector2 selectionPanelShownPos;
        private Vector2 selectionPanelHiddenPos;
        private Coroutine infoPanelRoutine;
        private Coroutine selectionPanelRoutine;
        private Coroutine enemyGlowRoutine;
        private Outline currentGlowOutline;
        private CombatantView currentGlowCombatant;
        private bool battleEnded;
        private int[] prevPartyHp;
        private int[] prevEnemyHp;
        private int[] prevPartyMana;
        private readonly Dictionary<Image, Image> ghostBars = new Dictionary<Image, Image>();

        Outline EnsureOutline(Button btn)
        {
            var outline = btn.GetComponent<Outline>();
            if (outline == null) outline = btn.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.9f, 0.3f, 1f);
            outline.effectDistance = new Vector2(4, -4);
            outline.enabled = false;
            return outline;
        }

        void Awake()
        {
            foreach (var t in FindObjectsByType<Text>(FindObjectsSortMode.None))
            {
                t.font = t.fontStyle == FontStyle.Bold ? ThaiFont.GetBold() : ThaiFont.Get();
            }

            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(() => BattleManager.Instance.ConfirmTurn());
                confirmButton.interactable = false;
                confirmOutline = EnsureOutline(confirmButton);
            }
            if (backToMenuButton != null) backToMenuButton.onClick.AddListener(() => SceneManager.LoadScene("MainMenu"));
            if (menuButton != null && settingsMenu != null) menuButton.onClick.AddListener(settingsMenu.Toggle);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);

            skillOutlines = new Outline[partyRows.Length, 4];
            for (int r = 0; r < partyRows.Length; r++)
            {
                var row = partyRows[r];
                var capturedRow = row;
                for (int c = 0; c < row.skillButtons.Length; c++)
                {
                    int skillIndex = c;
                    row.skillButtons[c].onClick.AddListener(() => OnSkillClicked(capturedRow, skillIndex));
                    skillOutlines[r, c] = EnsureOutline(row.skillButtons[c]);
                }
            }

            enemyOutlines = new Outline[enemyTargets.Length];
            for (int i = 0; i < enemyTargets.Length; i++)
            {
                var enemy = enemyTargets[i];
                var capturedEnemy = enemy;
                if (enemy.targetButton != null)
                {
                    enemy.targetButton.onClick.AddListener(() => OnEnemyClicked(capturedEnemy.combatant));
                    enemyOutlines[i] = EnsureOutline(enemy.targetButton);
                }
            }

            allyOutlines = new Outline[partyRows.Length];
            for (int i = 0; i < partyRows.Length; i++)
            {
                var row = partyRows[i];
                var capturedRow = row;
                if (row.allyTargetButton != null)
                {
                    row.allyTargetButton.onClick.AddListener(() => OnAllyClicked(capturedRow.combatant));
                    allyOutlines[i] = EnsureOutline(row.allyTargetButton);
                }
            }

            if (skillInfoPanel != null)
            {
                infoPanelShownPos = skillInfoPanel.anchoredPosition;
                infoPanelHiddenPos = infoPanelShownPos + new Vector2(infoPanelOffscreenOffset, 0f);
                skillInfoPanel.anchoredPosition = infoPanelHiddenPos;
            }

            if (selectionPanel != null)
            {
                selectionPanelShownPos = selectionPanel.anchoredPosition;
                selectionPanelHiddenPos = selectionPanelShownPos + new Vector2(-selectionPanelOffscreenOffset, 0f);
                selectionPanel.anchoredPosition = selectionPanelHiddenPos;
            }
        }

        void Start()
        {
            BattleManager.Instance.OnLog += HandleLog;
            BattleManager.Instance.OnSelectionReset += ClearAllHighlights;
            BattleManager.Instance.OnBattleEnded += HandleBattleEnded;
            BattleManager.Instance.OnTurnResolutionStarted += HandleTurnResolutionStarted;
            BattleManager.Instance.OnPlayerTurnReady += HandlePlayerTurnReady;

            if (logText != null) logText.text = "การต่อสู้เริ่มขึ้น!";

            prevPartyHp = new int[partyRows.Length];
            for (int i = 0; i < prevPartyHp.Length; i++) prevPartyHp[i] = -1;
            prevEnemyHp = new int[enemyTargets.Length];
            for (int i = 0; i < prevEnemyHp.Length; i++) prevEnemyHp[i] = -1;
            prevPartyMana = new int[partyRows.Length];
            for (int i = 0; i < prevPartyMana.Length; i++) prevPartyMana[i] = -1;
            RefreshAllHpTexts();

            navMode = NavMode.Skill;
            focusRow = 0;
            focusCol = 0;
            SetSkillFocus(focusRow, focusCol, true);

            SlidePanel(ref selectionPanelRoutine, selectionPanel, selectionPanelShownPos, selectionPanelSlideDuration);
        }

        void OnDestroy()
        {
            if (BattleManager.Instance != null)
            {
                BattleManager.Instance.OnLog -= HandleLog;
                BattleManager.Instance.OnSelectionReset -= ClearAllHighlights;
                BattleManager.Instance.OnBattleEnded -= HandleBattleEnded;
                BattleManager.Instance.OnTurnResolutionStarted -= HandleTurnResolutionStarted;
                BattleManager.Instance.OnPlayerTurnReady -= HandlePlayerTurnReady;
            }
        }

        void HandleTurnResolutionStarted()
        {
            SlidePanel(ref selectionPanelRoutine, selectionPanel, selectionPanelHiddenPos, selectionPanelSlideDuration);
            HideSkillInfo();
        }

        void HandlePlayerTurnReady()
        {
            SlidePanel(ref selectionPanelRoutine, selectionPanel, selectionPanelShownPos, selectionPanelSlideDuration);

            SetConfirmFocus(false);
            navMode = NavMode.Skill;
            focusRow = 0;
            focusCol = 0;
            SetSkillFocus(focusRow, focusCol, true);
        }

        void Update()
        {
            if (battleEnded) return;

            var kb = Keyboard.current;
            if (kb == null || BattleManager.Instance == null) return;

            bool up = kb.wKey.wasPressedThisFrame;
            bool down = kb.sKey.wasPressedThisFrame;
            bool left = kb.aKey.wasPressedThisFrame;
            bool right = kb.dKey.wasPressedThisFrame;
            bool confirm = kb.spaceKey.wasPressedThisFrame;
            bool cancel = kb.qKey.wasPressedThisFrame;

            switch (navMode)
            {
                case NavMode.Skill:
                    if (up) MoveRow(-1);
                    else if (down) MoveRow(1);
                    else if (left) MoveCol(-1);
                    else if (right) MoveCol(1);
                    if (confirm) ActivateFocusedSkill();
                    else if (cancel) CancelToPreviousRow();
                    break;

                case NavMode.EnemyTarget:
                    if (left) MoveTarget(-1);
                    else if (right) MoveTarget(1);
                    if (confirm) ActivateFocusedEnemyTarget();
                    else if (cancel) CancelTargetSelection();
                    break;

                case NavMode.AllyTarget:
                    if (left) MoveAllyTarget(-1);
                    else if (right) MoveAllyTarget(1);
                    if (confirm) ActivateFocusedAllyTarget();
                    else if (cancel) CancelTargetSelection();
                    break;

                case NavMode.Confirm:
                    if (up)
                    {
                        SetConfirmFocus(false);
                        navMode = NavMode.Skill;
                        SetSkillFocus(focusRow, focusCol, true);
                        if (partyRows[focusRow].combatant != null) ShowSkillInfo(partyRows[focusRow].combatant.definition.skills[focusCol]);
                    }
                    if (confirm && confirmButton != null && confirmButton.interactable) confirmButton.onClick.Invoke();
                    else if (cancel) CancelFromConfirm();
                    break;
            }
        }

        void CancelTargetSelection()
        {
            if (awaitingIsAllyTarget) SetAllyTargetFocusIndex(focusAllyIndex, false);
            else SetTargetFocusIndex(focusTargetIndex, false);

            awaitingTargetFor = null;
            awaitingSkill = null;
            awaitingRow = null;
            awaitingIsAllyTarget = false;

            navMode = NavMode.Skill;
            SetSkillFocus(focusRow, focusCol, true);
            if (partyRows[focusRow].combatant != null) ShowSkillInfo(partyRows[focusRow].combatant.definition.skills[focusCol]);
            if (logText != null) logText.text = "ยกเลิกการเลือกเป้าหมาย";
        }

        void CancelFromConfirm()
        {
            for (int i = partyRows.Length - 1; i >= 0; i--)
            {
                var c = partyRows[i].combatant;
                if (c == null || !c.IsAlive || !BattleManager.Instance.HasPendingAction(c)) continue;

                BattleManager.Instance.ClearPendingAction(c);
                UnhighlightRow(partyRows[i]);
                SetConfirmFocus(false);
                navMode = NavMode.Skill;
                focusRow = i;
                focusCol = 0;
                SetSkillFocus(focusRow, focusCol, true);
                ShowSkillInfo(c.definition.skills[focusCol]);
                if (confirmButton != null) confirmButton.interactable = false;
                return;
            }
        }

        void CancelToPreviousRow()
        {
            for (int i = focusRow - 1; i >= 0; i--)
            {
                var c = partyRows[i].combatant;
                if (c == null || !c.IsAlive || !BattleManager.Instance.HasPendingAction(c)) continue;

                BattleManager.Instance.ClearPendingAction(c);
                UnhighlightRow(partyRows[i]);
                SetSkillFocus(focusRow, focusCol, false);
                focusRow = i;
                focusCol = 0;
                SetSkillFocus(focusRow, focusCol, true);
                ShowSkillInfo(c.definition.skills[focusCol]);
                if (confirmButton != null) confirmButton.interactable = false;
                return;
            }
        }

        void MoveRow(int delta)
        {
            SetSkillFocus(focusRow, focusCol, false);
            focusRow = Mathf.Clamp(focusRow + delta, 0, partyRows.Length - 1);
            SetSkillFocus(focusRow, focusCol, true);
            if (partyRows[focusRow].combatant != null) ShowSkillInfo(partyRows[focusRow].combatant.definition.skills[focusCol]);
        }

        void MoveCol(int delta)
        {
            SetSkillFocus(focusRow, focusCol, false);
            focusCol = Mathf.Clamp(focusCol + delta, 0, partyRows[focusRow].skillButtons.Length - 1);
            SetSkillFocus(focusRow, focusCol, true);
            if (partyRows[focusRow].combatant != null) ShowSkillInfo(partyRows[focusRow].combatant.definition.skills[focusCol]);
        }

        void SetSkillFocus(int row, int col, bool focused)
        {
            if (skillOutlines == null) return;
            var o = skillOutlines[row, col];
            if (o != null) o.enabled = focused;
        }

        void ActivateFocusedSkill()
        {
            var row = partyRows[focusRow];
            if (row.combatant == null) return;

            row.skillButtons[focusCol].onClick.Invoke();

            if (awaitingTargetFor != null)
            {
                SetSkillFocus(focusRow, focusCol, false);
                HideSkillInfo();
                if (awaitingIsAllyTarget)
                {
                    navMode = NavMode.AllyTarget;
                    focusAllyIndex = 0;
                    SetAllyTargetFocusIndex(0, true);
                }
                else
                {
                    navMode = NavMode.EnemyTarget;
                    focusTargetIndex = 0;
                    SetTargetFocusIndex(0, true);
                }
            }
            else if (BattleManager.Instance.HasPendingAction(row.combatant))
            {
                AdvanceAfterLock();
            }
        }

        void ActivateFocusedEnemyTarget()
        {
            var alive = AliveEnemyIndices();
            if (focusTargetIndex < 0 || focusTargetIndex >= alive.Count) return;

            int actualIndex = alive[focusTargetIndex];
            var btn = enemyTargets[actualIndex].targetButton;
            SetTargetFocusIndex(focusTargetIndex, false);
            if (btn != null) btn.onClick.Invoke();

            navMode = NavMode.Skill;
            AdvanceAfterLock();
        }

        void ActivateFocusedAllyTarget()
        {
            var alive = AliveAllyIndicesExcluding(awaitingTargetFor);
            if (focusAllyIndex < 0 || focusAllyIndex >= alive.Count) return;

            int actualIndex = alive[focusAllyIndex];
            var btn = partyRows[actualIndex].allyTargetButton;
            SetAllyTargetFocusIndex(focusAllyIndex, false);
            if (btn != null) btn.onClick.Invoke();

            navMode = NavMode.Skill;
            AdvanceAfterLock();
        }

        void AdvanceAfterLock()
        {
            SetSkillFocus(focusRow, focusCol, false);

            for (int i = 0; i < partyRows.Length; i++)
            {
                int idx = (focusRow + 1 + i) % partyRows.Length;
                var c = partyRows[idx].combatant;
                if (c != null && c.IsAlive && !BattleManager.Instance.HasPendingAction(c))
                {
                    focusRow = idx;
                    focusCol = 0;
                    SetSkillFocus(focusRow, focusCol, true);
                    ShowSkillInfo(partyRows[focusRow].combatant.definition.skills[focusCol]);
                    return;
                }
            }

            navMode = NavMode.Confirm;
            HideSkillInfo();
            SetConfirmFocus(true);
        }

        void SetConfirmFocus(bool focused)
        {
            if (confirmOutline != null) confirmOutline.enabled = focused;
        }

        List<int> AliveEnemyIndices()
        {
            var list = new List<int>();
            for (int i = 0; i < enemyTargets.Length; i++)
            {
                if (enemyTargets[i].combatant != null && enemyTargets[i].combatant.IsAlive) list.Add(i);
            }
            return list;
        }

        void MoveTarget(int delta)
        {
            var alive = AliveEnemyIndices();
            if (alive.Count == 0) return;
            SetTargetFocusIndex(focusTargetIndex, false);
            focusTargetIndex = Mathf.Clamp(focusTargetIndex + delta, 0, alive.Count - 1);
            SetTargetFocusIndex(focusTargetIndex, true);
        }

        void SetTargetFocusIndex(int aliveIndex, bool focused)
        {
            var alive = AliveEnemyIndices();
            if (aliveIndex < 0 || aliveIndex >= alive.Count)
            {
                if (!focused) StopEnemyGlow();
                return;
            }
            int actualIndex = alive[aliveIndex];
            var outline = enemyOutlines[actualIndex];
            if (focused) StartGlow(outline, enemyGlowColor, enemyTargets[actualIndex].combatant);
            else StopEnemyGlow();
        }

        List<int> AliveAllyIndicesExcluding(CombatantView exclude)
        {
            var list = new List<int>();
            for (int i = 0; i < partyRows.Length; i++)
            {
                var c = partyRows[i].combatant;
                if (c != null && c.IsAlive && c != exclude) list.Add(i);
            }
            return list;
        }

        void MoveAllyTarget(int delta)
        {
            var alive = AliveAllyIndicesExcluding(awaitingTargetFor);
            if (alive.Count == 0) return;
            SetAllyTargetFocusIndex(focusAllyIndex, false);
            focusAllyIndex = Mathf.Clamp(focusAllyIndex + delta, 0, alive.Count - 1);
            SetAllyTargetFocusIndex(focusAllyIndex, true);
        }

        void SetAllyTargetFocusIndex(int aliveIndex, bool focused)
        {
            var alive = AliveAllyIndicesExcluding(awaitingTargetFor);
            if (aliveIndex < 0 || aliveIndex >= alive.Count)
            {
                if (!focused) StopEnemyGlow();
                return;
            }
            int actualIndex = alive[aliveIndex];
            var outline = allyOutlines[actualIndex];
            if (focused) StartGlow(outline, allyGlowColor, partyRows[actualIndex].combatant);
            else StopEnemyGlow();
        }

        void StartGlow(Outline outline, Color color, CombatantView combatant)
        {
            StopEnemyGlow();
            if (outline == null) return;
            currentGlowOutline = outline;
            outline.effectColor = color;
            currentGlowCombatant = combatant;
            if (combatant != null) combatant.SetSelected(true, color);
            enemyGlowRoutine = StartCoroutine(GlowPulse(outline, combatant));
        }

        void StopEnemyGlow()
        {
            if (enemyGlowRoutine != null) StopCoroutine(enemyGlowRoutine);
            enemyGlowRoutine = null;
            if (currentGlowOutline != null) currentGlowOutline.enabled = false;
            currentGlowOutline = null;
            if (currentGlowCombatant != null) currentGlowCombatant.SetSelected(false, Color.white);
            currentGlowCombatant = null;
        }

        IEnumerator GlowPulse(Outline outline, CombatantView combatant)
        {
            outline.enabled = true;
            while (true)
            {
                float t = (Mathf.Sin(Time.time * 6f) + 1f) * 0.5f;
                var c = outline.effectColor;
                c.a = Mathf.Lerp(0.5f, 1f, t);
                outline.effectColor = c;
                outline.effectDistance = Vector2.one * Mathf.Lerp(3f, 6f, t);
                if (combatant != null) combatant.SetSelectionPulse(t);
                yield return null;
            }
        }

        void ShowSkillInfo(CombatSkill skill)
        {
            if (skillInfoText != null && skill != null)
            {
                string typeLabel = skill.type switch
                {
                    SkillType.Attack => "โจมตี",
                    SkillType.Defend => "ป้องกัน",
                    SkillType.Heal => "ฟื้นฟู",
                    SkillType.Buff => "บัฟ",
                    _ => skill.type.ToString()
                };
                string powerLabel = skill.type == SkillType.Defend ? "ลดความเสียหาย 50%" : $"ค่าพลัง: {skill.power}";
                string manaLabel = skill.manaCost > 0 ? $"ใช้มานา: {skill.manaCost}" : skill.manaCost < 0 ? $"เพิ่มมานา: {-skill.manaCost}" : "ไม่ใช้มานา";

                string extra = "";
                if (skill.hitsAllEnemies) extra += "\nโจมตีศัตรูทั้งหมด";
                if (skill.healsAllAllies) extra += "\nฟื้นฟูเพื่อนทั้งหมด";
                if (skill.manaRestoreAmount > 0) extra += $"\nฟื้นฟูมานา: {skill.manaRestoreAmount}";
                if (skill.atkBuffAmount > 0) extra += $"\nเพิ่มพลังโจมตี: +{skill.atkBuffAmount}";
                if (skill.secondaryHealAmount > 0) extra += $"\nฟื้นฟู HP ตัวเอง: {skill.secondaryHealAmount}";
                if (skill.taunts) extra += "\nดึงความสนใจศัตรูทั้งหมด";

                skillInfoText.text = $"{skill.skillName}\nประเภท: {typeLabel}\n{powerLabel}\n{manaLabel}{extra}\n\n{skill.description}";
            }
            SlidePanel(ref infoPanelRoutine, skillInfoPanel, infoPanelShownPos, infoPanelSlideDuration);
        }

        void HideSkillInfo()
        {
            SlidePanel(ref infoPanelRoutine, skillInfoPanel, infoPanelHiddenPos, infoPanelSlideDuration);
        }

        void SlidePanel(ref Coroutine routine, RectTransform panel, Vector2 target, float duration)
        {
            if (panel == null) return;
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(AnimatePanel(panel, target, duration));
        }

        IEnumerator AnimatePanel(RectTransform panel, Vector2 target, float duration)
        {
            Vector2 start = panel.anchoredPosition;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                panel.anchoredPosition = Vector2.Lerp(start, target, t);
                yield return null;
            }
            panel.anchoredPosition = target;
        }

        void OnSkillClicked(PartyRowUI row, int skillIndex)
        {
            var actor = row.combatant;
            if (actor == null || !actor.IsAlive) return;
            var skill = actor.definition.skills[skillIndex];

            if (!actor.HasEnoughMana(skill.manaCost))
            {
                if (logText != null) logText.text = $"{actor.definition.combatantName} มานาไม่พอสำหรับ {skill.skillName} (ต้องการ {skill.manaCost}, มี {actor.CurrentMana})";
                return;
            }

            if (skill.type == SkillType.Attack && skill.hitsAllEnemies)
            {
                LockAction(row, skillIndex, actor, skill, actor);
            }
            else if (skill.type == SkillType.Attack)
            {
                var aliveEnemies = BattleManager.Instance.enemyParty.FindAll(e => e.IsAlive);
                if (aliveEnemies.Count == 1)
                {
                    LockAction(row, skillIndex, actor, skill, aliveEnemies[0]);
                }
                else
                {
                    awaitingTargetFor = actor;
                    awaitingSkill = skill;
                    awaitingRow = row;
                    awaitingSkillIndex = skillIndex;
                    awaitingIsAllyTarget = false;
                    if (logText != null) logText.text = $"เลือกเป้าหมายศัตรูสำหรับ {actor.definition.combatantName}...";
                }
            }
            else if (skill.type == SkillType.Heal && skill.healsAllAllies)
            {
                LockAction(row, skillIndex, actor, skill, actor);
            }
            else if (skill.type == SkillType.Heal)
            {
                var aliveAllyIndices = AliveAllyIndicesExcluding(actor);
                if (aliveAllyIndices.Count == 1)
                {
                    LockAction(row, skillIndex, actor, skill, partyRows[aliveAllyIndices[0]].combatant);
                }
                else if (aliveAllyIndices.Count == 0)
                {
                    if (logText != null) logText.text = $"{actor.definition.combatantName} ไม่มีเพื่อนร่วมทีมให้ฟื้นฟู";
                }
                else
                {
                    awaitingTargetFor = actor;
                    awaitingSkill = skill;
                    awaitingRow = row;
                    awaitingSkillIndex = skillIndex;
                    awaitingIsAllyTarget = true;
                    if (logText != null) logText.text = $"เลือกเพื่อนที่จะฟื้นฟูสำหรับ {actor.definition.combatantName}...";
                }
            }
            else
            {
                LockAction(row, skillIndex, actor, skill, actor);
            }
        }

        void OnEnemyClicked(CombatantView enemy)
        {
            if (awaitingTargetFor == null || awaitingIsAllyTarget || enemy == null || !enemy.IsAlive) return;
            LockAction(awaitingRow, awaitingSkillIndex, awaitingTargetFor, awaitingSkill, enemy);
            awaitingTargetFor = null;
            awaitingSkill = null;
            awaitingRow = null;
        }

        void OnAllyClicked(CombatantView ally)
        {
            if (awaitingTargetFor == null || !awaitingIsAllyTarget || ally == null || !ally.IsAlive || ally == awaitingTargetFor) return;
            LockAction(awaitingRow, awaitingSkillIndex, awaitingTargetFor, awaitingSkill, ally);
            awaitingTargetFor = null;
            awaitingSkill = null;
            awaitingRow = null;
            awaitingIsAllyTarget = false;
        }

        void LockAction(PartyRowUI row, int skillIndex, CombatantView actor, CombatSkill skill, CombatantView target)
        {
            BattleManager.Instance.SetPendingAction(actor, skill, target);
            if (!BattleManager.Instance.HasPendingAction(actor)) return;
            HighlightSelected(row, skillIndex);
            if (confirmButton != null) confirmButton.interactable = BattleManager.Instance.AllPlayersReady();
        }

        void HighlightSelected(PartyRowUI row, int selectedIndex)
        {
            for (int i = 0; i < row.skillButtons.Length; i++)
            {
                var img = row.skillButtons[i].GetComponent<Image>();
                img.color = i == selectedIndex ? new Color(0.95f, 0.55f, 0.2f) : Color.white;
            }
        }

        void UnhighlightRow(PartyRowUI row)
        {
            foreach (var btn in row.skillButtons) btn.GetComponent<Image>().color = Color.white;
        }

        void ClearAllHighlights()
        {
            foreach (var row in partyRows) UnhighlightRow(row);
            if (confirmButton != null) confirmButton.interactable = false;
            RefreshAllHpTexts();
        }

        void RefreshAllHpTexts()
        {
            for (int i = 0; i < partyRows.Length; i++)
            {
                var row = partyRows[i];
                if (row.combatant == null) continue;
                int hp = row.combatant.CurrentHP;
                int maxHp = row.combatant.definition.maxHP;
                float targetFill = (float)hp / maxHp;

                int mana = row.combatant.CurrentMana;
                int maxMana = row.combatant.definition.maxMana;

                if (row.nameHpText != null) row.nameHpText.text = row.combatant.definition.combatantName;
                if (partyManaNumberText != null && i < partyManaNumberText.Length && partyManaNumberText[i] != null)
                    partyManaNumberText[i].text = mana.ToString();
                if (partyOverheadLabels != null && i < partyOverheadLabels.Length && partyOverheadLabels[i] != null)
                    partyOverheadLabels[i].text = row.combatant.definition.combatantName;
                if (partyOverheadHpText != null && i < partyOverheadHpText.Length && partyOverheadHpText[i] != null)
                    partyOverheadHpText[i].text = hp.ToString();
                float manaFrac = maxMana > 0 ? (float)mana / maxMana : 0f;
                RectTransform manaAnchor = (partyManaFills != null && i < partyManaFills.Length && partyManaFills[i] != null) ? partyManaFills[i].rectTransform : null;
                UpdateHpBar(partyManaFills, i, manaFrac, prevPartyMana[i], mana, manaAnchor);
                prevPartyMana[i] = mana;

                bool justDied = prevPartyHp[i] > 0 && hp <= 0;

                RectTransform overheadAnchor = (partyOverheadFills != null && i < partyOverheadFills.Length && partyOverheadFills[i] != null) ? partyOverheadFills[i].rectTransform : null;
                UpdateHpBar(partyOverheadFills, i, targetFill, prevPartyHp[i], hp, overheadAnchor);

                prevPartyHp[i] = hp;

                if (justDied) PlayDeathAnimation(row.combatant.transform, partyOverheadFills != null && i < partyOverheadFills.Length ? partyOverheadFills[i] : null);
            }
            for (int i = 0; i < enemyTargets.Length; i++)
            {
                var enemy = enemyTargets[i];
                if (enemy.combatant == null) continue;
                int hp = enemy.combatant.CurrentHP;
                int maxHp = enemy.combatant.definition.maxHP;
                float targetFill = (float)hp / maxHp;

                if (enemy.nameHpText != null) enemy.nameHpText.text = enemy.combatant.definition.combatantName;
                if (enemyOverheadHpText != null && i < enemyOverheadHpText.Length && enemyOverheadHpText[i] != null)
                    enemyOverheadHpText[i].text = hp.ToString();

                bool justDied = prevEnemyHp[i] > 0 && hp <= 0;

                RectTransform overheadAnchor = (enemyHpFills != null && i < enemyHpFills.Length && enemyHpFills[i] != null) ? enemyHpFills[i].rectTransform : null;
                UpdateHpBar(enemyHpFills, i, targetFill, prevEnemyHp[i], hp, overheadAnchor);

                prevEnemyHp[i] = hp;

                if (justDied) PlayDeathAnimation(enemy.combatant.transform, enemyHpFills != null && i < enemyHpFills.Length ? enemyHpFills[i] : null);
            }
        }

        void PlayDeathAnimation(Transform body, Image overheadFill)
        {
            if (body != null) StartCoroutine(ShrinkAndHide(body));

            if (overheadFill != null)
            {
                var barBg = overheadFill.transform.parent;
                var barRoot = barBg != null ? barBg.parent : null;
                if (barRoot != null) StartCoroutine(FadeAndHide(barRoot.gameObject));
            }
        }

        IEnumerator ShrinkAndHide(Transform body)
        {
            Vector3 startScale = body.localScale;
            float duration = 0.5f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                body.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                yield return null;
            }
            body.gameObject.SetActive(false);
        }

        IEnumerator FadeAndHide(GameObject barRoot)
        {
            var group = barRoot.GetComponent<CanvasGroup>();
            if (group == null) group = barRoot.AddComponent<CanvasGroup>();

            float duration = 0.5f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                group.alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
                yield return null;
            }
            barRoot.SetActive(false);
        }

        void UpdateHpBar(Image[] fills, int index, float targetFill, int prevHp, int currentHp, RectTransform popupAnchor)
        {
            if (fills == null || index >= fills.Length || fills[index] == null) return;
            var img = fills[index];

            if (prevHp < 0)
            {
                img.fillAmount = targetFill;
                return;
            }

            int delta = currentHp - prevHp;
            if (delta == 0)
            {
                img.fillAmount = targetFill;
                return;
            }

            StartCoroutine(AnimateHpChange(img, img.fillAmount, targetFill, delta, popupAnchor));
        }

        IEnumerator AnimateHpChange(Image fillImg, float fromFill, float toFill, int delta, RectTransform popupAnchor)
        {
            bool isHeal = delta > 0;
            if (popupAnchor != null) SpawnFloatingNumber(popupAnchor, delta, isHeal);

            Color baseColor = fillImg.color;
            Color flashColor = isHeal ? new Color(0.7f, 1f, 0.7f, 1f) : Color.white;
            float duration = 0.6f;
            float elapsed = 0f;

            // Ghost trail: a bright strip that holds the OLD fill level and catches down to the
            // new level a beat later, so the lost/gained sliver reads clearly instead of the bar
            // just quietly settling at a new size.
            Image ghost = GetOrCreateGhost(fillImg);
            if (ghost != null)
            {
                ghost.fillAmount = fromFill;
                ghost.color = isHeal ? new Color(0.6f, 1f, 0.6f, 0.9f) : new Color(1f, 0.85f, 0.2f, 0.9f);
                ghost.gameObject.SetActive(true);
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float flashT = Mathf.Clamp01(t / 0.3f);
                fillImg.fillAmount = Mathf.Lerp(fromFill, toFill, Mathf.Clamp01(t / 0.5f));
                fillImg.color = Color.Lerp(flashColor, baseColor, flashT);

                if (ghost != null)
                {
                    float ghostT = Mathf.Clamp01((t - 0.35f) / 0.65f);
                    ghost.fillAmount = Mathf.Lerp(fromFill, toFill, ghostT);
                }
                yield return null;
            }

            fillImg.fillAmount = toFill;
            fillImg.color = baseColor;
            if (ghost != null) ghost.gameObject.SetActive(false);
        }

        Image GetOrCreateGhost(Image fillImg)
        {
            if (fillImg == null) return null;
            if (ghostBars.TryGetValue(fillImg, out var existing) && existing != null) return existing;

            var go = new GameObject("Ghost", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(fillImg.transform.parent, false);
            go.transform.SetSiblingIndex(fillImg.transform.GetSiblingIndex());

            var srcRt = fillImg.rectTransform;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = srcRt.anchorMin;
            rt.anchorMax = srcRt.anchorMax;
            rt.pivot = srcRt.pivot;
            rt.sizeDelta = srcRt.sizeDelta;
            rt.anchoredPosition = srcRt.anchoredPosition;

            var img = go.GetComponent<Image>();
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.raycastTarget = false;
            go.SetActive(false);

            ghostBars[fillImg] = img;
            return img;
        }

        void SpawnFloatingNumber(RectTransform anchor, int delta, bool isHeal)
        {
            if (anchor == null || anchor.parent == null) return;

            var go = new GameObject("FloatingNumber", typeof(RectTransform));
            go.transform.SetParent(anchor.parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchor.anchorMin;
            rt.anchorMax = anchor.anchorMax;
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = anchor.anchoredPosition + new Vector2(0f, 34f);
            rt.sizeDelta = new Vector2(140f, 40f);

            var text = go.AddComponent<Text>();
            text.font = ThaiFont.GetBold();
            text.fontSize = 24;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = isHeal ? new Color(0.4f, 1f, 0.4f, 1f) : new Color(1f, 0.35f, 0.3f, 1f);
            text.text = (isHeal ? "+" : "-") + Mathf.Abs(delta);

            StartCoroutine(AnimateFloatingNumber(rt, text));
        }

        IEnumerator AnimateFloatingNumber(RectTransform rt, Text text)
        {
            float duration = 0.9f;
            float elapsed = 0f;
            Vector2 start = rt.anchoredPosition;
            Vector2 end = start + new Vector2(0f, 55f);
            Color startColor = text.color;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                rt.anchoredPosition = Vector2.Lerp(start, end, t);
                var c = startColor;
                c.a = Mathf.Lerp(1f, 0f, t);
                text.color = c;
                yield return null;
            }
            Destroy(rt.gameObject);
        }

        void HandleLog(string message)
        {
            RefreshAllHpTexts();
            if (logText != null) logText.text = message;
        }

        void HandleBattleEnded(bool victory)
        {
            RefreshAllHpTexts();
            if (confirmButton != null) confirmButton.interactable = false;
            battleEnded = true;
            HideSkillInfo();
            if (!victory && gameOverPanel != null) gameOverPanel.SetActive(true);
        }
    }
}
