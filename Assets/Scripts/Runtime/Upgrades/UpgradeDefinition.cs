using System;
using NeonBreaker.Player;
using UnityEngine;

namespace NeonBreaker.Upgrades
{
    public enum UpgradeEffectType
    {
        AddDamagePercent,
        AddMoveSpeedPercent,
        ReduceAttackCooldownPercent,
        AddAttackRangePercent,
        AddCriticalChance,
        AddCriticalDamagePercent,
        ReduceDashCooldownPercent,
        AddDashDistancePercent,
        EnableDashShockwave,
        AddMaxHealth,
        HealFlat,
        ReduceSkillCooldownPercent,
        AddAttackAnglePercent,
        AddKnockbackPercent,
        AddSkillDamagePercent,
        AddSkillRadiusPercent,
        AddDashShockwaveRadiusPercent,
        AddLifeStealPercent,
        AddHitInvulnerabilityDuration,
        AddSkillShieldPerHit
    }

    [CreateAssetMenu(menuName = "Neon Breaker/Upgrades/Upgrade Definition")]
    public sealed class UpgradeDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "Upgrade";
        [SerializeField, TextArea] private string description = "Upgrade description.";
        [SerializeField] private Sprite icon;
        [SerializeField, Min(0)] private int maxLevel = 0;
        [SerializeField] private UpgradeEffect[] effects;

        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public int MaxLevel => Mathf.Max(0, maxLevel);
        public UpgradeEffect[] Effects => effects;
        public bool HasMaxLevel => MaxLevel > 0;

        public static UpgradeDefinition CreateRuntime(string upgradeName, string upgradeDescription, int upgradeMaxLevel, params UpgradeEffect[] upgradeEffects)
        {
            UpgradeDefinition definition = CreateInstance<UpgradeDefinition>();
            definition.displayName = upgradeName;
            definition.description = upgradeDescription;
            definition.maxLevel = Mathf.Max(0, upgradeMaxLevel);
            definition.effects = upgradeEffects;
            return definition;
        }

        public void Apply(PlayerStats target)
        {
            if (target == null || effects == null)
            {
                return;
            }

            for (int i = 0; i < effects.Length; i++)
            {
                UpgradeEffect effect = effects[i];
                if (effect != null)
                {
                    target.Apply(effect.EffectType, effect.Value);
                }
            }
        }

        [Serializable]
        public sealed class UpgradeEffect
        {
            [SerializeField] private UpgradeEffectType effectType;
            [SerializeField] private float value = 0.1f;

            public UpgradeEffectType EffectType => effectType;
            public float Value => value;

            public UpgradeEffect()
            {
            }

            public UpgradeEffect(UpgradeEffectType effectType, float value)
            {
                this.effectType = effectType;
                this.value = value;
            }
        }
    }
}
