using System;
using System.Collections;
using System.Collections.Generic;
using NeonBreaker.Combat;
using NeonBreaker.Enemies;
using NeonBreaker.Player;
using NeonBreaker.Pooling;
using NeonBreaker.Rooms;
using NeonBreaker.Skills;
using NeonBreaker.Upgrades;
using UnityEngine;

namespace NeonBreaker.Tutorial
{
    public sealed class TutorialManager : MonoBehaviour
    {
        private enum TutorialStep
        {
            Move,
            BasicAttack,
            Combo,
            Dash,
            BuildRecoil,
            SkillDischarge,
            ClearRoom,
            SelectUpgrade,
            ExitRoom,
            Complete
        }

        [Header("Sources")]
        [SerializeField] private PlayerController player;
        [SerializeField] private MeleeAttack2D meleeAttack;
        [SerializeField] private PlayerDash2D dash;
        [SerializeField] private PlayerSkillController skillController;
        [SerializeField] private PlayerRecoilCore recoilCore;
        [SerializeField] private RoomManager roomManager;
        [SerializeField] private RoomRunManager runManager;
        [SerializeField] private EnemySpawner enemySpawner;
        [SerializeField] private UpgradeManager upgradeManager;
        [SerializeField] private TutorialHintUI hintUI;

        [Header("Options")]
        [SerializeField] private bool autoStart = true;
        [SerializeField] private bool forceTutorial;
        [SerializeField] private bool saveCompletion = true;
        [SerializeField] private bool createDefaultHintUi = true;
        [SerializeField] private bool skipSkillStepWhenNoSkill = true;
        [SerializeField] private bool skipUpgradeStepWhenNoUpgradeManager = true;
        [SerializeField] private bool useFirstRoomAsTutorialStage = true;
        [SerializeField] private bool holdFirstRoomCombatUntilSkillLesson = true;
        [SerializeField] private bool hideHintDuringCombat = true;
        [SerializeField, Min(0f)] private float combatHintVisibleTime = 1.35f;
        [SerializeField] private bool spawnPracticeEnemyForRecoil = true;
        [SerializeField] private PoolKey practiceEnemyPoolKey;
        [SerializeField, Min(1)] private int practiceEnemyCount = 1;
        [SerializeField, Min(1f)] private float practiceEnemyMaxHealth = 999f;
        [SerializeField, Min(0f)] private float practiceEnemyInvulnerability = 0f;
        [SerializeField] private bool suppressPracticeEnemyBehavior = true;
        [SerializeField, Min(0.5f)] private float practiceEnemySpawnDistance = 2.4f;
        [SerializeField] private string completionPrefsKey = "NeonBreaker.Tutorial.Completed";
        [SerializeField, Min(0f)] private float stepAdvanceDelay = 0.18f;
        [SerializeField, Min(0f)] private float minimumStepVisibleTime = 1.15f;
        [SerializeField, Min(0.1f)] private float movementDistanceToComplete = 1.4f;
        [SerializeField, Range(0.01f, 1f)] private float recoilRatioToTeachSkill = 0.75f;
        [SerializeField, Min(1)] private int comboStepToComplete = 3;

        private TutorialStep currentStep;
        private Vector3 movementStartPosition;
        private int startRoomIndex;
        private float nextAdvanceTime;
        private float currentStepShownAt;
        private bool isRunning;
        private bool combatStartLocked;
        private bool practiceEnemySpawnRequested;
        private int alivePracticeEnemyCount;
        private readonly List<EnemyController> practiceEnemies = new List<EnemyController>();
        private Coroutine combatHintHideRoutine;

        public bool IsRunning => isRunning;
        public bool IsCompleted => currentStep == TutorialStep.Complete;

        private void Awake()
        {
            ResolveReferences();

            if (createDefaultHintUi && hintUI == null)
            {
                hintUI = gameObject.AddComponent<TutorialHintUI>();
            }
        }

        private void OnEnable()
        {
            Subscribe();

            if (autoStart)
            {
                TryStartTutorial();
            }
        }

        private void OnDisable()
        {
            if (combatHintHideRoutine != null)
            {
                StopCoroutine(combatHintHideRoutine);
                combatHintHideRoutine = null;
            }

            Unsubscribe();
        }

        private void Start()
        {
            if (autoStart)
            {
                TryStartTutorial();
            }
        }

        private void Update()
        {
            if (!isRunning || Time.unscaledTime < nextAdvanceTime)
            {
                return;
            }

            if (currentStep == TutorialStep.Move && player != null)
            {
                float movedDistance = Vector2.Distance(movementStartPosition, player.transform.position);
                if (movedDistance >= movementDistanceToComplete)
                {
                    CompleteCurrentStep();
                }
            }
        }

        [ContextMenu("Start Tutorial")]
        public void TryStartTutorial()
        {
            if (isRunning || currentStep == TutorialStep.Complete)
            {
                return;
            }

            ResolveReferences();

            if (!forceTutorial && saveCompletion && PlayerPrefs.GetInt(completionPrefsKey, 0) == 1)
            {
                FinishTutorial(showFinalMessage: false);
                return;
            }

            isRunning = true;
            currentStep = TutorialStep.Move;
            startRoomIndex = runManager != null ? runManager.CurrentRoomIndex : -1;
            movementStartPosition = player != null ? player.transform.position : transform.position;
            ShowCurrentStep();
        }

        [ContextMenu("Reset Tutorial Save")]
        public void ResetTutorialSave()
        {
            PlayerPrefs.DeleteKey(completionPrefsKey);
            PlayerPrefs.Save();
        }

        public void SkipTutorial()
        {
            FinishTutorial(showFinalMessage: false);
        }

        private void ResolveReferences()
        {
            if (player == null)
            {
                player = FindAnyObjectByType<PlayerController>();
            }

            if (player != null)
            {
                meleeAttack ??= player.Attack;
                dash ??= player.Dash;
                skillController ??= player.SkillController;
                recoilCore ??= player.RecoilCore;
            }

            if (meleeAttack == null)
            {
                meleeAttack = FindAnyObjectByType<MeleeAttack2D>();
            }

            if (dash == null)
            {
                dash = FindAnyObjectByType<PlayerDash2D>();
            }

            if (skillController == null)
            {
                skillController = FindAnyObjectByType<PlayerSkillController>();
            }

            if (recoilCore == null)
            {
                recoilCore = FindAnyObjectByType<PlayerRecoilCore>();
            }

            if (runManager == null)
            {
                runManager = FindAnyObjectByType<RoomRunManager>();
            }

            if (roomManager == null)
            {
                roomManager = FindAnyObjectByType<RoomManager>();
            }

            if (enemySpawner == null)
            {
                enemySpawner = FindAnyObjectByType<EnemySpawner>();
            }

            if (upgradeManager == null)
            {
                upgradeManager = FindAnyObjectByType<UpgradeManager>();
            }

            if (hintUI == null)
            {
                hintUI = FindAnyObjectByType<TutorialHintUI>();
            }
        }

        private void Subscribe()
        {
            ResolveReferences();

            if (meleeAttack != null)
            {
                meleeAttack.AttackStarted += HandleAttackStarted;
            }

            if (dash != null)
            {
                dash.DashStarted += HandleDashStarted;
            }

            if (skillController != null)
            {
                skillController.SkillStarted += HandleSkillStarted;
            }

            if (recoilCore != null)
            {
                recoilCore.RecoilChanged += HandleRecoilChanged;
            }

            if (runManager != null)
            {
                runManager.RunRoomCombatCleared += HandleRunRoomCombatCleared;
                runManager.RunRoomStarted += HandleRunRoomStarted;
            }

            if (roomManager != null)
            {
                roomManager.RoomStarted += HandleRoomStarted;
            }

            if (upgradeManager != null)
            {
                upgradeManager.UpgradeSelected += HandleUpgradeSelected;
            }
        }

        private void Unsubscribe()
        {
            if (meleeAttack != null)
            {
                meleeAttack.AttackStarted -= HandleAttackStarted;
            }

            if (dash != null)
            {
                dash.DashStarted -= HandleDashStarted;
            }

            if (skillController != null)
            {
                skillController.SkillStarted -= HandleSkillStarted;
            }

            if (recoilCore != null)
            {
                recoilCore.RecoilChanged -= HandleRecoilChanged;
            }

            if (runManager != null)
            {
                runManager.RunRoomCombatCleared -= HandleRunRoomCombatCleared;
                runManager.RunRoomStarted -= HandleRunRoomStarted;
            }

            if (roomManager != null)
            {
                roomManager.RoomStarted -= HandleRoomStarted;
            }

            if (upgradeManager != null)
            {
                upgradeManager.UpgradeSelected -= HandleUpgradeSelected;
            }
        }

        private void HandleAttackStarted()
        {
            if (!isRunning || Time.unscaledTime < nextAdvanceTime)
            {
                return;
            }

            if (currentStep == TutorialStep.BasicAttack)
            {
                CompleteCurrentStep();
                return;
            }

            if (currentStep == TutorialStep.Combo && meleeAttack != null)
            {
                int currentComboStep = meleeAttack.CurrentComboIndex + 1;
                if (currentComboStep >= comboStepToComplete)
                {
                    CompleteCurrentStep();
                }
            }
        }

        private void HandleDashStarted()
        {
            if (isRunning && currentStep == TutorialStep.Dash && Time.unscaledTime >= nextAdvanceTime)
            {
                CompleteCurrentStep();
            }
        }

        private void HandleSkillStarted(SkillDefinition skill)
        {
            if (isRunning && currentStep == TutorialStep.SkillDischarge && Time.unscaledTime >= nextAdvanceTime)
            {
                CompleteCurrentStep();
            }
        }

        private void HandleRecoilChanged(float current, float max)
        {
            if (!isRunning || currentStep != TutorialStep.BuildRecoil || Time.unscaledTime < nextAdvanceTime)
            {
                return;
            }

            float ratio = max <= 0f ? 0f : current / max;
            if (ratio >= recoilRatioToTeachSkill)
            {
                CompleteCurrentStep();
            }
        }

        private void HandleRoomStarted(RoomDefinition room)
        {
            if (!isRunning
                || !useFirstRoomAsTutorialStage
                || !holdFirstRoomCombatUntilSkillLesson
                || combatStartLocked
                || roomManager == null
                || runManager == null
                || (startRoomIndex >= 0 && runManager.CurrentRoomIndex != startRoomIndex))
            {
                return;
            }

            if (startRoomIndex < 0)
            {
                startRoomIndex = runManager.CurrentRoomIndex;
            }

            roomManager.PushCombatStartLock(this);
            combatStartLocked = true;
        }

        private void HandleRunRoomCombatCleared(int roomIndex, RoomDefinition room)
        {
            if (isRunning && currentStep == TutorialStep.ClearRoom && Time.unscaledTime >= nextAdvanceTime)
            {
                CompleteCurrentStep();
            }
        }

        private void HandleUpgradeSelected(UpgradeDefinition upgrade)
        {
            if (isRunning && currentStep == TutorialStep.SelectUpgrade && Time.unscaledTime >= nextAdvanceTime)
            {
                CompleteCurrentStep();
            }
        }

        private void HandleRunRoomStarted(int roomIndex, RoomDefinition room)
        {
            if (!isRunning || currentStep != TutorialStep.ExitRoom || Time.unscaledTime < nextAdvanceTime)
            {
                return;
            }

            if (startRoomIndex < 0 || roomIndex > startRoomIndex)
            {
                CompleteCurrentStep();
            }
        }

        private void CompleteCurrentStep()
        {
            if (Time.unscaledTime < currentStepShownAt + minimumStepVisibleTime)
            {
                return;
            }

            nextAdvanceTime = Time.unscaledTime + stepAdvanceDelay;
            currentStep = GetNextStep(currentStep);
            SkipUnavailableSteps();

            if (currentStep == TutorialStep.BuildRecoil)
            {
                TrySpawnPracticeEnemies();
            }

            if (currentStep == TutorialStep.ClearRoom)
            {
                ClearPracticeEnemies();
                ReleaseCombatStartLock();
                if (hideHintDuringCombat && hintUI != null)
                {
                    ShowCurrentStep();
                    StartCombatHintHideTimer();
                    return;
                }
            }

            if (currentStep == TutorialStep.Complete)
            {
                FinishTutorial(showFinalMessage: true);
                return;
            }

            ShowCurrentStep();
        }

        private TutorialStep GetNextStep(TutorialStep step)
        {
            return step switch
            {
                TutorialStep.Move => TutorialStep.BasicAttack,
                TutorialStep.BasicAttack => TutorialStep.Combo,
                TutorialStep.Combo => TutorialStep.Dash,
                TutorialStep.Dash => TutorialStep.BuildRecoil,
                TutorialStep.BuildRecoil => TutorialStep.SkillDischarge,
                TutorialStep.SkillDischarge => TutorialStep.ClearRoom,
                TutorialStep.ClearRoom => TutorialStep.SelectUpgrade,
                TutorialStep.SelectUpgrade => TutorialStep.ExitRoom,
                TutorialStep.ExitRoom => TutorialStep.Complete,
                _ => TutorialStep.Complete
            };
        }

        private void SkipUnavailableSteps()
        {
            bool changed;
            do
            {
                changed = false;

                if (currentStep == TutorialStep.SkillDischarge
                    && skipSkillStepWhenNoSkill
                    && (skillController == null || !skillController.HasSkill))
                {
                    currentStep = GetNextStep(currentStep);
                    changed = true;
                }

                if (currentStep == TutorialStep.BuildRecoil
                    && spawnPracticeEnemyForRecoil
                    && ResolvePracticeEnemyPoolKey() == null)
                {
                    currentStep = GetNextStep(currentStep);
                    changed = true;
                }

                if (currentStep == TutorialStep.SelectUpgrade
                    && skipUpgradeStepWhenNoUpgradeManager
                    && upgradeManager == null)
                {
                    currentStep = GetNextStep(currentStep);
                    changed = true;
                }
            } while (changed);
        }

        private bool ShowCurrentStepWithTransition()
        {
            if (hintUI == null)
            {
                return true;
            }

            string title = "튜토리얼";
            string body = currentStep switch
            {
                TutorialStep.Move => "[WASD]로 이동하세요",
                TutorialStep.BasicAttack => "[좌클릭]으로 기본 공격을 사용하세요",
                TutorialStep.Combo => "연속 공격으로 3타 콤보까지 이어보세요",
                TutorialStep.Dash => "[Shift]로 대쉬하세요. 공격 중에도 회피할 수 있습니다",
                TutorialStep.BuildRecoil => "기본 공격을 맞히면 반동이 쌓입니다. 반동을 30%까지 쌓아보세요",
                TutorialStep.SkillDischarge => "[스킬]로 반동을 방출하세요. 반동이 높을수록 스킬이 강해집니다",
                TutorialStep.ClearRoom => "남은 적을 처치해 방을 클리어하세요",
                TutorialStep.SelectUpgrade => "증강을 선택해 전투 방식을 강화하세요",
                TutorialStep.ExitRoom => "열린 문으로 다음 구역에 이동하세요",
                _ => string.Empty
            };

            currentStepShownAt = Time.unscaledTime;
            hintUI.TransitionTo(title, body);
            return true;
        }

        private bool ShowReadableCurrentStep()
        {
            if (hintUI == null)
            {
                return false;
            }

            string body = currentStep switch
            {
                TutorialStep.Move => "[WASD]로 이동해보세요. ",
                TutorialStep.BasicAttack => "[좌클릭]으로 기본 공격을 사용하세요. ",
                TutorialStep.Combo => "공격을 이어서 3타 콤보까지 사용해보세요. 콤보는 총 5타까지 있으며 콤보마다 범위와 타격 방식이 달라집니다.",
                TutorialStep.Dash => "[Space]로 대쉬하세요. 공격 직후에도 일정 타이밍부터 대쉬로 빠져나갈 수 있습니다.",
                TutorialStep.BuildRecoil => "반동 게이지를 모아보세요. 기본 공격을 맞히면 반동이 쌓이고, 반동은 기본 공격력과 스킬 피해/넉백을 올려줍니다.",
                TutorialStep.SkillDischarge => "[우클릭]으로 스킬을 사용해 반동을 방출하세요. 반동이 높을수록 스킬이 강해지지만, 너무 오래 쌓으면 부담도 커집니다.",
                TutorialStep.ClearRoom => "이제 실제 전투가 시작됩니다. 남은 적을 처치하고 방을 클리어하세요.",
                TutorialStep.SelectUpgrade => "증강을 선택하세요. 지금 빌드에 필요한 공격/생존/특수 효과를 보고 고르면 됩니다.",
                TutorialStep.ExitRoom => "열린 문으로 이동하세요. 네비게이터가 다음 목적지 방향을 알려줍니다.",
                _ => string.Empty
            };

            currentStepShownAt = Time.unscaledTime;
            hintUI.TransitionTo("튜토리얼", body);
            return true;
        }

        private void ShowCurrentStep()
        {
            if (ShowReadableCurrentStep())
            {
                return;
            }

            if (ShowCurrentStepWithTransition())
            {
                return;
            }

            if (hintUI == null)
            {
                return;
            }

            string title = "튜토리얼";
            string body = currentStep switch
            {
                TutorialStep.Move => "[WASD]로 이동하세요",
                TutorialStep.BasicAttack => "[좌클릭]으로 기본 공격을 사용하세요",
                TutorialStep.Combo => "연속 공격으로 3타 콤보까지 이어보세요",
                TutorialStep.Dash => "[Shift]로 대쉬하세요. 공격 중에도 회피할 수 있습니다",
                TutorialStep.BuildRecoil => "기본 공격을 맞히면 반동이 쌓입니다. 반동을 30%까지 쌓아보세요",
                TutorialStep.SkillDischarge => "[스킬]로 반동을 방출하세요. 반동이 높을수록 스킬이 강해집니다",
                TutorialStep.ClearRoom => "남은 적을 처치해 방을 클리어하세요",
                TutorialStep.SelectUpgrade => "증강을 선택해 전투 방식을 강화하세요",
                TutorialStep.ExitRoom => "열린 문으로 다음 구역에 이동하세요",
                _ => string.Empty
            };

            hintUI.Show(title, body);
        }

        private void TrySpawnPracticeEnemies()
        {
            if (!spawnPracticeEnemyForRecoil || practiceEnemySpawnRequested || enemySpawner == null)
            {
                return;
            }

            PoolKey poolKey = ResolvePracticeEnemyPoolKey();
            if (poolKey == null)
            {
                return;
            }

            practiceEnemySpawnRequested = true;
            StartCoroutine(SpawnPracticeEnemiesRoutine(poolKey));
        }

        private IEnumerator SpawnPracticeEnemiesRoutine(PoolKey poolKey)
        {
            int count = Mathf.Max(1, practiceEnemyCount);
            for (int i = 0; i < count; i++)
            {
                IRoomEnemy spawnedEnemy = null;
                yield return enemySpawner.SpawnEnemyRoutine(poolKey, enemy => spawnedEnemy = enemy, GetPracticeEnemySpawnPosition());
                RegisterPracticeEnemy(spawnedEnemy);
            }
        }

        private void RegisterPracticeEnemy(IRoomEnemy enemy)
        {
            if (enemy == null)
            {
                return;
            }

            alivePracticeEnemyCount++;
            enemy.Died += HandlePracticeEnemyDied;

            EnemyController enemyController = enemy as EnemyController;
            if (enemyController == null && enemy is MonoBehaviour behaviour)
            {
                enemyController = behaviour.GetComponent<EnemyController>();
            }

            if (enemyController == null)
            {
                return;
            }

            enemyController.InitializeHealth(practiceEnemyMaxHealth, practiceEnemyInvulnerability);
            enemyController.SetBehaviorSuppressed(suppressPracticeEnemyBehavior);
            practiceEnemies.Add(enemyController);
        }

        private void HandlePracticeEnemyDied(IRoomEnemy enemy)
        {
            if (enemy != null)
            {
                enemy.Died -= HandlePracticeEnemyDied;
            }

            alivePracticeEnemyCount = Mathf.Max(0, alivePracticeEnemyCount - 1);
        }

        private Vector3 GetPracticeEnemySpawnPosition()
        {
            if (player == null)
            {
                return transform.position;
            }

            Vector2 direction = Vector2.right;
            if (player.Input != null && player.Input.AimDirection.sqrMagnitude > 0.001f)
            {
                direction = player.Input.AimDirection.normalized;
            }
            else if (player.Input != null && player.Input.MoveInput.sqrMagnitude > 0.001f)
            {
                direction = player.Input.MoveInput.normalized;
            }

            return player.transform.position + (Vector3)(direction * Mathf.Max(0.5f, practiceEnemySpawnDistance));
        }

        private void ClearPracticeEnemies()
        {
            for (int i = practiceEnemies.Count - 1; i >= 0; i--)
            {
                EnemyController enemy = practiceEnemies[i];
                if (enemy == null || enemy.IsDead)
                {
                    continue;
                }

                enemy.SetBehaviorSuppressed(false);
                enemy.DespawnToPool();
            }

            practiceEnemies.Clear();
            alivePracticeEnemyCount = 0;
        }

        private PoolKey ResolvePracticeEnemyPoolKey()
        {
            if (practiceEnemyPoolKey != null)
            {
                return practiceEnemyPoolKey;
            }

            if (runManager == null)
            {
                return null;
            }

            RoomDefinition room = runManager.GetRoomDefinition(Mathf.Max(0, runManager.CurrentRoomIndex));
            RoomDefinition.EncounterWave[] waves = room != null ? room.Waves : null;
            if (waves == null)
            {
                return null;
            }

            for (int i = 0; i < waves.Length; i++)
            {
                RoomDefinition.SpawnGroup[] groups = waves[i] != null ? waves[i].SpawnGroups : null;
                if (groups == null)
                {
                    continue;
                }

                for (int j = 0; j < groups.Length; j++)
                {
                    if (groups[j] != null && groups[j].PoolKey != null)
                    {
                        return groups[j].PoolKey;
                    }
                }
            }

            return null;
        }

        private void ReleaseCombatStartLock()
        {
            if (!combatStartLocked || roomManager == null)
            {
                return;
            }

            roomManager.ReleaseCombatStartLock(this);
            combatStartLocked = false;
        }

        private void StartCombatHintHideTimer()
        {
            if (combatHintHideRoutine != null)
            {
                StopCoroutine(combatHintHideRoutine);
            }

            combatHintHideRoutine = StartCoroutine(HideCombatHintAfterDelay());
        }

        private IEnumerator HideCombatHintAfterDelay()
        {
            float delay = Mathf.Max(0f, combatHintVisibleTime);
            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }

            if (isRunning && currentStep == TutorialStep.ClearRoom && hideHintDuringCombat && hintUI != null)
            {
                hintUI.Hide();
            }

            combatHintHideRoutine = null;
        }

        private void FinishTutorial(bool showFinalMessage)
        {
            isRunning = false;
            currentStep = TutorialStep.Complete;
            ClearPracticeEnemies();
            ReleaseCombatStartLock();

            if (combatHintHideRoutine != null)
            {
                StopCoroutine(combatHintHideRoutine);
                combatHintHideRoutine = null;
            }

            if (saveCompletion)
            {
                PlayerPrefs.SetInt(completionPrefsKey, 1);
                PlayerPrefs.Save();
            }

            if (hintUI == null)
            {
                return;
            }

            if (showFinalMessage)
            {
                hintUI.Show("튜토리얼 완료", "반동을 관리하며 방을 돌파하세요.");
                Invoke(nameof(HideHint), 1.6f);
                return;
                hintUI.Show("튜토리얼 완료", "반동을 관리하며 방을 돌파하세요");
                Invoke(nameof(HideHint), 1.6f);
            }
            else
            {
                hintUI.Hide();
            }
        }

        private void HideHint()
        {
            if (hintUI != null)
            {
                hintUI.Hide();
            }
        }
    }
}
