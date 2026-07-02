using System;
using NeonBreaker.Pooling;
using UnityEngine;

namespace NeonBreaker.Rooms
{
    [CreateAssetMenu(menuName = "Neon Breaker/Rooms/Room Definition")]
    public sealed class RoomDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "Combat Room";
        [SerializeField] private RoomType roomType = RoomType.Combat;
        [SerializeField] private bool lockDoorsOnEnter = true;
        [SerializeField] private bool unlockExitOnClear = true;
        [SerializeField] private float clearDelay = 0.4f;
        [SerializeField] private EncounterWave[] waves;

        [Header("Clear Reward")]
        [SerializeField] private RoomRewardType clearReward = RoomRewardType.Auto;
        [SerializeField] private bool waitForUpgradeRewardBeforeExit = true;
        [SerializeField, Min(0f)] private float healAmount = 35f;
        [SerializeField, Range(0f, 1f)] private float healPercent = 0f;

        public string DisplayName => displayName;
        public RoomType RoomType => roomType;
        public bool LockDoorsOnEnter => lockDoorsOnEnter;
        public bool UnlockExitOnClear => unlockExitOnClear;
        public float ClearDelay => Mathf.Max(0f, clearDelay);
        public EncounterWave[] Waves => waves;
        public RoomRewardType ClearReward => clearReward;
        public RoomRewardType EffectiveClearReward => ResolveClearReward();
        public bool WaitForUpgradeRewardBeforeExit => waitForUpgradeRewardBeforeExit;
        public float HealAmount => Mathf.Max(0f, healAmount);
        public float HealPercent => Mathf.Clamp01(healPercent);
        public bool GrantsUpgradeReward => HasReward(RoomRewardType.Upgrade);
        public bool GrantsHealReward => HasReward(RoomRewardType.Heal);

        public bool HasEnemies
        {
            get
            {
                if (waves == null)
                {
                    return false;
                }

                for (int i = 0; i < waves.Length; i++)
                {
                    if (waves[i] != null && waves[i].HasEnemies)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private RoomRewardType ResolveClearReward()
        {
            if (clearReward != RoomRewardType.Auto)
            {
                return clearReward;
            }

            return roomType switch
            {
                RoomType.Combat => RoomRewardType.Upgrade,
                RoomType.Elite => RoomRewardType.Upgrade,
                RoomType.Reward => RoomRewardType.Upgrade,
                RoomType.Rest => RoomRewardType.Heal,
                _ => RoomRewardType.None
            };
        }

        private bool HasReward(RoomRewardType rewardType)
        {
            RoomRewardType effectiveReward = EffectiveClearReward;
            return effectiveReward == rewardType
                || effectiveReward == RoomRewardType.UpgradeAndHeal;
        }

        [Serializable]
        public sealed class EncounterWave
        {
            [SerializeField] private string displayName = "Wave";
            [SerializeField] private float startDelay = 0f;
            [SerializeField] private SpawnGroup[] spawnGroups;

            public string DisplayName => displayName;
            public float StartDelay => Mathf.Max(0f, startDelay);
            public SpawnGroup[] SpawnGroups => spawnGroups;

            public bool HasEnemies
            {
                get
                {
                    if (spawnGroups == null)
                    {
                        return false;
                    }

                    for (int i = 0; i < spawnGroups.Length; i++)
                    {
                        if (spawnGroups[i] != null && spawnGroups[i].Count > 0 && spawnGroups[i].PoolKey != null)
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }
        }

        [Serializable]
        public sealed class SpawnGroup
        {
            [SerializeField] private PoolKey poolKey;
            [SerializeField] private int count = 3;
            [SerializeField] private float spawnInterval = 0.25f;
            [SerializeField] private float startDelay = 0f;

            public PoolKey PoolKey => poolKey;
            public int Count => Mathf.Max(0, count);
            public float SpawnInterval => Mathf.Max(0f, spawnInterval);
            public float StartDelay => Mathf.Max(0f, startDelay);
        }
    }
}
