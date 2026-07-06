using System;
using System.Collections.Generic;
using NeonBreaker.Combat;
using NeonBreaker.Dungeon;
using NeonBreaker.Pooling;
using UnityEngine;

namespace NeonBreaker.Rooms
{
    public sealed class RoomRunManager : MonoBehaviour
    {
        [SerializeField] private RoomManager roomManager;
        [SerializeField] private EnemySpawner enemySpawner;
        [SerializeField] private TilemapDungeonGenerator dungeonGenerator;
        [SerializeField] private RoomExit roomExit;

        [Header("Room Sequence")]
        [SerializeField] private RoomDefinition[] roomSequence;
        [SerializeField] private RoomSequenceDefinition roomSequenceGenerator;
        [SerializeField] private bool generateRoomSequenceOnRunStart = true;
        [SerializeField] private bool logGeneratedRoomSequence = true;

        [Header("Run")]
        [SerializeField] private bool autoStart = true;
        [SerializeField] private bool generateDungeonOnRunStart = true;
        [SerializeField] private bool useGeneratedRoomEntryPoints = true;
        [SerializeField] private bool useGeneratedRoomSpawnAreas = true;
        [SerializeField] private bool waitForRewardBeforeExit = false;
        [SerializeField] private bool useDifficultyScaling = true;
        [SerializeField] private DifficultyScalingDefinition difficultyScaling;
        [SerializeField] private bool logRoomFlow = true;
        [SerializeField] private Transform player;
        [SerializeField] private Health playerHealth;
        [SerializeField] private Transform[] playerEntryPoints;

        private int currentRoomIndex = -1;
        private bool waitingForExit;
        private bool waitingForReward;
        private RoomDefinition pendingClearedRoom;
        private readonly List<Vector3> roomSpawnPositions = new List<Vector3>();

        public event Action<int, RoomDefinition> RunRoomStarted;
        public event Action<int, RoomDefinition> RunRoomCombatCleared;
        public event Action<int, RoomDefinition> RunRoomCleared;
        public event Action RunCleared;

        public int CurrentRoomIndex => currentRoomIndex;
        public int TotalRoomCount => roomSequence != null ? roomSequence.Length : 0;
        public bool HasNextRoom => roomSequence != null && currentRoomIndex + 1 < roomSequence.Length;
        public bool IsWaitingForExit => waitingForExit;
        public bool IsWaitingForReward => waitingForReward;

        public RoomDefinition GetRoomDefinition(int roomIndex)
        {
            if (roomSequence == null || roomIndex < 0 || roomIndex >= roomSequence.Length)
            {
                return null;
            }

            return roomSequence[roomIndex];
        }

        private void Awake()
        {
            if (roomManager == null)
            {
                roomManager = GetComponent<RoomManager>();
            }

            if (roomManager == null)
            {
                roomManager = GetComponentInChildren<RoomManager>();
            }

            if (enemySpawner == null)
            {
                enemySpawner = GetComponent<EnemySpawner>();
            }

            if (enemySpawner == null)
            {
                enemySpawner = GetComponentInChildren<EnemySpawner>();
            }

            if (roomManager != null && enemySpawner != null)
            {
                roomManager.SetSpawner(enemySpawner);
            }

            if (enemySpawner != null && player != null)
            {
                enemySpawner.SetPlayer(player);
            }

            if (playerHealth == null)
            {
                playerHealth = ResolvePlayerHealth();
            }

            if (dungeonGenerator == null)
            {
                dungeonGenerator = GetComponentInChildren<TilemapDungeonGenerator>();
            }

            if (dungeonGenerator == null)
            {
                dungeonGenerator = FindAnyObjectByType<TilemapDungeonGenerator>();
            }

            if (roomExit != null)
            {
                roomExit.SetRunManager(this);
                roomExit.SetUnlocked(false);
            }
        }

        private void OnEnable()
        {
            if (roomManager != null)
            {
                roomManager.RoomStarted += HandleRoomStarted;
                roomManager.RoomCleared += HandleRoomCleared;
            }
        }

        private void OnDisable()
        {
            if (roomManager != null)
            {
                roomManager.RoomStarted -= HandleRoomStarted;
                roomManager.RoomCleared -= HandleRoomCleared;
            }
        }

        private void Start()
        {
            if (logRoomFlow)
            {
                Debug.Log($"[RoomRunManager] Start. autoStart={autoStart}, rooms={(roomSequence != null ? roomSequence.Length : 0)}", this);
            }

            if (autoStart)
            {
                StartRun();
            }
        }

        public void StartRun()
        {
            PrepareRoomSequence();

            if (!CanStartRun())
            {
                return;
            }

            ValidateRoomSetup();
            PrepareDungeon();
            EnterRoom(0, true);
        }

        private void PrepareRoomSequence()
        {
            if (!generateRoomSequenceOnRunStart || roomSequenceGenerator == null)
            {
                return;
            }

            try
            {
                RoomDefinition[] generatedSequence = roomSequenceGenerator.Build();
                if (generatedSequence == null || generatedSequence.Length == 0)
                {
                    Debug.LogError("[RoomRunManager] Room sequence generator returned an empty sequence.", this);
                    return;
                }

                roomSequence = generatedSequence;
                LogGeneratedSequence();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[RoomRunManager] Failed to generate room sequence: {exception.Message}", this);
            }
        }

        [ContextMenu("Validate Room Setup")]
        public void ValidateRoomSetup()
        {
            int errorCount = 0;
            int warningCount = 0;
            ObjectPoolManager poolManager = ObjectPoolManager.Instance != null
                ? ObjectPoolManager.Instance
                : FindAnyObjectByType<ObjectPoolManager>();

            if (roomManager == null)
            {
                errorCount++;
                Debug.LogError("[RoomRunManager] RoomManager reference is missing.", this);
            }

            if (enemySpawner == null)
            {
                errorCount++;
                Debug.LogError("[RoomRunManager] EnemySpawner reference is missing. Enemy waves cannot spawn.", this);
            }

            if (dungeonGenerator == null)
            {
                errorCount++;
                Debug.LogError("[RoomRunManager] TilemapDungeonGenerator reference is missing. Dungeon doors/triggers will not be generated.", this);
            }

            if (player == null)
            {
                warningCount++;
                Debug.LogWarning("[RoomRunManager] Player reference is missing. Generated room entry movement will not work.", this);
            }

            if (roomSequence == null || roomSequence.Length == 0)
            {
                errorCount++;
                Debug.LogError("[RoomRunManager] Room Sequence is empty. No rooms can start.", this);
                return;
            }

            if (poolManager == null)
            {
                errorCount++;
                Debug.LogError("[RoomRunManager] ObjectPoolManager was not found. Enemy spawn groups will fail.", this);
            }

            for (int i = 0; i < roomSequence.Length; i++)
            {
                RoomDefinition room = roomSequence[i];
                if (room == null)
                {
                    errorCount++;
                    Debug.LogError($"[RoomRunManager] Room Sequence element {i} is empty.", this);
                    continue;
                }

                ValidateRoomDefinition(i, room, poolManager, ref errorCount, ref warningCount);
            }

            Debug.Log($"[RoomRunManager] Validate complete. rooms={roomSequence.Length}, errors={errorCount}, warnings={warningCount}.", this);
        }

        public void TryEnterNextRoom()
        {
            if (waitingForReward || !waitingForExit || !HasNextRoom)
            {
                return;
            }

            EnterRoom(currentRoomIndex + 1, true);
        }

        public void TryEnterRoom(int roomIndex)
        {
            if (roomIndex == currentRoomIndex)
            {
                return;
            }

            if (currentRoomIndex < 0)
            {
                if (roomIndex != 0 || !CanStartRun())
                {
                    return;
                }

                EnterRoom(0, false);
                return;
            }

            if (waitingForReward || !waitingForExit || roomIndex != currentRoomIndex + 1)
            {
                return;
            }

            EnterRoom(roomIndex, false);
        }

        private void EnterRoom(int roomIndex, bool movePlayerToRoom)
        {
            if (roomIndex < 0 || roomSequence == null || roomIndex >= roomSequence.Length)
            {
                return;
            }

            currentRoomIndex = roomIndex;
            waitingForExit = false;
            waitingForReward = false;
            pendingClearedRoom = null;
            roomExit?.SetUnlocked(false);

            if (logRoomFlow)
            {
                Debug.Log($"[RoomRunManager] Enter room {roomIndex}: {roomSequence[roomIndex].DisplayName}", this);
            }

            PrepareRoom(currentRoomIndex);
            if (movePlayerToRoom)
            {
                MovePlayerToEntryPoint(currentRoomIndex);
            }

            roomManager.SetDifficultyContext(EvaluateDifficulty(currentRoomIndex, roomSequence[currentRoomIndex]));
            roomManager.StartRoom(roomSequence[currentRoomIndex]);
        }

        private bool CanStartRun()
        {
            if (roomManager == null)
            {
                Debug.LogError("[RoomRunManager] Cannot start run. RoomManager is missing.", this);
                return false;
            }

            if (roomSequence == null || roomSequence.Length == 0)
            {
                Debug.LogError("[RoomRunManager] Cannot start run. Room sequence is empty.", this);
                return false;
            }

            return true;
        }

        private void LogGeneratedSequence()
        {
            if (!logGeneratedRoomSequence || roomSequence == null)
            {
                return;
            }

            string message = "[RoomRunManager] Generated room sequence:";
            for (int i = 0; i < roomSequence.Length; i++)
            {
                RoomDefinition room = roomSequence[i];
                string roomName = room != null ? room.DisplayName : "Empty";
                string roomType = room != null ? room.RoomType.ToString() : "None";
                message += $" {i + 1}.{roomName}({roomType})";
            }

            Debug.Log(message, this);
        }

        private void PrepareDungeon()
        {
            if (dungeonGenerator == null)
            {
                return;
            }

            if (generateDungeonOnRunStart || !dungeonGenerator.HasGeneratedDungeon)
            {
                dungeonGenerator.Generate(roomSequence);
            }
        }

        private void PrepareRoom(int roomIndex)
        {
            if (!useGeneratedRoomSpawnAreas || enemySpawner == null || dungeonGenerator == null)
            {
                return;
            }

            if (dungeonGenerator.TryGetRoomBounds(roomIndex, out RectInt roomBounds))
            {
                bool hasTemplateSpawnPositions = dungeonGenerator.TryGetRoomSpawnPositions(roomIndex, roomSpawnPositions);
                enemySpawner.SetActiveRoomSpawnData(
                    roomBounds,
                    dungeonGenerator.FloorTilemap,
                    dungeonGenerator.GetRoomCenterWorld(roomIndex),
                    hasTemplateSpawnPositions ? roomSpawnPositions : null);
            }
        }

        private DifficultyContext EvaluateDifficulty(int roomIndex, RoomDefinition room)
        {
            if (!useDifficultyScaling)
            {
                return DifficultyContext.Default;
            }

            DifficultyContext context = difficultyScaling != null
                ? difficultyScaling.Evaluate(roomIndex, TotalRoomCount, room)
                : DifficultyScalingDefinition.EvaluateDefault(roomIndex, TotalRoomCount, room);

            if (logRoomFlow)
            {
                Debug.Log(
                    $"[RoomRunManager] Difficulty room={roomIndex}, hp=x{context.EnemyHealthMultiplier:0.00}, spawn=x{context.SpawnCountMultiplier:0.00}",
                    this);
            }

            return context;
        }

        private void HandleRoomStarted(RoomDefinition room)
        {
            if (logRoomFlow)
            {
                Debug.Log($"[RoomRunManager] Room started: index={currentRoomIndex}, name={room.DisplayName}", this);
            }

            RunRoomStarted?.Invoke(currentRoomIndex, room);
        }

        private void HandleRoomCleared(RoomDefinition room)
        {
            if (logRoomFlow)
            {
                Debug.Log($"[RoomRunManager] Room cleared: index={currentRoomIndex}, name={room.DisplayName}", this);
            }

            ApplyImmediateRoomReward(room);

            if (ShouldWaitForReward(room))
            {
                waitingForReward = true;
                pendingClearedRoom = room;
                RunRoomCombatCleared?.Invoke(currentRoomIndex, room);

                if (waitingForReward)
                {
                    return;
                }
            }
            else
            {
                RunRoomCombatCleared?.Invoke(currentRoomIndex, room);
            }

            CompleteRoomClear(room);
        }

        public void SetRewardGateEnabled(bool enabled)
        {
            waitForRewardBeforeExit = enabled;
        }

        public void CompletePendingReward()
        {
            if (!waitingForReward)
            {
                return;
            }

            RoomDefinition room = pendingClearedRoom;
            waitingForReward = false;
            pendingClearedRoom = null;
            CompleteRoomClear(room);
        }

        private bool ShouldWaitForReward(RoomDefinition room)
        {
            return waitForRewardBeforeExit
                && HasNextRoom
                && room != null
                && room.GrantsUpgradeReward
                && room.WaitForUpgradeRewardBeforeExit;
        }

        private void ApplyImmediateRoomReward(RoomDefinition room)
        {
            if (room == null || !room.GrantsHealReward)
            {
                return;
            }

            if (playerHealth == null)
            {
                playerHealth = ResolvePlayerHealth();
            }

            if (playerHealth == null)
            {
                Debug.LogWarning("[RoomRunManager] Heal reward could not be applied because Player Health was not found.", this);
                return;
            }

            float healAmount = room.HealAmount + playerHealth.MaxHealth * room.HealPercent;
            if (healAmount <= 0f)
            {
                return;
            }

            playerHealth.Heal(healAmount);
            if (logRoomFlow)
            {
                Debug.Log($"[RoomRunManager] Applied heal reward: +{healAmount:0.#} HP.", this);
            }
        }

        private void CompleteRoomClear(RoomDefinition room)
        {
            RunRoomCleared?.Invoke(currentRoomIndex, room);

            if (!HasNextRoom)
            {
                waitingForExit = false;
                roomExit?.SetUnlocked(false);
                RunCleared?.Invoke();
                return;
            }

            waitingForExit = true;
            roomExit?.SetUnlocked(true);
        }

        private void MovePlayerToEntryPoint(int roomIndex)
        {
            if (player == null || playerEntryPoints == null || roomIndex < 0 || roomIndex >= playerEntryPoints.Length)
            {
                MovePlayerToGeneratedRoom(roomIndex);
                return;
            }

            Transform entryPoint = playerEntryPoints[roomIndex];
            if (entryPoint != null)
            {
                player.position = entryPoint.position;
                return;
            }

            MovePlayerToGeneratedRoom(roomIndex);
        }

        private void MovePlayerToGeneratedRoom(int roomIndex)
        {
            if (!useGeneratedRoomEntryPoints || player == null || dungeonGenerator == null)
            {
                return;
            }

            player.position = dungeonGenerator.GetRoomCenterWorld(roomIndex);
        }

        private Health ResolvePlayerHealth()
        {
            if (player != null && player.TryGetComponent(out Health health))
            {
                return health;
            }

            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null && playerObject.TryGetComponent(out health))
            {
                return health;
            }

            return FindAnyObjectByType<Health>();
        }

        private void ValidateRoomDefinition(int roomIndex, RoomDefinition room, ObjectPoolManager poolManager, ref int errorCount, ref int warningCount)
        {
            RoomDefinition.EncounterWave[] waves = room.Waves;
            if (waves == null || waves.Length == 0)
            {
                if (ShouldWarnMissingWaves(room))
                {
                    warningCount++;
                    Debug.LogWarning($"[RoomRunManager] Room {roomIndex} '{room.DisplayName}' has no waves. It will clear immediately.", room);
                }

                return;
            }

            for (int waveIndex = 0; waveIndex < waves.Length; waveIndex++)
            {
                RoomDefinition.EncounterWave wave = waves[waveIndex];
                if (wave == null)
                {
                    errorCount++;
                    Debug.LogError($"[RoomRunManager] Room {roomIndex} '{room.DisplayName}' wave {waveIndex} is empty.", room);
                    continue;
                }

                RoomDefinition.SpawnGroup[] spawnGroups = wave.SpawnGroups;
                if (spawnGroups == null || spawnGroups.Length == 0)
                {
                    warningCount++;
                    Debug.LogWarning($"[RoomRunManager] Room {roomIndex} '{room.DisplayName}' wave {waveIndex} has no spawn groups.", room);
                    continue;
                }

                for (int groupIndex = 0; groupIndex < spawnGroups.Length; groupIndex++)
                {
                    RoomDefinition.SpawnGroup group = spawnGroups[groupIndex];
                    if (group == null)
                    {
                        errorCount++;
                        Debug.LogError($"[RoomRunManager] Room {roomIndex} wave {waveIndex} spawn group {groupIndex} is empty.", room);
                        continue;
                    }

                    if (group.Count <= 0)
                    {
                        warningCount++;
                        Debug.LogWarning($"[RoomRunManager] Room {roomIndex} wave {waveIndex} spawn group {groupIndex} count is 0.", room);
                    }

                    if (group.PoolKey == null)
                    {
                        errorCount++;
                        Debug.LogError($"[RoomRunManager] Room {roomIndex} wave {waveIndex} spawn group {groupIndex} PoolKey is missing.", room);
                        continue;
                    }

                    if (poolManager != null && !poolManager.HasPool(group.PoolKey))
                    {
                        errorCount++;
                        Debug.LogError($"[RoomRunManager] PoolKey '{group.PoolKey.name}' is used by Room {roomIndex}, but ObjectPoolManager has no matching pool.", room);
                    }
                }
            }
        }

        private static bool ShouldWarnMissingWaves(RoomDefinition room)
        {
            if (room == null)
            {
                return false;
            }

            return room.RoomType == RoomType.Combat
                || room.RoomType == RoomType.Elite
                || room.RoomType == RoomType.Boss;
        }
    }
}
