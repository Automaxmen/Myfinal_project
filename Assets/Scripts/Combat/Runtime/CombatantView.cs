using System;
using UnityEngine;
using Combat.Data;

namespace Combat.Runtime
{
    public class CombatantView : MonoBehaviour
    {
        public CombatantDefinition definition;
        public bool isPlayerSide;
        public Renderer bodyRenderer;

        [Header("Selection Ring (glowing ring shown at the feet while this character is targeted)")]
        public GameObject selectionRing;
        public Renderer selectionRingRenderer;

        Camera billboardCamera;
        Material selectionRingMaterialInstance;

        public int CurrentHP { get; private set; }
        public int CurrentMana { get; private set; }
        public bool IsDefending { get; private set; }
        public bool IsTaunting { get; private set; }
        public int AtkBuff { get; private set; }
        public bool IsAlive => CurrentHP > 0;

        public event Action OnStateChanged;

        void Awake()
        {
            if (definition != null)
            {
                CurrentHP = definition.maxHP;
                CurrentMana = definition.maxMana;
            }
        }

        public void Initialize()
        {
            CurrentHP = definition.maxHP;
            CurrentMana = definition.maxMana;
            IsDefending = false;
            IsTaunting = false;
            AtkBuff = 0;
            if (bodyRenderer != null) bodyRenderer.material.color = definition.color;
        }

        public void SetDefending(bool value)
        {
            IsDefending = value;
            OnStateChanged?.Invoke();
        }

        public void SetTaunting(bool value)
        {
            IsTaunting = value;
            OnStateChanged?.Invoke();
        }

        public void ApplyAtkBuff(int amount)
        {
            AtkBuff += amount;
            OnStateChanged?.Invoke();
        }

        public int TakeDamage(int amount)
        {
            int final = IsDefending ? Mathf.Max(1, amount / 2) : amount;
            CurrentHP = Mathf.Max(0, CurrentHP - final);
            OnStateChanged?.Invoke();
            return final;
        }

        public void Heal(int amount)
        {
            CurrentHP = Mathf.Min(definition.maxHP, CurrentHP + amount);
            OnStateChanged?.Invoke();
        }

        public bool HasEnoughMana(int cost) => cost <= 0 || CurrentMana >= cost;

        public void ApplyManaCost(int cost)
        {
            CurrentMana = Mathf.Clamp(CurrentMana - cost, 0, definition.maxMana);
            OnStateChanged?.Invoke();
        }

        public void SetSelected(bool selected, Color color)
        {
            if (selectionRing == null) return;
            selectionRing.SetActive(selected);
            if (!selected) return;

            if (selectionRingMaterialInstance == null && selectionRingRenderer != null)
            {
                selectionRingMaterialInstance = selectionRingRenderer.material;
            }
            if (selectionRingMaterialInstance != null)
            {
                selectionRingMaterialInstance.color = color;
            }
        }

        public void SetSelectionPulse(float t)
        {
            if (selectionRing == null || !selectionRing.activeSelf) return;

            if (selectionRingMaterialInstance != null)
            {
                var c = selectionRingMaterialInstance.color;
                c.a = Mathf.Lerp(0.5f, 1f, t);
                selectionRingMaterialInstance.color = c;
            }

            float scale = Mathf.Lerp(1f, 1.15f, t);
            selectionRing.transform.localScale = new Vector3(scale, scale, scale);

            if (billboardCamera == null) billboardCamera = Camera.main;
            if (billboardCamera != null)
            {
                Vector3 toCam = billboardCamera.transform.position - selectionRing.transform.position;
                if (toCam.sqrMagnitude > 0.0001f)
                {
                    selectionRing.transform.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
                }
            }
        }
    }
}
