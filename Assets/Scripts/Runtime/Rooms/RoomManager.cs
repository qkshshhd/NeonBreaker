using System;
using System.Collections;
using NeonBreaker.Enemies;
using NeonBreaker.Player;
using UnityEngine;

namespace NeonBreaker.Rooms
{
    public sealed class RoomManager : MonoBehaviour
    {
        [SerializeField] private EnemySpawner spawner;
        [SerializeField] private RoomDefinition startRoom;
        [SerializeField] private bool autoStart;
        [SerializeField] private GameObject[] blockers;
        [SerializeField] private GameObject[] activateOnClear;
        [SerializeField] private bool playBossIntro = true;
        [SerializeField, Min(0f)] private float bossIntroDuration = 1.35f;
        [SerializeField] private string fallbackBossIntroTitle = "BOSS";
        [SerializeField] private string fallbackBossIntroSubtitle = "Final Encounter";
        [SerializeField] private bool playBossIntroAfterFirstSpawn = true;
        [SerializeField] private bool pauseTimeDuringBossIntro = true;
        [SerializeField] private bool spawnFirstBossAtRoomCenter = true;
        [SerializeField] private bool lockPlayerControlFromBossRoomStart = true;
        [SerializeField] private PlayerInputReader playerInput;

        private RoomDefinition currentRoom;
        private int aliveEnemyCount;
        private bool isRunning;
        private bool isCleared;
        private Coroutine roomRoutine;
        private DifficultyContext difficultyContext = DifficultyContext.Default;
        private bool bossIntroPlayedThisRoom;
        private PlayerController playerController;
        private bool bossRoomStartControlLocked;
        private bool previousCanMove;
        private int combatStartLockCount;

        public event Action<RoomDefinition> RoomStarted;
        public event Action<RoomDefinition, string, string, float> RoomIntroStarted;
        public event Action<RoomDefinition> RoomIntroFinished;
        public event Action<RoomDefinition> RoomCleared;
        public event Action<RoomDefinition, int, RoomDefinition.EncounterWave> RoomWaveStarted;
        public event Action<RoomDefinition, int, RoomDefinition.EncounterWave> RoomWaveCleared;
        public event Action<IRoomEnemy> EnemySpawned;
        public event Action<int> EnemyCountChanged;

        public RoomDefinition CurrentRoom => currentRoom;
        public int AliveEnemyCount => aliveEnemyCount;
        public bool IsRunning => isRunning;
        public bool IsCleared => isCleared;
        public bool IsCombatStartLocked => combatStartLockCount > 0;

        public void SetSpawner(EnemySpawner enemySpawner)
        {
            spawner = enemySpawner;
        }

        public void SetDifficultyContext(DifficultyContext context)
        {
            difficultyContext = context;
        }

        public void PushCombatStartLock(object owner)
        {
            combatStartLockCount++;
        }

        public void ReleaseCombatStartLock(object owner)
        {
            combatStartLockCount = Mathf.Max(0, combatStartLockCount - 1);
        }

        private void Awake()
        {
            if (spawner == null)
            {
                spawner = GetComponent<EnemySpawner>();
            }
        }

        private void Start()
        {
            if (autoStart && startRoom != null)
            {
                StartRoom(startRoom);
            }
        }

        public void StartRoom(RoomDefinition room)
        {
            if (room == null)
            {
                Debug.LogError("[RoomManager] Cannot start a null room.", this);
                return;
            }

            if (room.HasEnemies && spawner == null)
            {
                Debug.LogError("[RoomManager] Cannot start enemy waves. EnemySpawner is missing.", this);
                return;
            }

            StopRoom();

            currentRoom = room;
            aliveEnemyCount = 0;
            isRunning = true;
            isCleared = false;
            bossIntroPlayedThisRoom = false;
            EnemyCountChanged?.Invoke(aliveEnemyCount);

            SetObjectsActive(blockers, room.LockDoorsOnEnter);
            SetObjectsActive(activateOnClear, false);

            LockPlayerControlForBossIntro(room);

            RoomStarted?.Invoke(room);
            roomRoutine = StartCoroutine(RoomRoutine(room));
        }

        public void StopRoom()
        {
            if (roomRoutine != null)
            {
                StopCoroutine(roomRoutine);
                roomRoutine = null;
            }

            isRunning = false;
            ReleasePlayerControlForBossIntro();
        }

        private IEnumerator RoomRoutine(RoomDefinition room)
        {
            if (!playBossIntroAfterFirstSpawn)
            {
                yield return RoomIntroRoutine(room);
            }

            while (combatStartLockCount > 0)
            {
                yield return null;
            }

            if (room.Waves != null)
            {
                for (int i = 0; i < room.Waves.Length; i++)
                {
                    yield return WaveRoutine(room, i, room.Waves[i]);
                }
            }

            if (room.Waves == null || room.Waves.Length == 0)
            {
                aliveEnemyCount = 0;
            }

            if (room.HasEnemies)
            {
                while (aliveEnemyCount > 0)
                {
                    yield return null;
                }
            }

            if (room.ClearDelay > 0f)
            {
                yield return new WaitForSeconds(room.ClearDelay);
            }

            ClearRoom(room);
        }

        private IEnumerator RoomIntroRoutine(RoomDefinition room)
        {
            if (!ShouldPlayIntro(room))
            {
                yield break;
            }

            bossIntroPlayedThisRoom = true;
            string title = room != null && !string.IsNullOrWhiteSpace(room.IntroTitle)
                ? room.IntroTitle
                : fallbackBossIntroTitle;
            string subtitle = room != null && !string.IsNullOrWhiteSpace(room.IntroSubtitle)
                ? room.IntroSubtitle
                : fallbackBossIntroSubtitle;

            float previousTimeScale = Time.timeScale;
            float previousFixedDeltaTime = Time.fixedDeltaTime;
            if (pauseTimeDuringBossIntro)
            {
                Time.timeScale = 0f;
            }

            RoomIntroStarted?.Invoke(room, title, subtitle, bossIntroDuration);

            if (bossIntroDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(bossIntroDuration);
            }

            RoomIntroFinished?.Invoke(room);

            if (pauseTimeDuringBossIntro)
            {
                Time.timeScale = previousTimeScale;
                Time.fixedDeltaTime = previousFixedDeltaTime;
            }

            ReleasePlayerControlForBossIntro();
        }

        private bool ShouldPlayIntro(RoomDefinition room)
        {
            return playBossIntro
                && bossIntroDuration > 0f
                && room != null
                && room.RoomType == RoomType.Boss
                && !bossIntroPlayedThisRoom;
        }

        private IEnumerator WaveRoutine(RoomDefinition room, int waveIndex, RoomDefinition.EncounterWave wave)
        {
            aliveEnemyCount = 0;
            EnemyCountChanged?.Invoke(aliveEnemyCount);
            RoomWaveStarted?.Invoke(room, waveIndex, wave);

            if (wave == null || !wave.HasEnemies)
            {
                RoomWaveCleared?.Invoke(room, waveIndex, wave);
                yield break;
            }

            if (wave.StartDelay > 0f)
            {
                yield return new WaitForSeconds(wave.StartDelay);
            }

            if (wave.SpawnGroups != null)
            {
                for (int i = 0; i < wave.SpawnGroups.Length; i++)
                {
                    yield return SpawnGroupRoutine(room, waveIndex, i, wave.SpawnGroups[i]);
                }
            }

            while (aliveEnemyCount > 0)
            {
                yield return null;
            }

            RoomWaveCleared?.Invoke(room, waveIndex, wave);
        }

        private IEnumerator SpawnGroupRoutine(RoomDefinition room, int waveIndex, int groupIndex, RoomDefinition.SpawnGroup group)
        {
            if (group == null || group.PoolKey == null || group.Count <= 0)
            {
                if (group != null && group.Count > 0 && group.PoolKey == null)
                {
                    Debug.LogError("[RoomManager] Spawn group has enemies but PoolKey is missing.", this);
                }

                yield break;
            }

            if (group.StartDelay > 0f)
            {
                yield return new WaitForSeconds(group.StartDelay);
            }

            int spawnCount = GetScaledSpawnCount(room, group.Count);
            for (int i = 0; i < spawnCount; i++)
            {
                IRoomEnemy enemy = null;
                if (spawner != null)
                {
                    Vector3? overridePosition = GetSpawnPositionOverride(room, waveIndex, groupIndex, i);
                    yield return spawner.SpawnEnemyRoutine(group.PoolKey, spawnedEnemy => enemy = spawnedEnemy, overridePosition);
                }

                if (enemy != null)
                {
                    ApplyDifficulty(enemy);
                    aliveEnemyCount++;
                    enemy.Died += HandleEnemyDied;
                    EnemyCountChanged?.Invoke(aliveEnemyCount);
                    EnemySpawned?.Invoke(enemy);

                    if (ShouldPlayIntroAfterSpawn(room, waveIndex, groupIndex, i))
                    {
                        yield return RoomIntroRoutine(room);
                    }
                }
                else
                {
                    Debug.LogError($"[RoomManager] Failed to spawn enemy for PoolKey '{group.PoolKey.name}'.", this);
                }

                if (group.SpawnInterval > 0f && i < spawnCount - 1)
                {
                    yield return new WaitForSeconds(group.SpawnInterval);
                }
            }
        }

        private Vector3? GetSpawnPositionOverride(RoomDefinition room, int waveIndex, int groupIndex, int spawnIndex)
        {
            if (!spawnFirstBossAtRoomCenter
                || room == null
                || room.RoomType != RoomType.Boss
                || waveIndex != 0
                || groupIndex != 0
                || spawnIndex != 0
                || spawner == null)
            {
                return null;
            }

            return spawner.TryGetActiveRoomCenterPosition(out Vector3 roomCenter)
                ? roomCenter
                : null;
        }

        private bool ShouldPlayIntroAfterSpawn(RoomDefinition room, int waveIndex, int groupIndex, int spawnIndex)
        {
            return playBossIntroAfterFirstSpawn
                && room != null
                && room.RoomType == RoomType.Boss
                && waveIndex == 0
                && groupIndex == 0
                && spawnIndex == 0
                && ShouldPlayIntro(room);
        }

        private int GetScaledSpawnCount(RoomDefinition room, int baseCount)
        {
            if (baseCount <= 0)
            {
                return 0;
            }

            if (room != null && room.RoomType == RoomType.Boss)
            {
                return baseCount;
            }

            float multiplier = Mathf.Max(1f, difficultyContext.SpawnCountMultiplier);
            int scaledCount = Mathf.RoundToInt(baseCount * multiplier);
            int maxCount = baseCount + Mathf.Max(0, difficultyContext.MaxExtraEnemiesPerGroup);
            return Mathf.Clamp(scaledCount, baseCount, maxCount);
        }

        private void ApplyDifficulty(IRoomEnemy roomEnemy)
        {
            Component enemyComponent = roomEnemy as Component;
            if (enemyComponent == null)
            {
                return;
            }

            EnemyController enemyController = enemyComponent.GetComponent<EnemyController>();
            if (enemyController == null)
            {
                enemyController = enemyComponent.GetComponentInChildren<EnemyController>(true);
            }

            if (enemyController != null)
            {
                enemyController.ApplyDifficulty(difficultyContext);
            }
        }

        private void HandleEnemyDied(IRoomEnemy enemy)
        {
            if (enemy != null)
            {
                enemy.Died -= HandleEnemyDied;
            }

            aliveEnemyCount = Mathf.Max(0, aliveEnemyCount - 1);
            EnemyCountChanged?.Invoke(aliveEnemyCount);
        }

        private void ClearRoom(RoomDefinition room)
        {
            if (isCleared)
            {
                return;
            }

            isCleared = true;
            isRunning = false;
            roomRoutine = null;
            ReleasePlayerControlForBossIntro();

            if (room.UnlockExitOnClear)
            {
                SetObjectsActive(blockers, false);
            }

            SetObjectsActive(activateOnClear, true);
            RoomCleared?.Invoke(room);
        }

        private static void SetObjectsActive(GameObject[] objects, bool active)
        {
            if (objects == null)
            {
                return;
            }

            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null)
                {
                    objects[i].SetActive(active);
                }
            }
        }

        private void LockPlayerControlForBossIntro(RoomDefinition room)
        {
            if (!lockPlayerControlFromBossRoomStart || !ShouldPlayIntro(room) || bossRoomStartControlLocked)
            {
                return;
            }

            ResolvePlayerReferences();

            if (playerInput != null)
            {
                playerInput.PushGameplayInputLock(this);
            }

            if (playerController != null)
            {
                previousCanMove = playerController.Movement.CanMove;
                playerController.Movement.CanMove = false;
                playerController.Dash.CancelDash();
                playerController.Movement.Stop();
            }

            bossRoomStartControlLocked = true;
        }

        private void ReleasePlayerControlForBossIntro()
        {
            if (!bossRoomStartControlLocked)
            {
                return;
            }

            if (playerInput != null)
            {
                playerInput.ReleaseGameplayInputLock(this);
            }

            if (playerController != null)
            {
                playerController.Movement.CanMove = previousCanMove;
                playerController.Movement.Stop();
                playerController.ChangeToLocomotion();
            }

            bossRoomStartControlLocked = false;
        }

        private void ResolvePlayerReferences()
        {
            if (playerController == null)
            {
                playerController = FindAnyObjectByType<PlayerController>();
            }

            if (playerInput == null)
            {
                playerInput = playerController != null
                    ? playerController.Input
                    : FindAnyObjectByType<PlayerInputReader>();
            }
        }
    }
}
