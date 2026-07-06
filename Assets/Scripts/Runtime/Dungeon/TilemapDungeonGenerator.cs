using System;
using System.Collections.Generic;
using NeonBreaker.Rooms;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace NeonBreaker.Dungeon
{
    public sealed class TilemapDungeonGenerator : MonoBehaviour
    {
        private enum GenerationMode
        {
            ProceduralTilemap,
            TemplateRooms
        }

        [Header("Tilemaps")]
        [SerializeField] private Tilemap floorTilemap;
        [SerializeField] private Tilemap wallTilemap;
        [SerializeField] private bool createTilemapsIfMissing = true;
        [SerializeField] private bool addWallCollider = true;
        [SerializeField] private bool addDoorColliders = true;
        [SerializeField] private bool createRoomTriggers = true;
        [SerializeField] private bool hideDoorVisualWhenUnlocked = true;
        [SerializeField] private DungeonTileSet tileSet;
        [SerializeField] private RoomRunManager runManager;

        [Header("Generation")]
        [SerializeField] private GenerationMode generationMode = GenerationMode.ProceduralTilemap;
        [SerializeField] private bool generateOnStart = true;
        [SerializeField] private bool useRandomSeed = true;
        [SerializeField] private int seed = 12345;
        [SerializeField] private int roomCount = 5;
        [SerializeField] private Vector2Int minRoomSize = new Vector2Int(12, 8);
        [SerializeField] private Vector2Int maxRoomSize = new Vector2Int(18, 12);
        [SerializeField] private int roomSpacing = 8;
        [SerializeField] private int roomSeparationPadding = 3;
        [SerializeField] private int maxRoomPlacementAttempts = 64;
        [SerializeField] private int corridorWidth = 3;
        [SerializeField] private int wallThickness = 1;
        [SerializeField] private int doorThickness = 1;
        [SerializeField] private int roomTriggerPadding = 1;

        [Header("Template Rooms")]
        [SerializeField] private RoomTemplateSet roomTemplateSet;
        [SerializeField] private bool fallbackToProceduralIfTemplateMissing = true;
        [SerializeField] private int templateRoomSpacing = 8;

        private readonly List<RectInt> generatedRooms = new List<RectInt>();
        private readonly HashSet<Vector3Int> floorCells = new HashSet<Vector3Int>();
        private readonly HashSet<Vector3Int> wallCells = new HashSet<Vector3Int>();
        private readonly List<List<Vector3Int>> doorCellGroups = new List<List<Vector3Int>>();
        private readonly List<int> roomEntranceDoorIndices = new List<int>();
        private readonly List<int> roomExitDoorIndices = new List<int>();
        private readonly List<Tilemap> doorTilemaps = new List<Tilemap>();
        private readonly List<TilemapCollider2D> doorColliders = new List<TilemapCollider2D>();
        private readonly List<BoxCollider2D> doorBlockers = new List<BoxCollider2D>();
        private readonly List<RoomTemplateDoor2D> templateDoors = new List<RoomTemplateDoor2D>();
        private readonly List<Vector3> doorWorldCenters = new List<Vector3>();
        private readonly List<RoomTemplate2D> roomTemplateInstances = new List<RoomTemplate2D>();
        private readonly List<List<Vector3>> roomTemplateSpawnPositions = new List<List<Vector3>>();
        private readonly List<Vector3> roomTemplateCenters = new List<Vector3>();
        private RoomDefinition[] activeRoomDefinitions;
        private Transform templateRoomRoot;
        private Transform doorRoot;
        private Transform roomTriggerRoot;
        private bool subscribedToRunManager;

        public IReadOnlyList<RectInt> GeneratedRooms => generatedRooms;
        public int RoomCount => generatedRooms.Count;
        public Tilemap FloorTilemap => floorTilemap;
        public bool HasGeneratedDungeon => generatedRooms.Count > 0;

        public event Action<IReadOnlyList<RectInt>> DungeonGenerated;

        private void Awake()
        {
            if (runManager == null)
            {
                runManager = GetComponentInParent<RoomRunManager>();
            }
        }

        private void OnEnable()
        {
            SubscribeToRunManager();
        }

        private void OnDisable()
        {
            UnsubscribeFromRunManager();
        }

        private void Start()
        {
            if (generateOnStart && runManager == null)
            {
                Generate();
            }
        }

        [ContextMenu("Generate Dungeon")]
        public void Generate()
        {
            GenerateInternal(activeRoomDefinitions);
        }

        public void Generate(IReadOnlyList<RoomDefinition> roomDefinitions)
        {
            if (roomDefinitions == null)
            {
                activeRoomDefinitions = null;
                GenerateInternal(null);
                return;
            }

            activeRoomDefinitions = new RoomDefinition[roomDefinitions.Count];
            for (int i = 0; i < roomDefinitions.Count; i++)
            {
                activeRoomDefinitions[i] = roomDefinitions[i];
            }

            roomCount = Mathf.Max(1, activeRoomDefinitions.Length);
            GenerateInternal(activeRoomDefinitions);
        }

        private void GenerateInternal(IReadOnlyList<RoomDefinition> roomDefinitions)
        {
            if (createTilemapsIfMissing)
            {
                EnsureTilemaps();
            }

            if (floorTilemap == null || wallTilemap == null || tileSet == null)
            {
                Debug.LogError("[TilemapDungeonGenerator] Missing tilemap or tile set reference.", this);
                return;
            }

            if (!tileSet.HasRequiredSprites)
            {
                Debug.LogError("[TilemapDungeonGenerator] Tile set needs at least one weighted floor Sprite and one weighted wall Sprite.", this);
                return;
            }

            UnityEngine.Random.State previousState = UnityEngine.Random.state;
            if (!useRandomSeed)
            {
                UnityEngine.Random.InitState(seed);
            }

            Clear();

            bool generatedFromTemplates = false;
            if (generationMode == GenerationMode.TemplateRooms)
            {
                generatedFromTemplates = TryBuildTemplateRoomLayout(roomDefinitions);
                if (!generatedFromTemplates && !fallbackToProceduralIfTemplateMissing)
                {
                    Debug.LogError("[TilemapDungeonGenerator] Template room generation failed. Assign a Room Template Set or enable fallback.", this);
                    RestoreRandomState(previousState);
                    return;
                }
            }

            if (!generatedFromTemplates)
            {
                BuildRoomLayout();
                StampRoomsAndCorridors();
            }

            StampWalls();
            ApplyTiles();
            ApplyDoors();
            ApplyRoomTriggers();
            LockAllDoors();

            if (addDoorColliders && doorColliders.Count == 0 && generatedRooms.Count > 1)
            {
                Debug.LogWarning("[TilemapDungeonGenerator] No door colliders were generated. Check corridor width, room count, and door settings.", this);
            }

            RestoreRandomState(previousState);

            DungeonGenerated?.Invoke(generatedRooms);
        }

        public void Generate(int targetRoomCount)
        {
            roomCount = Mathf.Max(1, targetRoomCount);
            activeRoomDefinitions = null;
            GenerateInternal(null);
        }

        private void RestoreRandomState(UnityEngine.Random.State previousState)
        {
            if (!useRandomSeed)
            {
                UnityEngine.Random.state = previousState;
            }
        }

        public Vector3 GetRoomCenterWorld(int roomIndex)
        {
            if (roomIndex >= 0 && roomIndex < roomTemplateCenters.Count)
            {
                return roomTemplateCenters[roomIndex];
            }

            if (roomIndex < 0 || roomIndex >= generatedRooms.Count || floorTilemap == null)
            {
                return transform.position;
            }

            Vector3Int cell = Vector3Int.RoundToInt((Vector3)generatedRooms[roomIndex].center);
            return floorTilemap.GetCellCenterWorld(cell);
        }

        public bool TryGetRoomSpawnPositions(int roomIndex, List<Vector3> results)
        {
            if (results == null)
            {
                return false;
            }

            results.Clear();
            if (roomIndex < 0 || roomIndex >= roomTemplateSpawnPositions.Count)
            {
                return false;
            }

            List<Vector3> positions = roomTemplateSpawnPositions[roomIndex];
            if (positions == null || positions.Count == 0)
            {
                return false;
            }

            results.AddRange(positions);
            return true;
        }

        public bool TryGetRoomBounds(int roomIndex, out RectInt roomBounds)
        {
            if (roomIndex < 0 || roomIndex >= generatedRooms.Count)
            {
                roomBounds = default;
                return false;
            }

            roomBounds = generatedRooms[roomIndex];
            return true;
        }

        public bool TryGetDoorCenterWorld(int doorIndex, out Vector3 worldPosition)
        {
            worldPosition = transform.position;

            if (floorTilemap == null || doorIndex < 0 || doorIndex >= doorCellGroups.Count)
            {
                return false;
            }

            if (doorIndex < doorWorldCenters.Count)
            {
                worldPosition = doorWorldCenters[doorIndex];
                if (worldPosition != Vector3.positiveInfinity)
                {
                    return true;
                }
            }

            List<Vector3Int> cells = doorCellGroups[doorIndex];
            if (cells == null || cells.Count == 0)
            {
                return false;
            }

            Vector3 sum = Vector3.zero;
            for (int i = 0; i < cells.Count; i++)
            {
                sum += floorTilemap.GetCellCenterWorld(cells[i]);
            }

            worldPosition = sum / cells.Count;
            return true;
        }

        public bool TryGetRoomExitDoorCenterWorld(int roomIndex, out Vector3 worldPosition)
        {
            worldPosition = transform.position;

            if (roomIndex < 0 || roomIndex >= roomExitDoorIndices.Count)
            {
                return false;
            }

            return TryGetDoorCenterWorld(roomExitDoorIndices[roomIndex], out worldPosition);
        }

        public void LockDoor(int doorIndex)
        {
            SetDoorLocked(doorIndex, true);
        }

        public void UnlockDoor(int doorIndex)
        {
            SetDoorLocked(doorIndex, false);
        }

        public void LockEveryDoor()
        {
            for (int i = 0; i < doorColliders.Count; i++)
            {
                SetDoorLocked(i, true);
            }
        }

        public void UnlockEveryDoor()
        {
            for (int i = 0; i < doorColliders.Count; i++)
            {
                SetDoorLocked(i, false);
            }
        }

        private void EnsureTilemaps()
        {
            Grid grid = GetComponentInChildren<Grid>();
            if (grid == null)
            {
                GameObject gridObject = new GameObject("Generated Dungeon Grid");
                gridObject.transform.SetParent(transform, false);
                grid = gridObject.AddComponent<Grid>();
            }

            if (floorTilemap == null)
            {
                floorTilemap = CreateTilemap(grid.transform, "Floor Tilemap", 0);
            }

            if (wallTilemap == null)
            {
                wallTilemap = CreateTilemap(grid.transform, "Wall Tilemap", 1);
            }

            if (addWallCollider)
            {
                EnsureWallCollider();
            }

            if (runManager == null)
            {
                runManager = GetComponentInParent<RoomRunManager>();
                SubscribeToRunManager();
            }
        }

        private static Tilemap CreateTilemap(Transform parent, string objectName, int sortingOrder)
        {
            GameObject tilemapObject = new GameObject(objectName);
            tilemapObject.transform.SetParent(parent, false);

            Tilemap tilemap = tilemapObject.AddComponent<Tilemap>();
            TilemapRenderer renderer = tilemapObject.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = sortingOrder;

            return tilemap;
        }

        private void EnsureWallCollider()
        {
            if (wallTilemap == null)
            {
                return;
            }

            TilemapCollider2D tilemapCollider = wallTilemap.GetComponent<TilemapCollider2D>();
            if (tilemapCollider == null)
            {
                tilemapCollider = wallTilemap.gameObject.AddComponent<TilemapCollider2D>();
            }
        }

        private void Clear()
        {
            generatedRooms.Clear();
            floorCells.Clear();
            wallCells.Clear();
            doorCellGroups.Clear();
            roomEntranceDoorIndices.Clear();
            roomExitDoorIndices.Clear();
            templateDoors.Clear();
            doorWorldCenters.Clear();
            roomTemplateInstances.Clear();
            roomTemplateSpawnPositions.Clear();
            roomTemplateCenters.Clear();

            floorTilemap.ClearAllTiles();
            wallTilemap.ClearAllTiles();
            ClearGeneratedTemplateRooms();
            ClearGeneratedDoors();
            ClearGeneratedRoomTriggers();
        }

        private void BuildRoomLayout()
        {
            int safeRoomCount = Mathf.Max(1, roomCount);
            Vector2Int center = Vector2Int.zero;
            Vector2Int previousDirection = Vector2Int.right;

            Vector2Int firstSize = GetRandomRoomSize();
            RectInt firstRoom = CreateCenteredRoom(center, firstSize);
            generatedRooms.Add(firstRoom);

            for (int i = 1; i < safeRoomCount; i++)
            {
                Vector2Int size = GetRandomRoomSize();
                RectInt previousRoom = generatedRooms[generatedRooms.Count - 1];

                if (!TryCreateSeparatedRoom(previousRoom, center, size, previousDirection, out RectInt room, out Vector2Int nextCenter, out Vector2Int nextDirection))
                {
                    room = CreateFallbackSeparatedRoom(previousRoom, center, size, previousDirection, out nextCenter, out nextDirection);
                    Debug.LogWarning(
                        $"[TilemapDungeonGenerator] Room {i} needed fallback placement. Consider increasing Room Spacing or Room Separation Padding.",
                        this);
                }

                generatedRooms.Add(room);
                center = nextCenter;
                previousDirection = nextDirection;
            }
        }

        private bool TryCreateSeparatedRoom(
            RectInt previousRoom,
            Vector2Int previousCenter,
            Vector2Int size,
            Vector2Int previousDirection,
            out RectInt room,
            out Vector2Int center,
            out Vector2Int direction)
        {
            Vector2Int[] directions = GetDirectionOrder(previousDirection);
            int attempts = Mathf.Max(4, maxRoomPlacementAttempts);
            int attemptCount = 0;
            int distanceStep = Mathf.Max(4, roomSpacing);

            for (int distanceLayer = 0; attemptCount < attempts; distanceLayer++)
            {
                for (int i = 0; i < directions.Length && attemptCount < attempts; i++)
                {
                    direction = directions[i];
                    attemptCount++;

                    if (distanceLayer == 0 && direction + previousDirection == Vector2Int.zero)
                    {
                        continue;
                    }

                    int extraDistance = distanceLayer * distanceStep;
                    center = previousCenter + direction * GetStepDistance(previousRoom, size, direction, extraDistance);
                    room = CreateCenteredRoom(center, size);

                    if (IsRoomPlacementValid(room))
                    {
                        return true;
                    }
                }
            }

            room = default;
            center = previousCenter;
            direction = previousDirection;
            return false;
        }

        private RectInt CreateFallbackSeparatedRoom(
            RectInt previousRoom,
            Vector2Int previousCenter,
            Vector2Int size,
            Vector2Int previousDirection,
            out Vector2Int center,
            out Vector2Int direction)
        {
            Vector2Int[] directions = GetDirectionOrder(previousDirection);
            int distanceStep = Mathf.Max(4, roomSpacing);
            int startLayer = Mathf.Max(1, maxRoomPlacementAttempts / 4);

            for (int distanceLayer = startLayer; distanceLayer < startLayer + 64; distanceLayer++)
            {
                for (int i = 0; i < directions.Length; i++)
                {
                    direction = directions[i];
                    int extraDistance = distanceLayer * distanceStep;
                    center = previousCenter + direction * GetStepDistance(previousRoom, size, direction, extraDistance);
                    RectInt room = CreateCenteredRoom(center, size);

                    if (IsRoomPlacementValid(room))
                    {
                        return room;
                    }
                }
            }

            direction = previousDirection != Vector2Int.zero ? previousDirection : Vector2Int.right;
            center = previousCenter + direction * GetStepDistance(previousRoom, size, direction, startLayer * distanceStep);
            return CreateCenteredRoom(center, size);
        }

        private Vector2Int[] GetDirectionOrder(Vector2Int previousDirection)
        {
            Vector2Int[] directions =
            {
                Vector2Int.right,
                Vector2Int.left,
                Vector2Int.up,
                Vector2Int.down
            };

            int offset = UnityEngine.Random.Range(0, directions.Length);
            for (int i = 0; i < directions.Length; i++)
            {
                int swapIndex = (offset + i) % directions.Length;
                (directions[i], directions[swapIndex]) = (directions[swapIndex], directions[i]);
            }

            if (previousDirection == Vector2Int.zero)
            {
                return directions;
            }

            int previousIndex = Array.IndexOf(directions, previousDirection);
            if (previousIndex > 0)
            {
                (directions[0], directions[previousIndex]) = (directions[previousIndex], directions[0]);
            }

            return directions;
        }

        private int GetStepDistance(RectInt previousRoom, Vector2Int nextRoomSize, Vector2Int direction, int extraDistance)
        {
            int baseDistance;
            if (direction.x != 0)
            {
                baseDistance = Mathf.CeilToInt(previousRoom.width * 0.5f + nextRoomSize.x * 0.5f);
            }
            else
            {
                baseDistance = Mathf.CeilToInt(previousRoom.height * 0.5f + nextRoomSize.y * 0.5f);
            }

            return Mathf.Max(1, baseDistance + roomSpacing + GetRoomSeparationMargin() + Mathf.Max(0, extraDistance));
        }

        private bool IsRoomPlacementValid(RectInt room)
        {
            RectInt paddedRoom = Inflate(room, GetRoomSeparationMargin());
            for (int i = 0; i < generatedRooms.Count; i++)
            {
                RectInt existingRoom = Inflate(generatedRooms[i], GetRoomSeparationMargin());
                if (Overlaps(paddedRoom, existingRoom))
                {
                    return false;
                }
            }

            return true;
        }

        private int GetRoomSeparationMargin()
        {
            return Mathf.Max(0, roomSeparationPadding + wallThickness + corridorWidth / 2);
        }

        private Vector2Int GetRandomRoomSize()
        {
            int width = UnityEngine.Random.Range(minRoomSize.x, maxRoomSize.x + 1);
            int height = UnityEngine.Random.Range(minRoomSize.y, maxRoomSize.y + 1);

            return new Vector2Int(
                Mathf.Max(4, width),
                Mathf.Max(4, height));
        }

        private static RectInt CreateCenteredRoom(Vector2Int center, Vector2Int size)
        {
            int x = center.x - size.x / 2;
            int y = center.y - size.y / 2;
            return new RectInt(x, y, size.x, size.y);
        }

        private static RectInt Inflate(RectInt rect, int amount)
        {
            int safeAmount = Mathf.Max(0, amount);
            return new RectInt(
                rect.xMin - safeAmount,
                rect.yMin - safeAmount,
                rect.width + safeAmount * 2,
                rect.height + safeAmount * 2);
        }

        private static bool Overlaps(RectInt a, RectInt b)
        {
            return a.xMin < b.xMax
                && a.xMax > b.xMin
                && a.yMin < b.yMax
                && a.yMax > b.yMin;
        }

        private bool TryBuildTemplateRoomLayout(IReadOnlyList<RoomDefinition> roomDefinitions)
        {
            if (roomTemplateSet == null)
            {
                return false;
            }

            int safeRoomCount = roomDefinitions != null && roomDefinitions.Count > 0
                ? roomDefinitions.Count
                : Mathf.Max(1, roomCount);

            Transform parent = GetTemplateRoomRoot();
            RectInt previousBounds = default;

            for (int i = 0; i < safeRoomCount; i++)
            {
                RoomType roomType = GetTemplateRoomType(roomDefinitions, i);
                RoomTemplate2D template = roomTemplateSet.Pick(roomType);
                if (template == null)
                {
                    Debug.LogWarning($"[TilemapDungeonGenerator] No template found for room {i} ({roomType}).", this);
                    return false;
                }

                RoomTemplate2D instance = Instantiate(template, parent);
                instance.name = $"Room Template {i + 1} - {roomType}";

                RectInt localBounds = instance.LocalBounds;
                Vector2Int originCell = i == 0
                    ? -Vector2Int.RoundToInt(localBounds.center)
                    : new Vector2Int(previousBounds.xMax + Mathf.Max(1, templateRoomSpacing) - localBounds.xMin, -Mathf.RoundToInt(localBounds.center.y));

                instance.transform.position = floorTilemap.CellToWorld(new Vector3Int(originCell.x, originCell.y, 0));

                RectInt worldCellBounds = Offset(localBounds, originCell);
                generatedRooms.Add(worldCellBounds);
                roomTemplateInstances.Add(instance);
                roomTemplateCenters.Add(instance.CenterWorldPosition);

                List<Vector3> spawnPositions = new List<Vector3>();
                instance.CollectSpawnPositions(spawnPositions);
                roomTemplateSpawnPositions.Add(spawnPositions);

                RegisterTemplateRoomDoors(i, instance);

                if (i > 0)
                {
                    Vector3Int fromCell = floorTilemap.WorldToCell(roomTemplateInstances[i - 1].ExitWorldPosition);
                    Vector3Int toCell = floorTilemap.WorldToCell(instance.EntranceWorldPosition);
                    Vector2Int from = new Vector2Int(fromCell.x, fromCell.y);
                    Vector2Int to = new Vector2Int(toCell.x, toCell.y);
                    StampCorridor(from, to);
                }

                previousBounds = worldCellBounds;
            }

            return generatedRooms.Count > 0;
        }

        private static RectInt Offset(RectInt rect, Vector2Int offset)
        {
            return new RectInt(rect.x + offset.x, rect.y + offset.y, rect.width, rect.height);
        }

        private static RoomType GetTemplateRoomType(IReadOnlyList<RoomDefinition> roomDefinitions, int index)
        {
            if (roomDefinitions == null || index < 0 || index >= roomDefinitions.Count || roomDefinitions[index] == null)
            {
                return RoomType.Combat;
            }

            return roomDefinitions[index].RoomType;
        }

        private void RegisterTemplateRoomDoors(int roomIndex, RoomTemplate2D instance)
        {
            if (instance == null)
            {
                return;
            }

            int entranceIndex = AddTemplateDoor(instance.EntranceDoor, instance.EntranceWorldPosition);
            int exitIndex = AddTemplateDoor(instance.ExitDoor, instance.ExitWorldPosition);
            SetRoomEntranceDoorIndex(roomIndex, entranceIndex);
            SetRoomExitDoorIndex(roomIndex, exitIndex);
        }

        private int AddTemplateDoor(RoomTemplateDoor2D door, Vector3 worldCenter)
        {
            doorCellGroups.Add(new List<Vector3Int>());
            templateDoors.Add(door);
            doorWorldCenters.Add(worldCenter);
            return doorCellGroups.Count - 1;
        }

        private static Vector2Int GetNextDirection(Vector2Int previousDirection)
        {
            Vector2Int[] directions =
            {
                Vector2Int.right,
                Vector2Int.left,
                Vector2Int.up,
                Vector2Int.down
            };

            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector2Int candidate = directions[UnityEngine.Random.Range(0, directions.Length)];
                if (candidate + previousDirection != Vector2Int.zero)
                {
                    return candidate;
                }
            }

            return previousDirection;
        }

        private void StampRoomsAndCorridors()
        {
            for (int i = 0; i < generatedRooms.Count; i++)
            {
                StampRoom(generatedRooms[i]);

                if (i > 0)
                {
                    RectInt previousRoom = generatedRooms[i - 1];
                    RectInt currentRoom = generatedRooms[i];
                    Vector2Int from = Vector2Int.RoundToInt(previousRoom.center);
                    Vector2Int to = Vector2Int.RoundToInt(currentRoom.center);
                    StampCorridor(from, to);
                    StampConnectionDoors(i - 1, previousRoom, currentRoom, from, to);
                }
            }
        }

        private void StampRoom(RectInt room)
        {
            for (int x = room.xMin; x < room.xMax; x++)
            {
                for (int y = room.yMin; y < room.yMax; y++)
                {
                    floorCells.Add(new Vector3Int(x, y, 0));
                }
            }
        }

        private void StampCorridor(Vector2Int from, Vector2Int to)
        {
            Vector2Int corner = new Vector2Int(to.x, from.y);
            StampCorridorLine(from, corner);
            StampCorridorLine(corner, to);
        }

        private void StampCorridorLine(Vector2Int from, Vector2Int to)
        {
            int halfWidth = Mathf.Max(0, corridorWidth / 2);
            int minX = Mathf.Min(from.x, to.x);
            int maxX = Mathf.Max(from.x, to.x);
            int minY = Mathf.Min(from.y, to.y);
            int maxY = Mathf.Max(from.y, to.y);

            for (int x = minX - halfWidth; x <= maxX + halfWidth; x++)
            {
                for (int y = minY - halfWidth; y <= maxY + halfWidth; y++)
                {
                    floorCells.Add(new Vector3Int(x, y, 0));
                }
            }
        }

        private void StampConnectionDoors(int connectionIndex, RectInt sourceRoom, RectInt targetRoom, Vector2Int from, Vector2Int to)
        {
            Vector2Int exitDirection = GetFirstCorridorDirection(from, to);
            Vector2Int entryDirection = GetLastCorridorDirection(from, to);
            if (exitDirection == Vector2Int.zero || entryDirection == Vector2Int.zero)
            {
                return;
            }

            int sourceExitDoorIndex = AddDoorCells(sourceRoom, from, exitDirection);
            int targetEntranceDoorIndex = AddDoorCells(targetRoom, to, -entryDirection);

            SetRoomExitDoorIndex(connectionIndex, sourceExitDoorIndex);
            SetRoomEntranceDoorIndex(connectionIndex + 1, targetEntranceDoorIndex);
        }

        private int AddDoorCells(RectInt room, Vector2Int roomCenter, Vector2Int direction)
        {
            Vector2Int center = GetDoorCenter(room, roomCenter, direction);
            int halfWidth = Mathf.Max(0, corridorWidth / 2);
            int thickness = Mathf.Max(1, doorThickness);
            List<Vector3Int> cells = new List<Vector3Int>();

            for (int widthOffset = -halfWidth; widthOffset <= halfWidth; widthOffset++)
            {
                for (int depth = 0; depth < thickness; depth++)
                {
                    Vector2Int cell = center + direction * depth;

                    if (direction.x != 0)
                    {
                        cell.y += widthOffset;
                    }
                    else
                    {
                        cell.x += widthOffset;
                    }

                    Vector3Int tileCell = new Vector3Int(cell.x, cell.y, 0);
                    floorCells.Add(tileCell);
                    cells.Add(tileCell);
                }
            }

            doorCellGroups.Add(cells);
            templateDoors.Add(null);
            doorWorldCenters.Add(Vector3.positiveInfinity);
            return doorCellGroups.Count - 1;
        }

        private void SetRoomEntranceDoorIndex(int roomIndex, int doorIndex)
        {
            while (roomEntranceDoorIndices.Count <= roomIndex)
            {
                roomEntranceDoorIndices.Add(-1);
            }

            roomEntranceDoorIndices[roomIndex] = doorIndex;
        }

        private void SetRoomExitDoorIndex(int roomIndex, int doorIndex)
        {
            while (roomExitDoorIndices.Count <= roomIndex)
            {
                roomExitDoorIndices.Add(-1);
            }

            roomExitDoorIndices[roomIndex] = doorIndex;
        }

        private static Vector2Int GetFirstCorridorDirection(Vector2Int from, Vector2Int to)
        {
            if (to.x > from.x)
            {
                return Vector2Int.right;
            }

            if (to.x < from.x)
            {
                return Vector2Int.left;
            }

            if (to.y > from.y)
            {
                return Vector2Int.up;
            }

            if (to.y < from.y)
            {
                return Vector2Int.down;
            }

            return Vector2Int.zero;
        }

        private static Vector2Int GetLastCorridorDirection(Vector2Int from, Vector2Int to)
        {
            if (to.y > from.y)
            {
                return Vector2Int.up;
            }

            if (to.y < from.y)
            {
                return Vector2Int.down;
            }

            return GetFirstCorridorDirection(from, to);
        }

        private static Vector2Int GetDoorCenter(RectInt sourceRoom, Vector2Int roomCenter, Vector2Int direction)
        {
            if (direction == Vector2Int.right)
            {
                return new Vector2Int(sourceRoom.xMax, roomCenter.y);
            }

            if (direction == Vector2Int.left)
            {
                return new Vector2Int(sourceRoom.xMin - 1, roomCenter.y);
            }

            if (direction == Vector2Int.up)
            {
                return new Vector2Int(roomCenter.x, sourceRoom.yMax);
            }

            return new Vector2Int(roomCenter.x, sourceRoom.yMin - 1);
        }

        private void StampWalls()
        {
            int thickness = Mathf.Max(1, wallThickness);

            foreach (Vector3Int floor in floorCells)
            {
                for (int x = -thickness; x <= thickness; x++)
                {
                    for (int y = -thickness; y <= thickness; y++)
                    {
                        if (x == 0 && y == 0)
                        {
                            continue;
                        }

                        Vector3Int candidate = new Vector3Int(floor.x + x, floor.y + y, 0);
                        if (!floorCells.Contains(candidate))
                        {
                            wallCells.Add(candidate);
                        }
                    }
                }
            }
        }

        private void ApplyTiles()
        {
            foreach (Vector3Int cell in floorCells)
            {
                floorTilemap.SetTile(cell, tileSet.GetRandomFloorTile());
                wallTilemap.SetTile(cell, null);
            }

            foreach (Vector3Int cell in wallCells)
            {
                if (!floorCells.Contains(cell))
                {
                    wallTilemap.SetTile(cell, tileSet.GetRandomWallTile());
                }
            }
        }

        private void ApplyDoors()
        {
            if (!addDoorColliders)
            {
                return;
            }

            Transform parent = GetDoorRoot();

            for (int i = 0; i < doorCellGroups.Count; i++)
            {
                List<Vector3Int> cells = doorCellGroups[i];
                if (cells == null || cells.Count == 0)
                {
                    doorTilemaps.Add(null);
                    doorColliders.Add(null);
                    doorBlockers.Add(null);
                    continue;
                }

                Tilemap doorTilemap = CreateTilemap(parent, $"Door Tilemap {i}", 2);
                TilemapCollider2D doorCollider = doorTilemap.gameObject.AddComponent<TilemapCollider2D>();
                BoxCollider2D doorBlocker = CreateDoorBlocker(parent, i, cells);

                for (int cellIndex = 0; cellIndex < cells.Count; cellIndex++)
                {
                    doorTilemap.SetTile(cells[cellIndex], tileSet.GetRandomDoorTile());
                }

                doorTilemaps.Add(doorTilemap);
                doorColliders.Add(doorCollider);
                doorBlockers.Add(doorBlocker);
            }
        }

        private BoxCollider2D CreateDoorBlocker(Transform parent, int doorIndex, List<Vector3Int> cells)
        {
            if (floorTilemap == null || cells == null || cells.Count == 0)
            {
                return null;
            }

            Vector3Int min = cells[0];
            Vector3Int max = cells[0];
            for (int i = 1; i < cells.Count; i++)
            {
                Vector3Int cell = cells[i];
                min = new Vector3Int(Mathf.Min(min.x, cell.x), Mathf.Min(min.y, cell.y), 0);
                max = new Vector3Int(Mathf.Max(max.x, cell.x), Mathf.Max(max.y, cell.y), 0);
            }

            Vector3 worldMin = floorTilemap.CellToWorld(min);
            Vector3 worldMax = floorTilemap.CellToWorld(new Vector3Int(max.x + 1, max.y + 1, 0));

            GameObject blockerObject = new GameObject($"Door Blocker {doorIndex}");
            blockerObject.transform.SetParent(parent, true);
            blockerObject.transform.position = (worldMin + worldMax) * 0.5f;

            BoxCollider2D blocker = blockerObject.AddComponent<BoxCollider2D>();
            blocker.isTrigger = false;
            blocker.size = new Vector2(
                Mathf.Abs(worldMax.x - worldMin.x),
                Mathf.Abs(worldMax.y - worldMin.y));

            return blocker;
        }

        private void ApplyRoomTriggers()
        {
            if (!createRoomTriggers)
            {
                return;
            }

            if (runManager == null)
            {
                runManager = GetComponentInParent<RoomRunManager>();
            }

            if (runManager == null)
            {
                runManager = FindAnyObjectByType<RoomRunManager>();
            }

            if (runManager == null)
            {
                Debug.LogError("[TilemapDungeonGenerator] Cannot create room triggers. RoomRunManager was not found.", this);
                return;
            }

            SubscribeToRunManager();

            if (floorTilemap == null)
            {
                Debug.LogError("[TilemapDungeonGenerator] Cannot create room triggers. Floor Tilemap is missing.", this);
                return;
            }

            Transform parent = GetRoomTriggerRoot();
            int padding = Mathf.Max(0, roomTriggerPadding);

            for (int i = 0; i < generatedRooms.Count; i++)
            {
                RectInt room = generatedRooms[i];
                int width = Mathf.Max(1, room.width - padding * 2);
                int height = Mathf.Max(1, room.height - padding * 2);
                Vector3Int minCell = new Vector3Int(room.xMin + padding, room.yMin + padding, 0);
                Vector3Int maxCell = new Vector3Int(minCell.x + width, minCell.y + height, 0);
                Vector3 minWorld = floorTilemap.CellToWorld(minCell);
                Vector3 maxWorld = floorTilemap.CellToWorld(maxCell);

                GameObject triggerObject = new GameObject($"Room Trigger {i}");
                triggerObject.transform.SetParent(parent, true);
                triggerObject.transform.position = (minWorld + maxWorld) * 0.5f;

                BoxCollider2D trigger = triggerObject.AddComponent<BoxCollider2D>();
                trigger.isTrigger = true;
                trigger.size = new Vector2(
                    Mathf.Abs(maxWorld.x - minWorld.x),
                    Mathf.Abs(maxWorld.y - minWorld.y));

                GeneratedRoomTrigger roomTrigger = triggerObject.AddComponent<GeneratedRoomTrigger>();
                roomTrigger.Initialize(runManager, i);
            }
        }

        private Transform GetDoorRoot()
        {
            Transform gridRoot = GetGridRoot();
            if (doorRoot != null && doorRoot.parent == gridRoot)
            {
                return doorRoot;
            }

            GameObject root = new GameObject("Generated Doors");
            root.transform.SetParent(gridRoot, false);
            doorRoot = root.transform;
            return doorRoot;
        }

        private Transform GetTemplateRoomRoot()
        {
            if (templateRoomRoot != null && templateRoomRoot.parent == transform)
            {
                return templateRoomRoot;
            }

            GameObject root = new GameObject("Generated Room Templates");
            root.transform.SetParent(transform, false);
            templateRoomRoot = root.transform;
            return templateRoomRoot;
        }

        private Transform GetRoomTriggerRoot()
        {
            if (roomTriggerRoot != null && roomTriggerRoot.parent == transform)
            {
                return roomTriggerRoot;
            }

            GameObject root = new GameObject("Generated Room Triggers");
            root.transform.SetParent(transform, false);
            roomTriggerRoot = root.transform;
            return roomTriggerRoot;
        }

        private Transform GetGridRoot()
        {
            if (floorTilemap != null && floorTilemap.transform.parent != null)
            {
                return floorTilemap.transform.parent;
            }

            if (wallTilemap != null && wallTilemap.transform.parent != null)
            {
                return wallTilemap.transform.parent;
            }

            return transform;
        }

        private void ClearGeneratedDoors()
        {
            for (int i = 0; i < doorTilemaps.Count; i++)
            {
                if (doorTilemaps[i] != null)
                {
                    DestroyGeneratedObject(doorTilemaps[i].gameObject);
                }
            }

            doorTilemaps.Clear();
            doorColliders.Clear();
            doorBlockers.Clear();

            if (doorRoot != null)
            {
                DestroyGeneratedObject(doorRoot.gameObject);
                doorRoot = null;
            }
        }

        private void ClearGeneratedRoomTriggers()
        {
            if (roomTriggerRoot != null)
            {
                DestroyGeneratedObject(roomTriggerRoot.gameObject);
                roomTriggerRoot = null;
            }
        }

        private void ClearGeneratedTemplateRooms()
        {
            if (templateRoomRoot != null)
            {
                DestroyGeneratedObject(templateRoomRoot.gameObject);
                templateRoomRoot = null;
            }
        }

        private static void DestroyGeneratedObject(GameObject target)
        {
            target.transform.SetParent(null);
            target.name = $"{target.name}_Destroying";
            target.SetActive(false);

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private void LockAllDoors()
        {
            LockEveryDoor();
        }

        private void SetDoorLocked(int doorIndex, bool locked)
        {
            if (doorIndex < 0 || doorIndex >= doorCellGroups.Count)
            {
                return;
            }

            TilemapCollider2D doorCollider = doorIndex < doorColliders.Count ? doorColliders[doorIndex] : null;
            if (doorCollider != null)
            {
                doorCollider.enabled = locked;
            }

            if (doorIndex < doorBlockers.Count && doorBlockers[doorIndex] != null)
            {
                doorBlockers[doorIndex].enabled = locked;
            }

            if (hideDoorVisualWhenUnlocked && doorIndex < doorTilemaps.Count && doorTilemaps[doorIndex] != null)
            {
                TilemapRenderer renderer = doorTilemaps[doorIndex].GetComponent<TilemapRenderer>();
                if (renderer != null)
                {
                    renderer.enabled = locked;
                }
            }

            if (doorIndex < templateDoors.Count && templateDoors[doorIndex] != null)
            {
                templateDoors[doorIndex].SetLocked(locked);
            }
        }

        private void SubscribeToRunManager()
        {
            if (subscribedToRunManager || runManager == null)
            {
                return;
            }

            runManager.RunRoomStarted += HandleRunRoomStarted;
            runManager.RunRoomCleared += HandleRunRoomCleared;
            subscribedToRunManager = true;
        }

        private void UnsubscribeFromRunManager()
        {
            if (!subscribedToRunManager || runManager == null)
            {
                return;
            }

            runManager.RunRoomStarted -= HandleRunRoomStarted;
            runManager.RunRoomCleared -= HandleRunRoomCleared;
            subscribedToRunManager = false;
        }

        private void HandleRunRoomStarted(int roomIndex, RoomDefinition room)
        {
            if (room != null && room.LockDoorsOnEnter)
            {
                SetRoomEntranceLocked(roomIndex, true);
                SetRoomExitLocked(roomIndex, true);
            }
        }

        private void HandleRunRoomCleared(int roomIndex, RoomDefinition room)
        {
            if (room != null && room.UnlockExitOnClear)
            {
                SetRoomExitLocked(roomIndex, false);
                SetRoomEntranceLocked(roomIndex + 1, false);
            }
        }

        private void SetRoomEntranceLocked(int roomIndex, bool locked)
        {
            if (roomIndex < 0 || roomIndex >= roomEntranceDoorIndices.Count)
            {
                return;
            }

            SetDoorLocked(roomEntranceDoorIndices[roomIndex], locked);
        }

        private void SetRoomExitLocked(int roomIndex, bool locked)
        {
            if (roomIndex < 0 || roomIndex >= roomExitDoorIndices.Count)
            {
                return;
            }

            SetDoorLocked(roomExitDoorIndices[roomIndex], locked);
        }
    }
}
