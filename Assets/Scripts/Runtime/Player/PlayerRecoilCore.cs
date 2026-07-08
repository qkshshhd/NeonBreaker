using System;
using UnityEngine;

namespace NeonBreaker.Player
{
    public sealed class PlayerRecoilCore : MonoBehaviour
    {
        [Header("Gauge")]
        [SerializeField, Min(1f)] private float maxRecoil = 100f;
        [SerializeField, Min(0f)] private float basicAttackRecoilGain = 2.5f;
        [SerializeField, Min(0f)] private float comboStepGainBonus = 0.35f;
        [SerializeField, Min(0f)] private float naturalDecayPerSecond = 0f;

        [Header("Basic Attack Scaling")]
        [SerializeField, Min(0f)] private float fullRecoilAttackDamageBonus = 0.45f;
        [SerializeField, Min(0f)] private float fullRecoilAttackKnockbackBonus = 0.25f;

        [Header("High Recoil Penalty")]
        [SerializeField, Range(0f, 1f)] private float penaltyStartRatio = 0.25f;
        [SerializeField, Range(0f, 0.8f)] private float fullRecoilMoveSpeedPenalty = 0.34f;
        [SerializeField, Range(0f, 1.5f)] private float fullRecoilAttackCooldownPenalty = 0.7f;
        [SerializeField, Range(0f, 1.5f)] private float fullRecoilDashCooldownPenalty = 0.55f;
        [SerializeField, Range(0f, 0.8f)] private float fullRecoilDashDistancePenalty = 0.28f;
        [SerializeField, Range(0.25f, 3f)] private float penaltyCurvePower = 0.85f;

        [Header("Skill Discharge Scaling")]
        [SerializeField, Min(0f)] private float emptyRecoilSkillDamageMultiplier = 0.35f;
        [SerializeField, Min(0f)] private float fullRecoilSkillDamageMultiplier = 3.2f;
        [SerializeField, Min(0f)] private float emptyRecoilSkillKnockbackMultiplier = 0.35f;
        [SerializeField, Min(0f)] private float fullRecoilSkillKnockbackMultiplier = 2.8f;
        [SerializeField, Range(0.25f, 3f)] private float skillDischargeCurvePower = 1.35f;

        private float currentRecoil;
        [SerializeField, HideInInspector] private int tuningVersion;

        private const int CurrentTuningVersion = 4;

        public event Action<float, float> RecoilChanged;
        public event Action<float> RecoilDischarged;

        public float CurrentRecoil => currentRecoil;
        public float MaxRecoil => Mathf.Max(1f, maxRecoil);
        public float NormalizedRecoil => Mathf.Clamp01(currentRecoil / MaxRecoil);
        public bool IsStable => NormalizedRecoil < 0.3f;
        public bool IsOverheated => NormalizedRecoil >= 0.3f && NormalizedRecoil < 0.7f;
        public bool IsDangerous => NormalizedRecoil >= 0.7f;
        public float PenaltyRatio => GetPenaltyRatio();

        private void Awake()
        {
            UpgradeTuningDefaults();
            maxRecoil = Mathf.Max(1f, maxRecoil);
            currentRecoil = Mathf.Clamp(currentRecoil, 0f, MaxRecoil);
        }

        private void OnValidate()
        {
            UpgradeTuningDefaults();
        }

        private void Update()
        {
            if (naturalDecayPerSecond <= 0f || currentRecoil <= 0f)
            {
                return;
            }

            SetRecoil(currentRecoil - naturalDecayPerSecond * Time.deltaTime);
        }

        public void AddBasicAttackRecoil(int hitCount, int comboStepIndex)
        {
            if (hitCount <= 0 || basicAttackRecoilGain <= 0f)
            {
                return;
            }

            float stepBonus = Mathf.Max(0f, comboStepIndex) * comboStepGainBonus;
            AddRecoil((basicAttackRecoilGain + stepBonus) * hitCount);
        }

        public float GetBasicAttackDamageMultiplier()
        {
            return 1f + NormalizedRecoil * Mathf.Max(0f, fullRecoilAttackDamageBonus);
        }

        public float GetBasicAttackKnockbackMultiplier()
        {
            return 1f + NormalizedRecoil * Mathf.Max(0f, fullRecoilAttackKnockbackBonus);
        }

        public float GetMoveSpeedMultiplier()
        {
            return 1f - GetPenaltyRatio() * Mathf.Clamp01(fullRecoilMoveSpeedPenalty);
        }

        public float GetAttackCooldownMultiplier()
        {
            return 1f + GetPenaltyRatio() * Mathf.Max(0f, fullRecoilAttackCooldownPenalty);
        }

        public float GetDashCooldownMultiplier()
        {
            return 1f + GetPenaltyRatio() * Mathf.Max(0f, fullRecoilDashCooldownPenalty);
        }

        public float GetDashDistanceMultiplier()
        {
            return 1f - GetPenaltyRatio() * Mathf.Clamp01(fullRecoilDashDistancePenalty);
        }

        public float BeginSkillDischarge()
        {
            float ratio = NormalizedRecoil;
            SetRecoil(0f);
            RecoilDischarged?.Invoke(ratio);
            return ratio;
        }

        public float GetSkillDamageMultiplier(float dischargeRatio)
        {
            float t = GetCurvedDischargeRatio(dischargeRatio);
            return Mathf.Lerp(
                Mathf.Max(0f, emptyRecoilSkillDamageMultiplier),
                Mathf.Max(0f, fullRecoilSkillDamageMultiplier),
                t);
        }

        public float GetSkillKnockbackMultiplier(float dischargeRatio)
        {
            float t = GetCurvedDischargeRatio(dischargeRatio);
            return Mathf.Lerp(
                Mathf.Max(0f, emptyRecoilSkillKnockbackMultiplier),
                Mathf.Max(0f, fullRecoilSkillKnockbackMultiplier),
                t);
        }

        public void AddRecoil(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            SetRecoil(currentRecoil + amount);
        }

        public void Clear()
        {
            SetRecoil(0f);
        }

        private float GetCurvedDischargeRatio(float ratio)
        {
            return Mathf.Pow(Mathf.Clamp01(ratio), Mathf.Max(0.25f, skillDischargeCurvePower));
        }

        private float GetPenaltyRatio()
        {
            float start = Mathf.Clamp01(penaltyStartRatio);
            if (NormalizedRecoil <= start)
            {
                return 0f;
            }

            float ratio = Mathf.InverseLerp(start, 1f, NormalizedRecoil);
            return Mathf.Pow(ratio, Mathf.Max(0.25f, penaltyCurvePower));
        }

        private void SetRecoil(float value)
        {
            float previous = currentRecoil;
            currentRecoil = Mathf.Clamp(value, 0f, MaxRecoil);
            if (Mathf.Approximately(previous, currentRecoil))
            {
                return;
            }

            RecoilChanged?.Invoke(currentRecoil, MaxRecoil);
        }

        private void UpgradeTuningDefaults()
        {
            if (tuningVersion >= CurrentTuningVersion)
            {
                return;
            }

            if (Mathf.Approximately(basicAttackRecoilGain, 7f))
            {
                basicAttackRecoilGain = 4f;
            }

            if (Mathf.Approximately(comboStepGainBonus, 1.25f))
            {
                comboStepGainBonus = 0.65f;
            }

            if (Mathf.Approximately(basicAttackRecoilGain, 4f))
            {
                basicAttackRecoilGain = 2.5f;
            }

            if (Mathf.Approximately(comboStepGainBonus, 0.65f))
            {
                comboStepGainBonus = 0.35f;
            }

            if (Mathf.Approximately(penaltyStartRatio, 0.3f))
            {
                penaltyStartRatio = 0.25f;
            }

            if (Mathf.Approximately(fullRecoilMoveSpeedPenalty, 0.18f))
            {
                fullRecoilMoveSpeedPenalty = 0.34f;
            }

            if (Mathf.Approximately(fullRecoilAttackCooldownPenalty, 0.25f))
            {
                fullRecoilAttackCooldownPenalty = 0.7f;
            }

            if (Mathf.Approximately(fullRecoilDashCooldownPenalty, 0.2f))
            {
                fullRecoilDashCooldownPenalty = 0.55f;
            }

            if (Mathf.Approximately(penaltyCurvePower, 1.2f))
            {
                penaltyCurvePower = 0.85f;
            }

            tuningVersion = CurrentTuningVersion;
        }
    }
}
