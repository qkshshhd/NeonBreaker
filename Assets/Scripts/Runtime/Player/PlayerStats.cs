using System;
using NeonBreaker.Combat;
using NeonBreaker.Skills;
using NeonBreaker.Upgrades;
using UnityEngine;

namespace NeonBreaker.Player
{
    public sealed class PlayerStats : MonoBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField] private PlayerSkillController skillController;
        [SerializeField] private PlayerRecoilCore recoilCore;

        private float damageMultiplier = 1f;
        private float moveSpeedMultiplier = 1f;
        private float attackCooldownMultiplier = 1f;
        private float attackRangeMultiplier = 1f;
        private float attackAngleMultiplier = 1f;
        private float knockbackMultiplier = 1f;
        private float criticalChance = 0.05f;
        private float criticalDamageMultiplier = 1.5f;
        private float dashCooldownMultiplier = 1f;
        private float dashDistanceMultiplier = 1f;
        private float dashShockwaveRadiusMultiplier = 1f;
        private int dashShockwaveLevel;
        private float skillCooldownMultiplier = 1f;
        private float skillDamageMultiplier = 1f;
        private float skillRadiusMultiplier = 1f;
        private float lifeStealPercent;
        private float skillShieldPerHit;

        public event Action StatsChanged;

        public float DamageMultiplier => damageMultiplier;
        public float MoveSpeedMultiplier => moveSpeedMultiplier;
        public float AttackCooldownMultiplier => attackCooldownMultiplier;
        public float AttackRangeMultiplier => attackRangeMultiplier;
        public float AttackAngleMultiplier => attackAngleMultiplier;
        public float KnockbackMultiplier => knockbackMultiplier;
        public float CriticalChance => criticalChance;
        public float CriticalDamageMultiplier => criticalDamageMultiplier;
        public float DashCooldownMultiplier => dashCooldownMultiplier;
        public float DashDistanceMultiplier => dashDistanceMultiplier;
        public float DashShockwaveRadiusMultiplier => dashShockwaveRadiusMultiplier;
        public int DashShockwaveLevel => dashShockwaveLevel;
        public float SkillCooldownMultiplier => skillCooldownMultiplier;
        public float SkillDamageMultiplier => skillDamageMultiplier;
        public float SkillRadiusMultiplier => skillRadiusMultiplier;
        public float LifeStealPercent => lifeStealPercent;
        public float SkillShieldPerHit => skillShieldPerHit;

        private void Awake()
        {
            if (health == null)
            {
                health = GetComponent<Health>();
            }

            if (skillController == null)
            {
                skillController = GetComponent<PlayerSkillController>();
            }

            if (recoilCore == null)
            {
                recoilCore = GetComponent<PlayerRecoilCore>();
            }
        }

        private void OnEnable()
        {
            if (skillController != null)
            {
                skillController.SkillHit += HandleSkillHit;
            }
        }

        private void OnDisable()
        {
            if (skillController != null)
            {
                skillController.SkillHit -= HandleSkillHit;
            }
        }

        public void ResetModifiers()
        {
            damageMultiplier = 1f;
            moveSpeedMultiplier = 1f;
            attackCooldownMultiplier = 1f;
            attackRangeMultiplier = 1f;
            attackAngleMultiplier = 1f;
            knockbackMultiplier = 1f;
            criticalChance = 0.05f;
            criticalDamageMultiplier = 1.5f;
            dashCooldownMultiplier = 1f;
            dashDistanceMultiplier = 1f;
            dashShockwaveRadiusMultiplier = 1f;
            dashShockwaveLevel = 0;
            skillCooldownMultiplier = 1f;
            skillDamageMultiplier = 1f;
            skillRadiusMultiplier = 1f;
            lifeStealPercent = 0f;
            skillShieldPerHit = 0f;
            health?.ClearShield();
            StatsChanged?.Invoke();
        }

        public float GetDamage(float baseDamage)
        {
            return Mathf.Max(0f, baseDamage * damageMultiplier);
        }

        public float RollAttackDamage(float baseDamage, out bool isCritical)
        {
            float finalDamage = GetDamage(baseDamage);
            isCritical = UnityEngine.Random.value < criticalChance;
            return isCritical ? finalDamage * criticalDamageMultiplier : finalDamage;
        }

        public float GetMoveSpeed(float baseMoveSpeed)
        {
            float recoilMultiplier = recoilCore != null ? recoilCore.GetMoveSpeedMultiplier() : 1f;
            return Mathf.Max(0f, baseMoveSpeed * moveSpeedMultiplier * recoilMultiplier);
        }

        public float GetAttackCooldown(float baseCooldown)
        {
            float recoilMultiplier = recoilCore != null ? recoilCore.GetAttackCooldownMultiplier() : 1f;
            return Mathf.Max(0.02f, baseCooldown * attackCooldownMultiplier * recoilMultiplier);
        }

        public float GetAttackRange(float baseRange)
        {
            return Mathf.Max(0.05f, baseRange * attackRangeMultiplier);
        }

        public float GetAttackAngle(float baseAngle)
        {
            return Mathf.Clamp(baseAngle * attackAngleMultiplier, 1f, 360f);
        }

        public float GetKnockback(float baseKnockback)
        {
            return Mathf.Max(0f, baseKnockback * knockbackMultiplier);
        }

        public float GetDashCooldown(float baseCooldown)
        {
            float recoilMultiplier = recoilCore != null ? recoilCore.GetDashCooldownMultiplier() : 1f;
            return Mathf.Max(0.02f, baseCooldown * dashCooldownMultiplier * recoilMultiplier);
        }

        public float GetDashDistance(float baseDistance)
        {
            float recoilMultiplier = recoilCore != null ? recoilCore.GetDashDistanceMultiplier() : 1f;
            return Mathf.Max(0.05f, baseDistance * dashDistanceMultiplier * recoilMultiplier);
        }

        public float GetDashShockwaveDamage(float baseDamage)
        {
            if (dashShockwaveLevel <= 0)
            {
                return 0f;
            }

            float levelBonus = 1f + (dashShockwaveLevel - 1) * 0.35f;
            return GetDamage(baseDamage) * levelBonus;
        }

        public float GetDashShockwaveRadius(float baseRadius)
        {
            return Mathf.Max(0.05f, baseRadius * dashShockwaveRadiusMultiplier);
        }

        public float GetSkillCooldown(float baseCooldown)
        {
            return Mathf.Max(0.05f, baseCooldown * skillCooldownMultiplier);
        }

        public float GetSkillDamage(float baseDamage)
        {
            return Mathf.Max(0f, GetDamage(baseDamage) * skillDamageMultiplier);
        }

        public float GetSkillRadius(float baseRadius)
        {
            return Mathf.Max(0.05f, baseRadius * skillRadiusMultiplier);
        }

        public void NotifyDamageDealt(float damageAmount)
        {
            if (health == null || lifeStealPercent <= 0f || damageAmount <= 0f)
            {
                return;
            }

            health.Heal(damageAmount * lifeStealPercent);
        }

        public void Apply(UpgradeEffectType effectType, float value)
        {
            switch (effectType)
            {
                case UpgradeEffectType.AddDamagePercent:
                    damageMultiplier += Mathf.Max(0f, value);
                    break;
                case UpgradeEffectType.AddMoveSpeedPercent:
                    moveSpeedMultiplier += Mathf.Max(0f, value);
                    break;
                case UpgradeEffectType.ReduceAttackCooldownPercent:
                    attackCooldownMultiplier = Mathf.Max(0.1f, attackCooldownMultiplier - Mathf.Max(0f, value));
                    break;
                case UpgradeEffectType.AddAttackRangePercent:
                    attackRangeMultiplier += Mathf.Max(0f, value);
                    break;
                case UpgradeEffectType.AddCriticalChance:
                    criticalChance = Mathf.Clamp01(criticalChance + Mathf.Max(0f, value));
                    break;
                case UpgradeEffectType.AddCriticalDamagePercent:
                    criticalDamageMultiplier += Mathf.Max(0f, value);
                    break;
                case UpgradeEffectType.ReduceDashCooldownPercent:
                    dashCooldownMultiplier = Mathf.Max(0.1f, dashCooldownMultiplier - Mathf.Max(0f, value));
                    break;
                case UpgradeEffectType.AddDashDistancePercent:
                    dashDistanceMultiplier += Mathf.Max(0f, value);
                    break;
                case UpgradeEffectType.EnableDashShockwave:
                    dashShockwaveLevel += Mathf.Max(1, Mathf.RoundToInt(value));
                    EnsureDashShockwaveComponent();
                    break;
                case UpgradeEffectType.AddMaxHealth:
                    health?.IncreaseMaxHealth(value, true);
                    break;
                case UpgradeEffectType.HealFlat:
                    health?.Heal(value);
                    break;
                case UpgradeEffectType.ReduceSkillCooldownPercent:
                    skillCooldownMultiplier = Mathf.Max(0.1f, skillCooldownMultiplier - Mathf.Max(0f, value));
                    break;
                case UpgradeEffectType.AddAttackAnglePercent:
                    attackAngleMultiplier += Mathf.Max(0f, value);
                    break;
                case UpgradeEffectType.AddKnockbackPercent:
                    knockbackMultiplier += Mathf.Max(0f, value);
                    break;
                case UpgradeEffectType.AddSkillDamagePercent:
                    skillDamageMultiplier += Mathf.Max(0f, value);
                    break;
                case UpgradeEffectType.AddSkillRadiusPercent:
                    skillRadiusMultiplier += Mathf.Max(0f, value);
                    break;
                case UpgradeEffectType.AddDashShockwaveRadiusPercent:
                    dashShockwaveRadiusMultiplier += Mathf.Max(0f, value);
                    break;
                case UpgradeEffectType.AddLifeStealPercent:
                    lifeStealPercent = Mathf.Clamp01(lifeStealPercent + Mathf.Max(0f, value));
                    break;
                case UpgradeEffectType.AddHitInvulnerabilityDuration:
                    health?.IncreaseInvulnerabilityDuration(value);
                    break;
                case UpgradeEffectType.AddSkillShieldPerHit:
                    skillShieldPerHit += Mathf.Max(0f, value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(effectType), effectType, null);
            }

            StatsChanged?.Invoke();
        }

        private void HandleSkillHit(SkillDefinition skill, int hitCount)
        {
            if (health == null || skillShieldPerHit <= 0f || hitCount <= 0)
            {
                return;
            }

            health.AddShield(skillShieldPerHit * hitCount);
        }

        private void EnsureDashShockwaveComponent()
        {
            if (GetComponent<PlayerDashShockwave2D>() == null)
            {
                gameObject.AddComponent<PlayerDashShockwave2D>();
            }
        }
    }
}
