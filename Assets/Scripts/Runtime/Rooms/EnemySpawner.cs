using NeonBreaker.Enemies;
using NeonBreaker.Pooling;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace NeonBreaker.Rooms
{
    public sealed class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private ObjectPoolManager poolManager;
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private float fallbackSpawnRadius = 6f;
        [SerializeField] private int roomSpawnPadding = 2;
        [SerializeField] private int roomSpawnAttempts = 32;
        [SerializeField] private float spawnCheckRadius = 0.45f;
        [SerializeField] private LayerMask spawnBlockerLayers = ~0;
        [SerializeField] private Transform player;
        [SerializeField] private float minDistanceFromPlayer = 3f;
        [SerializeField] private bool useSpawnTelegraph = true;
        [SerializeField, Min(0f)] private float spawnTelegraphDuration = 0.65f;
        [SerializeField] private EnemySpawnTelegraph2D spawnTelegraphPrefab;
        [SerializeField] private Vector3 telegraphOffset = Vector3.zero;
        [SerializeField] private bool logSpawnPositions;

        private Tilemap activeRoomTilemap;
        private RectInt activeRoomBounds;
        private bool hasActiveRoomBounds;
        private readonly List<Vector3Int> activeRoomSpawnCells = new List<Vector3Int>();
        private readonly Collider2D[] spawnOverlapResults = new Collider2D[8];

        private void Awake()
        {
            if (poolManager == null)
            {
                poolManager = ObjectPoolManager.Instance;
            }

            if (player == null)
            {
                GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
                if (playerObject != null)
                {
                    player = playerObject.transform;
                }
            }
        }

        public void SetActiveRoom(RectInt roomBounds, Tilemap roomTilemap)
        {
            activeRoomBounds = roomBounds;
            activeRoomTilemap = roomTilemap;
            hasActiveRoomBounds = roomTilemap != null;
            RebuildActiveRoomSpawnCells();

            if (roomTilemap == null)
            {
                Debug.LogWarning("[EnemySpawner] Active room was set without a Tilemap. Falling back to spawn points or radius.", this);
            }
            else if (activeRoomSpawnCells.Count == 0)
            {
                Debug.LogWarning($"[EnemySpawner] Active room {roomBounds} has no valid floor cells for spawning.", this);
            }
        }

        public void ClearActiveRoom()
        {
            activeRoomTilemap = null;
            hasActiveRoomBounds = false;
            activeRoomSpawnCells.Clear();
        }

        public void SetPlayer(Transform playerTransform)
        {
            player = playerTransform;
        }

        public IRoomEnemy SpawnEnemy(PoolKey poolKey)
        {
            if (poolManager == null)
            {
                Debug.LogError("[EnemySpawner] Missing ObjectPoolManager.", this);
                return null;
            }

            Vector3 position = GetSpawnPosition();
            if (logSpawnPositions)
            {
                Debug.Log($"[EnemySpawner] Spawn '{poolKey.name}' at {position}.", this);
            }

            PoolableGameObject poolable = poolManager.Spawn(poolKey, position, Quaternion.identity);
            if (poolable == null)
            {
                return null;
            }

            IRoomEnemy enemy = FindRoomEnemy(poolable);
            if (enemy == null)
            {
                Debug.LogError($"[EnemySpawner] Spawned object '{poolable.name}' has no IRoomEnemy component.", poolable);
            }

            return enemy;
        }

        public IEnumerator SpawnEnemyRoutine(PoolKey poolKey, Action<IRoomEnemy> onSpawned)
        {
            yield return SpawnEnemyRoutine(poolKey, onSpawned, null);
        }

        public IEnumerator SpawnEnemyRoutine(PoolKey poolKey, Action<IRoomEnemy> onSpawned, Vector3? overridePosition)
        {
            if (poolManager == null)
            {
                Debug.LogError("[EnemySpawner] Missing ObjectPoolManager.", this);
                onSpawned?.Invoke(null);
                yield break;
            }

            Vector3 position = overridePosition ?? GetSpawnPosition();
            if (logSpawnPositions)
            {
                Debug.Log($"[EnemySpawner] Spawn '{poolKey.name}' at {position}.", this);
            }

            if (useSpawnTelegraph && spawnTelegraphDuration > 0f)
            {
                PlaySpawnTelegraph(position + telegraphOffset, spawnTelegraphDuration);
                yield return new WaitForSeconds(spawnTelegraphDuration);
            }

            onSpawned?.Invoke(SpawnEnemyAt(poolKey, position));
        }

        public bool TryGetActiveRoomCenterPosition(out Vector3 position)
        {
            position = transform.position;

            if (!hasActiveRoomBounds || activeRoomTilemap == null)
            {
                return false;
            }

            Vector3Int centerCell = Vector3Int.RoundToInt((Vector3)activeRoomBounds.center);
            if (!activeRoomTilemap.HasTile(centerCell))
            {
                centerCell = FindNearestFloorCell(centerCell);
            }

            position = activeRoomTilemap.GetCellCenterWorld(centerCell);
            return true;
        }

        private static IRoomEnemy FindRoomEnemy(PoolableGameObject poolable)
        {
            MonoBehaviour[] behaviours = poolable.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IRoomEnemy roomEnemy)
                {
                    return roomEnemy;
                }
            }

            return null;
        }

        private IRoomEnemy SpawnEnemyAt(PoolKey poolKey, Vector3 position)
        {
            PoolableGameObject poolable = poolManager.Spawn(poolKey, position, Quaternion.identity);
            if (poolable == null)
            {
                return null;
            }

            IRoomEnemy enemy = FindRoomEnemy(poolable);
            if (enemy == null)
            {
                Debug.LogError($"[EnemySpawner] Spawned object '{poolable.name}' has no IRoomEnemy component.", poolable);
            }

            return enemy;
        }

        private void PlaySpawnTelegraph(Vector3 position, float duration)
        {
            EnemySpawnTelegraph2D telegraph = spawnTelegraphPrefab != null
                ? Instantiate(spawnTelegraphPrefab, position, Quaternion.identity)
                : BuildFallbackTelegraph(position);

            if (telegraph != null)
            {
                telegraph.Play(duration);
            }
        }

        private static EnemySpawnTelegraph2D BuildFallbackTelegraph(Vector3 position)
        {
            GameObject telegraphObject = new GameObject("Enemy Spawn Telegraph");
            telegraphObject.transform.position = position;
            return telegraphObject.AddComponent<EnemySpawnTelegraph2D>();
        }

        private Vector3 GetSpawnPosition()
        {
            if (hasActiveRoomBounds && activeRoomTilemap != null)
            {
                return GetRandomRoomPosition();
            }

            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                Transform point = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
                if (point != null)
                {
                    return point.position;
                }
            }

            Vector2 offset = UnityEngine.Random.insideUnitCircle.normalized * fallbackSpawnRadius;
            return transform.position + (Vector3)offset;
        }

        private Vector3 GetRandomRoomPosition()
        {
            int attempts = Mathf.Max(1, roomSpawnAttempts);
            if (activeRoomSpawnCells.Count == 0)
            {
                Vector3Int centerCell = Vector3Int.RoundToInt((Vector3)activeRoomBounds.center);
                return activeRoomTilemap.GetCellCenterWorld(centerCell);
            }

            for (int i = 0; i < attempts; i++)
            {
                Vector3Int cell = activeRoomSpawnCells[UnityEngine.Random.Range(0, activeRoomSpawnCells.Count)];
                Vector3 position = activeRoomTilemap.GetCellCenterWorld(cell);
                if (!IsSpawnPositionValid(position))
                {
                    continue;
                }

                return position;
            }

            if (TryGetBestFallbackPosition(out Vector3 fallback))
            {
                return fallback;
            }

            if (logSpawnPositions)
            {
                Debug.LogWarning($"[EnemySpawner] Could not find a fully valid spawn position in room {activeRoomBounds}. Falling back to first floor cell.", this);
            }

            return activeRoomTilemap.GetCellCenterWorld(activeRoomSpawnCells[0]);
        }

        private void RebuildActiveRoomSpawnCells()
        {
            activeRoomSpawnCells.Clear();

            if (activeRoomTilemap == null)
            {
                return;
            }

            int padding = Mathf.Max(0, roomSpawnPadding);
            int minX = activeRoomBounds.xMin + padding;
            int maxX = activeRoomBounds.xMax - padding;
            int minY = activeRoomBounds.yMin + padding;
            int maxY = activeRoomBounds.yMax - padding;

            if (minX >= maxX || minY >= maxY)
            {
                Vector3Int centerCell = Vector3Int.RoundToInt((Vector3)activeRoomBounds.center);
                if (activeRoomTilemap.HasTile(centerCell))
                {
                    activeRoomSpawnCells.Add(centerCell);
                }

                return;
            }

            for (int x = minX; x < maxX; x++)
            {
                for (int y = minY; y < maxY; y++)
                {
                    Vector3Int cell = new Vector3Int(x, y, 0);
                    if (activeRoomTilemap.HasTile(cell))
                    {
                        activeRoomSpawnCells.Add(cell);
                    }
                }
            }
        }

        private Vector3Int FindNearestFloorCell(Vector3Int centerCell)
        {
            if (activeRoomSpawnCells.Count == 0)
            {
                return centerCell;
            }

            Vector3Int bestCell = activeRoomSpawnCells[0];
            int bestDistance = int.MaxValue;
            for (int i = 0; i < activeRoomSpawnCells.Count; i++)
            {
                Vector3Int cell = activeRoomSpawnCells[i];
                int distance = Mathf.Abs(cell.x - centerCell.x) + Mathf.Abs(cell.y - centerCell.y);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestCell = cell;
                }
            }

            return bestCell;
        }

        private bool TryGetBestFallbackPosition(out Vector3 position)
        {
            position = Vector3.zero;

            if (activeRoomSpawnCells.Count == 0)
            {
                return false;
            }

            float bestScore = float.MinValue;
            bool found = false;
            for (int i = 0; i < activeRoomSpawnCells.Count; i++)
            {
                Vector3 candidate = activeRoomTilemap.GetCellCenterWorld(activeRoomSpawnCells[i]);
                if (HasBlockingCollider(candidate))
                {
                    continue;
                }

                float score = player != null
                    ? ((Vector2)candidate - (Vector2)player.position).sqrMagnitude
                    : UnityEngine.Random.value;

                if (!found || score > bestScore)
                {
                    found = true;
                    bestScore = score;
                    position = candidate;
                }
            }

            return found;
        }

        private bool IsSpawnPositionValid(Vector3 position)
        {
            float minimumDistance = Mathf.Max(0f, minDistanceFromPlayer);
            if (player != null && minimumDistance > 0f)
            {
                float sqrDistance = ((Vector2)position - (Vector2)player.position).sqrMagnitude;
                if (sqrDistance < minimumDistance * minimumDistance)
                {
                    return false;
                }
            }

            return !HasBlockingCollider(position);
        }

        private bool HasBlockingCollider(Vector3 position)
        {
            float radius = Mathf.Max(0f, spawnCheckRadius);
            if (radius <= 0f)
            {
                return false;
            }

            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(spawnBlockerLayers);
            filter.useTriggers = false;

            int hitCount = Physics2D.OverlapCircle(position, radius, filter, spawnOverlapResults);
            return hitCount > 0;
        }
    }
}
