using System;
using System.Collections.Generic;
using NeonBreaker.Player;
using NeonBreaker.Rooms;
using UnityEngine;

namespace NeonBreaker.Upgrades
{
    public sealed class UpgradeManager : MonoBehaviour
    {
        [SerializeField] private RoomRunManager runManager;
        [SerializeField] private PlayerStats playerStats;
        [SerializeField] private UpgradeDefinition[] upgradePool;
        [SerializeField] private UpgradeDefinition[] eliteUpgradePool;
        [SerializeField] private bool useDefaultMvpUpgradesWhenPoolIsEmpty = true;
        [SerializeField] private bool createDefaultChoiceUi = true;
        [SerializeField] private bool pauseGameWhileChoosing = true;
        [SerializeField] private bool offerChoicesImmediately = true;
        [SerializeField, Min(1)] private int choicesPerReward = 3;
        [SerializeField] private bool offerRewardAfterFinalRoom = false;
        [SerializeField] private bool logUpgradeFlow = true;

        private readonly List<UpgradeDefinition> currentChoices = new List<UpgradeDefinition>();
        private readonly Dictionary<UpgradeDefinition, int> upgradeLevels = new Dictionary<UpgradeDefinition, int>();
        private readonly List<UpgradeRecord> acquiredUpgrades = new List<UpgradeRecord>();
        private readonly Dictionary<UpgradeDefinition, int> acquiredUpgradeIndices = new Dictionary<UpgradeDefinition, int>();
        private UpgradeDefinition[] defaultMvpUpgrades;
        private UpgradeDefinition[] defaultEliteUpgrades;
        private bool hasActiveChoices;
        private bool choicesVisible;
        private float previousTimeScale = 1f;
        private bool pausedByReward;

        public event Action<IReadOnlyList<UpgradeDefinition>> ChoicesOffered;
        public event Action<int, RoomDefinition> RewardChoicesPrepared;
        public event Action<UpgradeDefinition> UpgradeSelected;
        public event Action<UpgradeDefinition, int> UpgradeLevelChanged;
        public event Action<IReadOnlyList<UpgradeRecord>> AcquiredUpgradesChanged;

        public IReadOnlyList<UpgradeDefinition> CurrentChoices => currentChoices;
        public IReadOnlyList<UpgradeRecord> AcquiredUpgrades => acquiredUpgrades;
        public bool HasActiveChoices => hasActiveChoices;
        public bool ChoicesVisible => choicesVisible;

        private void Awake()
        {
            if (runManager == null)
            {
                runManager = FindAnyObjectByType<RoomRunManager>();
            }

            if (playerStats == null)
            {
                playerStats = FindAnyObjectByType<PlayerStats>();
            }

            if (runManager != null)
            {
                runManager.SetRewardGateEnabled(true);
            }

            BuildDefaultMvpUpgrades();
            BuildDefaultEliteUpgrades();

            if (createDefaultChoiceUi && FindAnyObjectByType<UpgradeChoiceUI>() == null)
            {
                gameObject.AddComponent<UpgradeChoiceUI>();
            }
        }

        private void OnEnable()
        {
            if (runManager != null)
            {
                runManager.RunRoomCombatCleared += HandleRoomCombatCleared;
            }
        }

        private void OnDisable()
        {
            if (runManager != null)
            {
                runManager.RunRoomCombatCleared -= HandleRoomCombatCleared;
            }

            RestoreTimeScale();
        }

        public void SetOfferChoicesImmediately(bool enabled)
        {
            offerChoicesImmediately = enabled;
        }

        public void SelectChoice(int choiceIndex)
        {
            if (!hasActiveChoices || choiceIndex < 0 || choiceIndex >= currentChoices.Count)
            {
                return;
            }

            UpgradeDefinition selected = currentChoices[choiceIndex];
            selected?.Apply(playerStats);
            int newLevel = IncreaseUpgradeLevel(selected);

            if (logUpgradeFlow && selected != null)
            {
                Debug.Log($"[UpgradeManager] Selected upgrade: {selected.DisplayName} Lv.{newLevel}", selected);
            }

            hasActiveChoices = false;
            choicesVisible = false;
            currentChoices.Clear();
            RestoreTimeScale();
            UpgradeSelected?.Invoke(selected);
            runManager?.CompletePendingReward();
        }

        public void ShowPreparedChoices()
        {
            if (!hasActiveChoices || currentChoices.Count == 0 || choicesVisible)
            {
                return;
            }

            choicesVisible = true;
            PauseTimeScale();
            ChoicesOffered?.Invoke(currentChoices);
        }

        public int GetUpgradeLevel(UpgradeDefinition upgrade)
        {
            if (upgrade == null)
            {
                return 0;
            }

            return upgradeLevels.TryGetValue(upgrade, out int level) ? level : 0;
        }

        public bool IsUpgradeAtMaxLevel(UpgradeDefinition upgrade)
        {
            if (upgrade == null || !upgrade.HasMaxLevel)
            {
                return false;
            }

            return GetUpgradeLevel(upgrade) >= upgrade.MaxLevel;
        }

        private void HandleRoomCombatCleared(int roomIndex, RoomDefinition room)
        {
            if (!ShouldOfferReward(room))
            {
                runManager?.CompletePendingReward();
                return;
            }

            BuildChoices(room);
            if (currentChoices.Count == 0)
            {
                Debug.LogWarning("[UpgradeManager] No valid upgrade choices were found. Completing reward step automatically.", this);
                runManager?.CompletePendingReward();
                return;
            }

            hasActiveChoices = true;
            choicesVisible = false;

            if (offerChoicesImmediately)
            {
                ShowPreparedChoices();
            }
            else
            {
                RewardChoicesPrepared?.Invoke(roomIndex, room);
            }

            if (logUpgradeFlow)
            {
                string mode = offerChoicesImmediately ? "immediately" : "waiting for reward pickup";
                string rewardType = room != null && room.RoomType == RoomType.Elite ? "elite" : "normal";
                Debug.Log($"[UpgradeManager] Prepared {currentChoices.Count} {rewardType} upgrade choices after room {roomIndex} ({mode}).", this);
            }
        }

        private bool ShouldOfferReward(RoomDefinition room)
        {
            if (playerStats == null || room == null || !room.GrantsUpgradeReward)
            {
                return false;
            }

            if (!offerRewardAfterFinalRoom && runManager != null && !runManager.HasNextRoom)
            {
                return false;
            }

            return GetAvailableUpgradePool(room).Length > 0;
        }

        private void BuildChoices(RoomDefinition room)
        {
            currentChoices.Clear();
            UpgradeDefinition[] availableUpgrades = GetAvailableUpgradePool(room);
            if (availableUpgrades.Length == 0)
            {
                return;
            }

            int safeChoiceCount = Mathf.Min(Mathf.Max(1, choicesPerReward), availableUpgrades.Length);
            int guard = 0;

            while (currentChoices.Count < safeChoiceCount && guard < availableUpgrades.Length * 8)
            {
                guard++;
                UpgradeDefinition candidate = availableUpgrades[UnityEngine.Random.Range(0, availableUpgrades.Length)];
                if (candidate == null || currentChoices.Contains(candidate) || IsUpgradeAtMaxLevel(candidate))
                {
                    continue;
                }

                currentChoices.Add(candidate);
            }

            if (currentChoices.Count < safeChoiceCount)
            {
                for (int i = 0; i < availableUpgrades.Length && currentChoices.Count < safeChoiceCount; i++)
                {
                    UpgradeDefinition candidate = availableUpgrades[i];
                    if (candidate != null && !currentChoices.Contains(candidate) && !IsUpgradeAtMaxLevel(candidate))
                    {
                        currentChoices.Add(candidate);
                    }
                }
            }
        }

        private UpgradeDefinition[] GetAvailableUpgradePool(RoomDefinition room)
        {
            if (room != null && room.RoomType == RoomType.Elite)
            {
                if (eliteUpgradePool != null && eliteUpgradePool.Length > 0)
                {
                    return eliteUpgradePool;
                }

                if (!useDefaultMvpUpgradesWhenPoolIsEmpty)
                {
                    return Array.Empty<UpgradeDefinition>();
                }

                if (defaultEliteUpgrades == null || defaultEliteUpgrades.Length == 0)
                {
                    BuildDefaultEliteUpgrades();
                }

                return defaultEliteUpgrades ?? Array.Empty<UpgradeDefinition>();
            }

            if (upgradePool != null && upgradePool.Length > 0)
            {
                return upgradePool;
            }

            if (!useDefaultMvpUpgradesWhenPoolIsEmpty)
            {
                return Array.Empty<UpgradeDefinition>();
            }

            if (defaultMvpUpgrades == null || defaultMvpUpgrades.Length == 0)
            {
                BuildDefaultMvpUpgrades();
            }

            return defaultMvpUpgrades ?? Array.Empty<UpgradeDefinition>();
        }

        private void BuildDefaultMvpUpgrades()
        {
            if (defaultMvpUpgrades != null && defaultMvpUpgrades.Length > 0)
            {
                return;
            }

            defaultMvpUpgrades = new[]
            {
                UpgradeDefinition.CreateRuntime(
                    "오버클럭 블레이드",
                    "기본 공격 피해가 15% 증가합니다.",
                    5,
                    new UpgradeDefinition.UpgradeEffect(UpgradeEffectType.AddDamagePercent, 0.15f)),

                UpgradeDefinition.CreateRuntime(
                    "가속 베기",
                    "기본 공격 쿨타임이 10% 감소합니다.",
                    5,
                    new UpgradeDefinition.UpgradeEffect(UpgradeEffectType.ReduceAttackCooldownPercent, 0.10f)),

                UpgradeDefinition.CreateRuntime(
                    "확장형 에너지 엣지",
                    "기본 공격 사거리가 12% 증가합니다.",
                    4,
                    new UpgradeDefinition.UpgradeEffect(UpgradeEffectType.AddAttackRangePercent, 0.12f)),

                UpgradeDefinition.CreateRuntime(
                    "약점 스캔",
                    "치명타 확률이 8% 증가합니다.",
                    5,
                    new UpgradeDefinition.UpgradeEffect(UpgradeEffectType.AddCriticalChance, 0.08f)),

                UpgradeDefinition.CreateRuntime(
                    "코어 관통",
                    "치명타 피해가 25% 증가합니다.",
                    4,
                    new UpgradeDefinition.UpgradeEffect(UpgradeEffectType.AddCriticalDamagePercent, 0.25f)),

                UpgradeDefinition.CreateRuntime(
                    "경량 프레임",
                    "이동 속도가 8% 증가합니다.",
                    4,
                    new UpgradeDefinition.UpgradeEffect(UpgradeEffectType.AddMoveSpeedPercent, 0.08f)),

                UpgradeDefinition.CreateRuntime(
                    "리미터 해제",
                    "대시 쿨타임이 12% 감소합니다.",
                    4,
                    new UpgradeDefinition.UpgradeEffect(UpgradeEffectType.ReduceDashCooldownPercent, 0.12f)),

                UpgradeDefinition.CreateRuntime(
                    "장거리 점멸",
                    "대시 거리가 10% 증가합니다.",
                    4,
                    new UpgradeDefinition.UpgradeEffect(UpgradeEffectType.AddDashDistancePercent, 0.10f)),

                UpgradeDefinition.CreateRuntime(
                    "대시 충격파",
                    "대시가 끝나는 지점에 적을 밀어내는 충격파가 발생합니다.",
                    3,
                    new UpgradeDefinition.UpgradeEffect(UpgradeEffectType.EnableDashShockwave, 1f)),

                UpgradeDefinition.CreateRuntime(
                    "강화 생체 코어",
                    "최대 체력이 20 증가하고 증가량만큼 즉시 회복합니다.",
                    4,
                    new UpgradeDefinition.UpgradeEffect(UpgradeEffectType.AddMaxHealth, 20f)),

                UpgradeDefinition.CreateRuntime(
                    "응급 나노 회복",
                    "체력을 즉시 30 회복합니다.",
                    0,
                    new UpgradeDefinition.UpgradeEffect(UpgradeEffectType.HealFlat, 30f)),

                UpgradeDefinition.CreateRuntime(
                    "보조 회로 냉각",
                    "스킬 쿨타임이 15% 감소합니다.",
                    4,
                    new UpgradeDefinition.UpgradeEffect(UpgradeEffectType.ReduceSkillCooldownPercent, 0.15f)),

                UpgradeDefinition.CreateRuntime(
                    "와이드 아크",
                    "기본 공격 각도가 18% 넓어집니다.",
                    4,
                    new UpgradeDefinition.UpgradeEffect(UpgradeEffectType.AddAttackAnglePercent, 0.18f)),

                UpgradeDefinition.CreateRuntime(
                    "펄스 넉백",
                    "공격과 스킬의 넉백이 20% 증가합니다.",
                    4,
                    new UpgradeDefinition.UpgradeEffect(UpgradeEffectType.AddKnockbackPercent, 0.20f)),

                UpgradeDefinition.CreateRuntime(
                    "쇼크 코어 증폭",
                    "스킬 피해가 18% 증가합니다.",
                    5,
                    new UpgradeDefinition.UpgradeEffect(UpgradeEffectType.AddSkillDamagePercent, 0.18f)),

                UpgradeDefinition.CreateRuntime(
                    "확산형 쇼크웨이브",
                    "스킬 범위가 15% 증가합니다.",
                    4,
                    new UpgradeDefinition.UpgradeEffect(UpgradeEffectType.AddSkillRadiusPercent, 0.15f)),

                UpgradeDefinition.CreateRuntime(
                    "과열된 충격파",
                    "대시 충격파의 범위가 18% 증가합니다.",
                    3,
                    new UpgradeDefinition.UpgradeEffect(UpgradeEffectType.AddDashShockwaveRadiusPercent, 0.18f)),

                UpgradeDefinition.CreateRuntime(
                    "반응형 실드",
                    "피격 후 무적 시간이 0.08초 증가합니다.",
                    3,
                    new UpgradeDefinition.UpgradeEffect(UpgradeEffectType.AddHitInvulnerabilityDuration, 0.08f))
            };
        }

        private void BuildDefaultEliteUpgrades()
        {
            if (defaultEliteUpgrades != null && defaultEliteUpgrades.Length > 0)
            {
                return;
            }

            defaultEliteUpgrades = new[]
            {
                UpgradeDefinition.CreateRuntime(
                    "흡수형 블레이드",
                    "가한 피해의 3%만큼 체력을 회복합니다.",
                    4,
                    new UpgradeDefinition.UpgradeEffect(UpgradeEffectType.AddLifeStealPercent, 0.03f)),

                UpgradeDefinition.CreateRuntime(
                    "공명 보호막",
                    "스킬로 맞춘 대상 하나당 방어막을 6 획득합니다. 방어막은 최대 체력의 60%까지 쌓입니다.",
                    4,
                    new UpgradeDefinition.UpgradeEffect(UpgradeEffectType.AddSkillShieldPerHit, 6f)),

                UpgradeDefinition.CreateRuntime(
                    "과충전 쇼크 코어",
                    "스킬 피해가 30% 증가하고 스킬 쿨타임이 15% 감소합니다.",
                    3,
                    new UpgradeDefinition.UpgradeEffect(UpgradeEffectType.AddSkillDamagePercent, 0.30f),
                    new UpgradeDefinition.UpgradeEffect(UpgradeEffectType.ReduceSkillCooldownPercent, 0.15f)),

                UpgradeDefinition.CreateRuntime(
                    "붕괴장 증폭기",
                    "스킬 범위가 25% 증가하고 스킬 피해가 15% 증가합니다.",
                    3,
                    new UpgradeDefinition.UpgradeEffect(UpgradeEffectType.AddSkillRadiusPercent, 0.25f),
                    new UpgradeDefinition.UpgradeEffect(UpgradeEffectType.AddSkillDamagePercent, 0.15f)),

                UpgradeDefinition.CreateRuntime(
                    "리퍼 프로토콜",
                    "치명타 확률이 15%, 치명타 피해가 40% 증가합니다.",
                    3,
                    new UpgradeDefinition.UpgradeEffect(UpgradeEffectType.AddCriticalChance, 0.15f),
                    new UpgradeDefinition.UpgradeEffect(UpgradeEffectType.AddCriticalDamagePercent, 0.40f)),

                UpgradeDefinition.CreateRuntime(
                    "위상 질주 회로",
                    "대시 쿨타임이 20% 감소하고 대시 거리가 15% 증가합니다.",
                    3,
                    new UpgradeDefinition.UpgradeEffect(UpgradeEffectType.ReduceDashCooldownPercent, 0.20f),
                    new UpgradeDefinition.UpgradeEffect(UpgradeEffectType.AddDashDistancePercent, 0.15f)),

                UpgradeDefinition.CreateRuntime(
                    "전투 지속 코어",
                    "최대 체력이 35 증가하고 피격 후 무적 시간이 0.12초 증가합니다.",
                    3,
                    new UpgradeDefinition.UpgradeEffect(UpgradeEffectType.AddMaxHealth, 35f),
                    new UpgradeDefinition.UpgradeEffect(UpgradeEffectType.AddHitInvulnerabilityDuration, 0.12f)),

                UpgradeDefinition.CreateRuntime(
                    "압축 충격 엔진",
                    "대시 충격파가 활성화되고 충격파 범위가 25% 증가합니다.",
                    2,
                    new UpgradeDefinition.UpgradeEffect(UpgradeEffectType.EnableDashShockwave, 1f),
                    new UpgradeDefinition.UpgradeEffect(UpgradeEffectType.AddDashShockwaveRadiusPercent, 0.25f))
            };
        }

        private int IncreaseUpgradeLevel(UpgradeDefinition upgrade)
        {
            if (upgrade == null)
            {
                return 0;
            }

            int newLevel = GetUpgradeLevel(upgrade) + 1;
            upgradeLevels[upgrade] = newLevel;
            UpsertAcquiredUpgrade(upgrade, newLevel);
            UpgradeLevelChanged?.Invoke(upgrade, newLevel);
            AcquiredUpgradesChanged?.Invoke(acquiredUpgrades);
            return newLevel;
        }

        private void UpsertAcquiredUpgrade(UpgradeDefinition upgrade, int level)
        {
            if (acquiredUpgradeIndices.TryGetValue(upgrade, out int index))
            {
                acquiredUpgrades[index] = new UpgradeRecord(upgrade, level, acquiredUpgrades[index].PickCount + 1);
                return;
            }

            acquiredUpgradeIndices[upgrade] = acquiredUpgrades.Count;
            acquiredUpgrades.Add(new UpgradeRecord(upgrade, level, 1));
        }

        private void PauseTimeScale()
        {
            if (!pauseGameWhileChoosing || pausedByReward)
            {
                return;
            }

            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            pausedByReward = true;
        }

        private void RestoreTimeScale()
        {
            if (!pausedByReward)
            {
                return;
            }

            Time.timeScale = previousTimeScale;
            pausedByReward = false;
        }

        public readonly struct UpgradeRecord
        {
            public UpgradeRecord(UpgradeDefinition definition, int level, int pickCount)
            {
                Definition = definition;
                Level = level;
                PickCount = pickCount;
            }

            public UpgradeDefinition Definition { get; }
            public int Level { get; }
            public int PickCount { get; }
        }
    }
}
