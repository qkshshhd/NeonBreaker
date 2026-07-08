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

        private enum DoorSpriteFacing
        {
            Up,
            Down,
            Left,
            Right
        }

        private enum StructuredRoomShape
        {
            Rectangle,
            LShape,
            TShape,
            Cross,
            BentCorridor,
            Courtyard
        }

        private readonly struct BoundaryLineKey : IEquatable<BoundaryLineKey>
        {
            public BoundaryLineKey(bool horizontal, int fixedLine, int outwardSign)
            {
                Horizontal = horizontal;
                FixedLine = fixedLine;
                OutwardSign = outwardSign;
            }

            public readonly bool Horizontal;
            public readonly int FixedLine;
            public readonly int OutwardSign;

            public bool Equals(BoundaryLineKey other)
            {
                return Horizontal == other.Horizontal
                    && FixedLine == other.FixedLine
                    && OutwardSign == other.OutwardSign;
            }

            public override bool Equals(object obj)
            {
                return obj is BoundaryLineKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = Horizontal ? 17 : 31;
                    hash = hash * 397 ^ FixedLine;
                    hash = hash * 397 ^ OutwardSign;
                    return hash;
                }
            }
        }

        private readonly struct ReentrantCornerCollision
        {
            public ReentrantCornerCollision(Vector3Int cell, Vector2Int openA, Vector2Int openB)
            {
                Cell = cell;
                OpenA = openA;
                OpenB = openB;
            }

            public readonly Vector3Int Cell;
            public readonly Vector2Int OpenA;
            public readonly Vector2Int OpenB;
        }

        [Header("Tilemaps")]
        [SerializeField] private Tilemap floorTilemap;
        [SerializeField] private Tilemap wallTilemap;
        [SerializeField] private Tilemap wallCollisionTilemap;
        [SerializeField] private Tilemap decorationTilemap;
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

        [Header("Boss Room")]
        [SerializeField] private bool forceBossRoomSquare = true;
        [SerializeField, Min(4)] private int bossRoomSize = 18;
        [SerializeField] private bool keepBossRoomRectangular = true;

        [Header("Tutorial Room")]
        [SerializeField] private bool forceTutorialRoomSquare = true;
        [SerializeField, Min(4)] private int tutorialRoomSize = 14;
        [SerializeField] private bool keepTutorialRoomRectangular = true;

        [Header("Room Dressing")]
        [SerializeField] private bool decorateRooms = true;
        [SerializeField] private bool randomizeFloorTileRotation = true;
        [SerializeField] private bool randomizeDecorationTileRotation = true;
        [SerializeField, Range(0f, 1f)] private float roomDecorationChance = 0.08f;
        [SerializeField, Min(0)] private int decorationRoomPadding = 2;
        [SerializeField, Min(0)] private int decorationCenterSafeRadius = 2;
        [SerializeField] private bool varyRoomShapes = true;
        [SerializeField] private bool useStructuredRoomShapes = true;
        [SerializeField, Range(0f, 1f)] private float structuredRoomChance = 0.85f;
        [SerializeField, Min(3)] private int structureArmMinWidth = 4;
        [SerializeField, Min(0)] private int structureOuterPadding = 0;
        [SerializeField] private bool allowEdgeCutsOnStructuredRooms = false;
        [SerializeField, Range(0f, 1f)] private float cornerCutChance = 0.45f;
        [SerializeField] private Vector2Int cornerCutSize = new Vector2Int(2, 3);
        [SerializeField, Range(0f, 1f)] private float edgeCutChance = 0.45f;
        [SerializeField, Min(0)] private int maxEdgeCutsPerRoom = 3;
        [SerializeField] private Vector2Int edgeCutDepthRange = new Vector2Int(1, 3);
        [SerializeField] private Vector2Int edgeCutLengthRange = new Vector2Int(2, 5);
        [SerializeField, Min(0)] private int safeSpawnPaddingFromOutline = 2;

        [Header("Room Connectivity")]
        [SerializeField] private bool repairRoomConnectivity = true;
        [SerializeField] private bool fillSmallInteriorVoids = true;
        [SerializeField, Min(1)] private int maxInteriorVoidFillCells = 14;

        [Header("Room Outline")]
        [SerializeField] private bool useDirectionalOutlineTiles = true;
        [SerializeField] private bool suppressDoorwayOuterCornerTiles = true;
        [SerializeField] private bool useBoundaryBoxWallColliders = true;
        [SerializeField, Min(0.02f)] private float wallBoundaryColliderThickness = 0.18f;
        [SerializeField, Min(0f)] private float wallBoundaryColliderOutwardOffsetCells = 1f;
        [SerializeField] private bool collideOuterCornerTiles = false;
        [SerializeField] private bool sealCornerCollisionGaps = true;
        [SerializeField, Min(0)] private int wallCollisionOutwardOffset = 1;
        [SerializeField] private bool swapInnerOuterCornerTiles = true;

        [Header("Room Doors")]
        [SerializeField] private bool rotateDoorTilesByDirection = true;
        [SerializeField] private bool useDoorTilemapCollider;
        [SerializeField] private bool fitDoorWidthToCorridor = true;
        [SerializeField, Min(1)] private int doorWidthProbeDepth = 2;
        [SerializeField] private DoorSpriteFacing doorSpriteDefaultFacing = DoorSpriteFacing.Up;
        [SerializeField, Min(0)] private int doorBlockerSidePaddingCells = 2;
        [SerializeField, Min(0f)] private float doorBlockerDepthPaddingCells = 0.5f;

        [Header("Template Rooms")]
        [SerializeField] private RoomTemplateSet roomTemplateSet;
        [SerializeField] private bool fallbackToProceduralIfTemplateMissing = true;
        [SerializeField] private int templateRoomSpacing = 8;

        private readonly List<RectInt> generatedRooms = new List<RectInt>();
        private readonly HashSet<Vector3Int> floorCells = new HashSet<Vector3Int>();
        private readonly HashSet<Vector3Int> wallCells = new HashSet<Vector3Int>();
        private readonly List<List<Vector3Int>> roomFloorCells = new List<List<Vector3Int>>();
        private readonly List<List<Vector3Int>> doorCellGroups = new List<List<Vector3Int>>();
        private readonly List<Vector2Int> doorDirections = new List<Vector2Int>();
        private readonly List<Vector2Int> doorCenters = new List<Vector2Int>();
        private readonly List<Vector2Int> doorWidthOffsetRanges = new List<Vector2Int>();
        private readonly List<int> roomEntranceDoorIndices = new List<int>();
        private readonly List<int> roomExitDoorIndices = new List<int>();
        private readonly List<Tilemap> doorTilemaps = new List<Tilemap>();
        private readonly List<TilemapCollider2D> doorColliders = new List<TilemapCollider2D>();
        private readonly List<BoxCollider2D> doorBlockers = new List<BoxCollider2D>();
        private readonly List<BoxCollider2D> wallBoundaryColliders = new List<BoxCollider2D>();
        private readonly List<RoomTemplateDoor2D> templateDoors = new List<RoomTemplateDoor2D>();
        private readonly List<Vector3> doorWorldCenters = new List<Vector3>();
        private readonly List<RoomTemplate2D> roomTemplateInstances = new List<RoomTemplate2D>();
        private readonly List<List<Vector3>> roomTemplateSpawnPositions = new List<List<Vector3>>();
        private readonly List<Vector3> roomTemplateCenters = new List<Vector3>();
        private RoomDefinition[] activeRoomDefinitions;
        private Transform templateRoomRoot;
        private Transform doorRoot;
        private Transform wallBoundaryColliderRoot;
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
                RepairProceduralRoomConnectivity();
                FillSmallInteriorVoids();
            }

            StampWalls();
            ApplyTiles();
            ApplyRoomDecorations();
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

        public Vector3 GetRoomRewardWorldPosition(int roomIndex)
        {
            return TryGetSafeRoomWorldPosition(roomIndex, out Vector3 position)
                ? position
                : GetRoomCenterWorld(roomIndex);
        }

        public bool TryGetSafeRoomWorldPosition(int roomIndex, out Vector3 position)
        {
            position = GetRoomCenterWorld(roomIndex);

            if (floorTilemap == null || roomIndex < 0 || roomIndex >= roomFloorCells.Count)
            {
                return false;
            }

            List<Vector3Int> cells = roomFloorCells[roomIndex];
            if (cells == null || cells.Count == 0)
            {
                return false;
            }

            Vector2 center = roomIndex < generatedRooms.Count ? generatedRooms[roomIndex].center : Vector2.zero;
            int padding = Mathf.Max(0, safeSpawnPaddingFromOutline);
            for (int currentPadding = padding; currentPadding >= 0; currentPadding--)
            {
                bool found = false;
                float bestDistance = float.MaxValue;
                Vector3Int bestCell = cells[0];

                for (int i = 0; i < cells.Count; i++)
                {
                    Vector3Int cell = cells[i];
                    if (!HasFloorArea(cell, currentPadding))
                    {
                        continue;
                    }

                    Vector2 cellPosition = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
                    float distance = (cellPosition - center).sqrMagnitude;
                    if (!found || distance < bestDistance)
                    {
                        found = true;
                        bestDistance = distance;
                        bestCell = cell;
                    }
                }

                if (found)
                {
                    position = floorTilemap.GetCellCenterWorld(bestCell);
                    return true;
                }
            }

            return false;
        }

        public bool TryGetRoomSafeSpawnPositions(int roomIndex, List<Vector3> results)
        {
            if (results == null)
            {
                return false;
            }

            results.Clear();

            if (floorTilemap == null || roomIndex < 0 || roomIndex >= roomFloorCells.Count)
            {
                return false;
            }

            List<Vector3Int> cells = roomFloorCells[roomIndex];
            if (cells == null || cells.Count == 0)
            {
                return false;
            }

            int padding = Mathf.Max(0, safeSpawnPaddingFromOutline);
            for (int currentPadding = padding; currentPadding >= 0; currentPadding--)
            {
                results.Clear();
                for (int i = 0; i < cells.Count; i++)
                {
                    Vector3Int cell = cells[i];
                    if (HasFloorArea(cell, currentPadding))
                    {
                        results.Add(floorTilemap.GetCellCenterWorld(cell));
                    }
                }

                if (results.Count > 0)
                {
                    return true;
                }
            }

            return false;
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

        private bool HasFloorArea(Vector3Int centerCell, int padding)
        {
            if (padding <= 0)
            {
                return floorCells.Contains(centerCell);
            }

            for (int x = -padding; x <= padding; x++)
            {
                for (int y = -padding; y <= padding; y++)
                {
                    if (!floorCells.Contains(new Vector3Int(centerCell.x + x, centerCell.y + y, 0)))
                    {
                        return false;
                    }
                }
            }

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

        public bool TryGetRoomFloorBounds(int roomIndex, out RectInt roomBounds)
        {
            if (roomIndex < 0 || roomIndex >= roomFloorCells.Count)
            {
                return TryGetRoomBounds(roomIndex, out roomBounds);
            }

            List<Vector3Int> cells = roomFloorCells[roomIndex];
            if (cells == null || cells.Count == 0)
            {
                return TryGetRoomBounds(roomIndex, out roomBounds);
            }

            Vector3Int first = cells[0];
            int xMin = first.x;
            int yMin = first.y;
            int xMax = first.x + 1;
            int yMax = first.y + 1;

            for (int i = 1; i < cells.Count; i++)
            {
                Vector3Int cell = cells[i];
                xMin = Mathf.Min(xMin, cell.x);
                yMin = Mathf.Min(yMin, cell.y);
                xMax = Mathf.Max(xMax, cell.x + 1);
                yMax = Mathf.Max(yMax, cell.y + 1);
            }

            roomBounds = new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
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

            if (addWallCollider && wallCollisionTilemap == null)
            {
                wallCollisionTilemap = CreateTilemap(grid.transform, "Wall Collision Tilemap", -10);
            }

            SetTilemapRendererVisible(wallCollisionTilemap, false);

            if (decorationTilemap == null)
            {
                decorationTilemap = CreateTilemap(grid.transform, "Decoration Tilemap", 2);
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

        private static void SetTilemapRendererVisible(Tilemap tilemap, bool visible)
        {
            if (tilemap == null)
            {
                return;
            }

            TilemapRenderer renderer = tilemap.GetComponent<TilemapRenderer>();
            if (renderer != null)
            {
                renderer.enabled = visible;
            }
        }

        private void EnsureWallCollider()
        {
            Tilemap colliderTilemap = wallCollisionTilemap != null ? wallCollisionTilemap : wallTilemap;
            if (colliderTilemap == null)
            {
                return;
            }

            if (wallTilemap != null && wallTilemap != colliderTilemap)
            {
                TilemapCollider2D visualCollider = wallTilemap.GetComponent<TilemapCollider2D>();
                if (visualCollider != null)
                {
                    visualCollider.enabled = false;
                }
            }

            TilemapCollider2D tilemapCollider = colliderTilemap.GetComponent<TilemapCollider2D>();
            if (tilemapCollider == null)
            {
                tilemapCollider = colliderTilemap.gameObject.AddComponent<TilemapCollider2D>();
            }

            tilemapCollider.enabled = !useBoundaryBoxWallColliders;
        }

        private void Clear()
        {
            generatedRooms.Clear();
            floorCells.Clear();
            wallCells.Clear();
            roomFloorCells.Clear();
            doorCellGroups.Clear();
            doorDirections.Clear();
            doorCenters.Clear();
            doorWidthOffsetRanges.Clear();
            roomEntranceDoorIndices.Clear();
            roomExitDoorIndices.Clear();
            templateDoors.Clear();
            doorWorldCenters.Clear();
            roomTemplateInstances.Clear();
            roomTemplateSpawnPositions.Clear();
            roomTemplateCenters.Clear();

            floorTilemap.ClearAllTiles();
            wallTilemap.ClearAllTiles();
            if (wallCollisionTilemap != null)
            {
                wallCollisionTilemap.ClearAllTiles();
            }
            if (decorationTilemap != null)
            {
                decorationTilemap.ClearAllTiles();
            }
            ClearGeneratedTemplateRooms();
            ClearGeneratedDoors();
            ClearGeneratedWallBoundaryColliders();
            ClearGeneratedRoomTriggers();
        }

        private void BuildRoomLayout()
        {
            int safeRoomCount = Mathf.Max(1, roomCount);
            Vector2Int center = Vector2Int.zero;
            Vector2Int previousDirection = Vector2Int.right;

            Vector2Int firstSize = GetRoomSizeForIndex(0);
            RectInt firstRoom = CreateCenteredRoom(center, firstSize);
            generatedRooms.Add(firstRoom);

            for (int i = 1; i < safeRoomCount; i++)
            {
                Vector2Int size = GetRoomSizeForIndex(i);
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

        private Vector2Int GetRoomSizeForIndex(int roomIndex)
        {
            if (forceTutorialRoomSquare && IsTutorialRoomIndex(roomIndex))
            {
                int size = Mathf.Max(4, tutorialRoomSize);
                return new Vector2Int(size, size);
            }

            if (forceBossRoomSquare && IsBossRoomIndex(roomIndex))
            {
                int size = Mathf.Max(4, bossRoomSize);
                return new Vector2Int(size, size);
            }

            return GetRandomRoomSize();
        }

        private bool IsBossRoomIndex(int roomIndex)
        {
            if (roomIndex < 0 || activeRoomDefinitions == null || roomIndex >= activeRoomDefinitions.Length)
            {
                return false;
            }

            RoomDefinition roomDefinition = activeRoomDefinitions[roomIndex];
            return roomDefinition != null && roomDefinition.RoomType == RoomType.Boss;
        }

        private static bool IsTutorialRoomIndex(int roomIndex)
        {
            return roomIndex == 0;
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

        private readonly struct RoomShapeMask
        {
            private enum JunctionDirection
            {
                North,
                South,
                East,
                West
            }

            private enum EdgeSide
            {
                Bottom,
                Top,
                Left,
                Right
            }

            private readonly struct EdgeCut
            {
                private readonly EdgeSide side;
                private readonly int start;
                private readonly int length;
                private readonly int depth;

                public EdgeCut(EdgeSide side, int start, int length, int depth)
                {
                    this.side = side;
                    this.start = start;
                    this.length = length;
                    this.depth = depth;
                }

                public bool Contains(RectInt room, int x, int y)
                {
                    switch (side)
                    {
                        case EdgeSide.Bottom:
                            return x >= room.xMin + start
                                && x < room.xMin + start + length
                                && y >= room.yMin
                                && y < room.yMin + depth;
                        case EdgeSide.Top:
                            return x >= room.xMin + start
                                && x < room.xMin + start + length
                                && y < room.yMax
                                && y >= room.yMax - depth;
                        case EdgeSide.Left:
                            return y >= room.yMin + start
                                && y < room.yMin + start + length
                                && x >= room.xMin
                                && x < room.xMin + depth;
                        case EdgeSide.Right:
                            return y >= room.yMin + start
                                && y < room.yMin + start + length
                                && x < room.xMax
                                && x >= room.xMax - depth;
                        default:
                            return false;
                    }
                }
            }

            private readonly RectInt room;
            private readonly bool cutBottomLeft;
            private readonly bool cutBottomRight;
            private readonly bool cutTopLeft;
            private readonly bool cutTopRight;
            private readonly int cutWidth;
            private readonly int cutHeight;
            private readonly EdgeCut[] edgeCuts;
            private readonly StructuredRoomShape structuredShape;
            private readonly JunctionDirection structuredDirection;
            private readonly int armWidth;
            private readonly int outerPadding;
            private readonly RectInt innerHole;
            private readonly bool hasInnerHole;

            private RoomShapeMask(
                RectInt room,
                bool cutBottomLeft,
                bool cutBottomRight,
                bool cutTopLeft,
                bool cutTopRight,
                int cutWidth,
                int cutHeight,
                EdgeCut[] edgeCuts,
                StructuredRoomShape structuredShape,
                JunctionDirection structuredDirection,
                int armWidth,
                int outerPadding,
                RectInt innerHole,
                bool hasInnerHole)
            {
                this.room = room;
                this.cutBottomLeft = cutBottomLeft;
                this.cutBottomRight = cutBottomRight;
                this.cutTopLeft = cutTopLeft;
                this.cutTopRight = cutTopRight;
                this.cutWidth = cutWidth;
                this.cutHeight = cutHeight;
                this.edgeCuts = edgeCuts;
                this.structuredShape = structuredShape;
                this.structuredDirection = structuredDirection;
                this.armWidth = armWidth;
                this.outerPadding = outerPadding;
                this.innerHole = innerHole;
                this.hasInnerHole = hasInnerHole;
            }

            public static RoomShapeMask Create(
                RectInt room,
                bool enabled,
                bool useStructuredShapes,
                float structuredChance,
                int requestedArmWidth,
                int requestedOuterPadding,
                bool allowCutsOnStructuredRooms,
                float cornerChance,
                Vector2Int cornerSize,
                float edgeChance,
                int maxEdgeCuts,
                Vector2Int edgeDepthRange,
                Vector2Int edgeLengthRange)
            {
                if (!enabled)
                {
                    return new RoomShapeMask(
                        room,
                        false,
                        false,
                        false,
                        false,
                        0,
                        0,
                        null,
                        StructuredRoomShape.Rectangle,
                        JunctionDirection.North,
                        0,
                        0,
                        default,
                        false);
                }

                bool useStructured = useStructuredShapes
                    && structuredChance > 0f
                    && UnityEngine.Random.value < structuredChance
                    && room.width >= 8
                    && room.height >= 8;

                StructuredRoomShape structuredShape = useStructured
                    ? PickStructuredShape(room)
                    : StructuredRoomShape.Rectangle;
                JunctionDirection structuredDirection = (JunctionDirection)UnityEngine.Random.Range(0, 4);
                int maxArmWidth = Mathf.Max(3, Mathf.Min(room.width, room.height) - 2);
                int armWidth = Mathf.Clamp(requestedArmWidth + UnityEngine.Random.Range(0, 2), 3, maxArmWidth);
                int outerPadding = Mathf.Clamp(requestedOuterPadding, 0, Mathf.Max(0, Mathf.Min(room.width, room.height) / 4));
                RectInt innerHole = CreateInnerHole(room, armWidth);
                bool hasInnerHole = structuredShape == StructuredRoomShape.Courtyard && innerHole.width > 0 && innerHole.height > 0;
                bool useLegacyCuts = !useStructured || allowCutsOnStructuredRooms;
                int width = Mathf.Clamp(cornerSize.x, 1, Mathf.Max(1, room.width / 3));
                int height = Mathf.Clamp(cornerSize.y, 1, Mathf.Max(1, room.height / 3));
                return new RoomShapeMask(
                    room,
                    useLegacyCuts && cornerChance > 0f && UnityEngine.Random.value < cornerChance,
                    useLegacyCuts && cornerChance > 0f && UnityEngine.Random.value < cornerChance,
                    useLegacyCuts && cornerChance > 0f && UnityEngine.Random.value < cornerChance,
                    useLegacyCuts && cornerChance > 0f && UnityEngine.Random.value < cornerChance,
                    width,
                    height,
                    useLegacyCuts ? CreateEdgeCuts(room, edgeChance, maxEdgeCuts, edgeDepthRange, edgeLengthRange) : null,
                    structuredShape,
                    structuredDirection,
                    armWidth,
                    outerPadding,
                    innerHole,
                    hasInnerHole);
            }

            public bool Contains(int x, int y)
            {
                if (!ContainsStructuredShape(x, y))
                {
                    return false;
                }

                int leftDistance = x - room.xMin;
                int rightDistance = room.xMax - 1 - x;
                int bottomDistance = y - room.yMin;
                int topDistance = room.yMax - 1 - y;

                if (cutBottomLeft && leftDistance < cutWidth && bottomDistance < cutHeight)
                {
                    return false;
                }

                if (cutBottomRight && rightDistance < cutWidth && bottomDistance < cutHeight)
                {
                    return false;
                }

                if (cutTopLeft && leftDistance < cutWidth && topDistance < cutHeight)
                {
                    return false;
                }

                if (cutTopRight && rightDistance < cutWidth && topDistance < cutHeight)
                {
                    return false;
                }

                if (edgeCuts != null)
                {
                    for (int i = 0; i < edgeCuts.Length; i++)
                    {
                        if (edgeCuts[i].Contains(room, x, y))
                        {
                            return false;
                        }
                    }
                }

                return true;
            }

            private bool ContainsStructuredShape(int x, int y)
            {
                int localX = x - room.xMin;
                int localY = y - room.yMin;
                int width = room.width;
                int height = room.height;
                int padding = outerPadding;

                if (localX < padding || localX >= width - padding || localY < padding || localY >= height - padding)
                {
                    return structuredShape == StructuredRoomShape.Rectangle;
                }

                int horizontalStart = Mathf.Max(padding, (width - armWidth) / 2);
                int horizontalEnd = Mathf.Min(width - padding, horizontalStart + armWidth);
                int verticalStart = Mathf.Max(padding, (height - armWidth) / 2);
                int verticalEnd = Mathf.Min(height - padding, verticalStart + armWidth);

                bool centerColumn = localX >= horizontalStart && localX < horizontalEnd;
                bool centerRow = localY >= verticalStart && localY < verticalEnd;
                bool bottomBand = localY < padding + armWidth;
                bool topBand = localY >= height - padding - armWidth;
                bool leftBand = localX < padding + armWidth;
                bool rightBand = localX >= width - padding - armWidth;

                switch (structuredShape)
                {
                    case StructuredRoomShape.LShape:
                        return ContainsLShape(leftBand, rightBand, bottomBand, topBand);
                    case StructuredRoomShape.TShape:
                        return ContainsTShape(centerColumn, centerRow, leftBand, rightBand, bottomBand, topBand);
                    case StructuredRoomShape.Cross:
                        return centerColumn || centerRow;
                    case StructuredRoomShape.BentCorridor:
                        return ContainsBentCorridor(localX, localY, centerColumn, centerRow);
                    case StructuredRoomShape.Courtyard:
                        return !hasInnerHole || !innerHole.Contains(new Vector2Int(x, y));
                    default:
                        return true;
                }
            }

            private bool ContainsLShape(bool leftBand, bool rightBand, bool bottomBand, bool topBand)
            {
                switch (structuredDirection)
                {
                    case JunctionDirection.North:
                        return leftBand || topBand;
                    case JunctionDirection.South:
                        return rightBand || bottomBand;
                    case JunctionDirection.East:
                        return rightBand || topBand;
                    default:
                        return leftBand || bottomBand;
                }
            }

            private bool ContainsTShape(
                bool centerColumn,
                bool centerRow,
                bool leftBand,
                bool rightBand,
                bool bottomBand,
                bool topBand)
            {
                switch (structuredDirection)
                {
                    case JunctionDirection.North:
                        return centerColumn || topBand;
                    case JunctionDirection.South:
                        return centerColumn || bottomBand;
                    case JunctionDirection.East:
                        return centerRow || rightBand;
                    default:
                        return centerRow || leftBand;
                }
            }

            private bool ContainsBentCorridor(int localX, int localY, bool centerColumn, bool centerRow)
            {
                bool northHalf = localY >= room.height / 2;
                bool southHalf = localY <= room.height / 2;
                bool eastHalf = localX >= room.width / 2;
                bool westHalf = localX <= room.width / 2;
                bool elbow = Mathf.Abs(localX - room.width / 2) <= armWidth / 2
                    && Mathf.Abs(localY - room.height / 2) <= armWidth / 2;

                switch (structuredDirection)
                {
                    case JunctionDirection.North:
                        return (centerColumn && northHalf) || (centerRow && eastHalf) || elbow;
                    case JunctionDirection.South:
                        return (centerColumn && southHalf) || (centerRow && westHalf) || elbow;
                    case JunctionDirection.East:
                        return (centerRow && eastHalf) || (centerColumn && southHalf) || elbow;
                    default:
                        return (centerRow && westHalf) || (centerColumn && northHalf) || elbow;
                }
            }

            private static StructuredRoomShape PickStructuredShape(RectInt room)
            {
                if (room.width < 10 || room.height < 10)
                {
                    return UnityEngine.Random.value < 0.55f
                        ? StructuredRoomShape.LShape
                        : StructuredRoomShape.TShape;
                }

                int roll = UnityEngine.Random.Range(0, 100);
                if (roll < 24)
                {
                    return StructuredRoomShape.LShape;
                }

                if (roll < 46)
                {
                    return StructuredRoomShape.TShape;
                }

                if (roll < 65)
                {
                    return StructuredRoomShape.BentCorridor;
                }

                if (roll < 82)
                {
                    return StructuredRoomShape.Cross;
                }

                if (roll < 94)
                {
                    return StructuredRoomShape.Courtyard;
                }

                return StructuredRoomShape.Rectangle;
            }

            private static RectInt CreateInnerHole(RectInt room, int armWidth)
            {
                int holeWidth = Mathf.Clamp(room.width / 3, 2, Mathf.Max(2, room.width - armWidth * 2));
                int holeHeight = Mathf.Clamp(room.height / 3, 2, Mathf.Max(2, room.height - armWidth * 2));
                if (holeWidth <= 1 || holeHeight <= 1)
                {
                    return default;
                }

                int holeX = room.xMin + (room.width - holeWidth) / 2;
                int holeY = room.yMin + (room.height - holeHeight) / 2;
                return new RectInt(holeX, holeY, holeWidth, holeHeight);
            }

            private static EdgeCut[] CreateEdgeCuts(
                RectInt room,
                float chance,
                int maxCuts,
                Vector2Int depthRange,
                Vector2Int lengthRange)
            {
                if (chance <= 0f || maxCuts <= 0)
                {
                    return null;
                }

                List<EdgeCut> cuts = null;
                int attempts = Mathf.Max(1, maxCuts);
                for (int i = 0; i < attempts; i++)
                {
                    if (UnityEngine.Random.value > chance)
                    {
                        continue;
                    }

                    EdgeSide side = (EdgeSide)UnityEngine.Random.Range(0, 4);
                    int axisLength = side == EdgeSide.Bottom || side == EdgeSide.Top ? room.width : room.height;
                    int maxDepth = side == EdgeSide.Bottom || side == EdgeSide.Top
                        ? Mathf.Max(1, room.height / 4)
                        : Mathf.Max(1, room.width / 4);

                    int minLength = Mathf.Clamp(Mathf.Min(lengthRange.x, lengthRange.y), 1, Mathf.Max(1, axisLength - 2));
                    int maxLength = Mathf.Clamp(Mathf.Max(lengthRange.x, lengthRange.y), minLength, Mathf.Max(minLength, axisLength - 2));
                    int length = UnityEngine.Random.Range(minLength, maxLength + 1);
                    int startMin = 1;
                    int startMax = Mathf.Max(startMin, axisLength - length - 1);
                    int start = UnityEngine.Random.Range(startMin, startMax + 1);

                    int minDepth = Mathf.Clamp(Mathf.Min(depthRange.x, depthRange.y), 1, maxDepth);
                    int maxCutDepth = Mathf.Clamp(Mathf.Max(depthRange.x, depthRange.y), minDepth, maxDepth);
                    int depth = UnityEngine.Random.Range(minDepth, maxCutDepth + 1);

                    cuts ??= new List<EdgeCut>();
                    cuts.Add(new EdgeCut(side, start, length, depth));
                }

                return cuts != null && cuts.Count > 0 ? cuts.ToArray() : null;
            }
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
                roomFloorCells.Add(new List<Vector3Int>());
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
            doorDirections.Add(Vector2Int.zero);
            doorCenters.Add(Vector2Int.zero);
            doorWidthOffsetRanges.Add(Vector2Int.zero);
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
                StampRoom(i, generatedRooms[i]);

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

        private void RepairProceduralRoomConnectivity()
        {
            if (!repairRoomConnectivity)
            {
                return;
            }

            for (int roomIndex = 0; roomIndex < generatedRooms.Count; roomIndex++)
            {
                EnsureRoomFloorCellList(roomIndex);
                List<Vector3Int> roomCells = roomFloorCells[roomIndex];
                if (roomCells.Count == 0)
                {
                    Vector3Int fallbackCell = Vector3Int.RoundToInt((Vector3)generatedRooms[roomIndex].center);
                    AddRoomFloorPatch(roomIndex, fallbackCell, 0);
                }

                Vector3Int anchor = GetBestRoomCellNear(roomIndex, generatedRooms[roomIndex].center);
                ConnectRoomDoorToAnchor(roomIndex, GetRoomEntranceDoorIndex(roomIndex), anchor);
                ConnectRoomDoorToAnchor(roomIndex, GetRoomExitDoorIndex(roomIndex), anchor);
                ConnectDisconnectedRoomIslands(roomIndex, anchor);
            }
        }

        private int GetRoomEntranceDoorIndex(int roomIndex)
        {
            return roomIndex >= 0 && roomIndex < roomEntranceDoorIndices.Count
                ? roomEntranceDoorIndices[roomIndex]
                : -1;
        }

        private int GetRoomExitDoorIndex(int roomIndex)
        {
            return roomIndex >= 0 && roomIndex < roomExitDoorIndices.Count
                ? roomExitDoorIndices[roomIndex]
                : -1;
        }

        private void ConnectRoomDoorToAnchor(int roomIndex, int doorIndex, Vector3Int anchor)
        {
            if (!TryGetDoorCellCenter(doorIndex, out Vector3Int doorCenter))
            {
                return;
            }

            CarveRoomPath(roomIndex, anchor, doorCenter);
        }

        private bool TryGetDoorCellCenter(int doorIndex, out Vector3Int center)
        {
            center = default;
            if (doorIndex < 0 || doorIndex >= doorCellGroups.Count)
            {
                return false;
            }

            List<Vector3Int> cells = doorCellGroups[doorIndex];
            if (cells == null || cells.Count == 0)
            {
                return false;
            }

            int x = 0;
            int y = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                x += cells[i].x;
                y += cells[i].y;
            }

            center = new Vector3Int(
                Mathf.RoundToInt((float)x / cells.Count),
                Mathf.RoundToInt((float)y / cells.Count),
                0);
            return true;
        }

        private Vector3Int GetBestRoomCellNear(int roomIndex, Vector2 target)
        {
            List<Vector3Int> cells = roomFloorCells[roomIndex];
            bool found = false;
            float bestDistance = float.MaxValue;
            Vector3Int bestCell = Vector3Int.RoundToInt(new Vector3(target.x, target.y, 0f));

            for (int i = 0; i < cells.Count; i++)
            {
                Vector3Int cell = cells[i];
                if (!floorCells.Contains(cell))
                {
                    continue;
                }

                Vector2 cellCenter = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
                float distance = (cellCenter - target).sqrMagnitude;
                if (!found || distance < bestDistance)
                {
                    found = true;
                    bestDistance = distance;
                    bestCell = cell;
                }
            }

            if (!found)
            {
                AddRoomFloorPatch(roomIndex, bestCell, 0);
            }

            return bestCell;
        }

        private void ConnectDisconnectedRoomIslands(int roomIndex, Vector3Int anchor)
        {
            List<Vector3Int> roomCells = roomFloorCells[roomIndex];
            if (roomCells.Count <= 1)
            {
                return;
            }

            for (int pass = 0; pass < roomCells.Count; pass++)
            {
                HashSet<Vector3Int> roomSet = new HashSet<Vector3Int>(roomCells);
                if (!roomSet.Contains(anchor))
                {
                    AddRoomFloorPatch(roomIndex, anchor, 0);
                    roomSet.Add(anchor);
                }

                HashSet<Vector3Int> reachable = FloodFillRoomCells(anchor, roomSet);
                if (reachable.Count >= roomSet.Count)
                {
                    return;
                }

                Vector3Int unreachable = default;
                bool foundUnreachable = false;
                for (int i = 0; i < roomCells.Count; i++)
                {
                    Vector3Int cell = roomCells[i];
                    if (reachable.Contains(cell))
                    {
                        continue;
                    }

                    unreachable = cell;
                    foundUnreachable = true;
                    break;
                }

                if (!foundUnreachable)
                {
                    return;
                }

                Vector3Int nearestReachable = FindNearestCell(unreachable, reachable);
                CarveRoomPath(roomIndex, nearestReachable, unreachable);
            }
        }

        private void FillSmallInteriorVoids()
        {
            if (!fillSmallInteriorVoids)
            {
                return;
            }

            int maxFillCells = Mathf.Max(1, maxInteriorVoidFillCells);
            for (int roomIndex = 0; roomIndex < generatedRooms.Count; roomIndex++)
            {
                RectInt room = generatedRooms[roomIndex];
                HashSet<Vector3Int> visited = new HashSet<Vector3Int>();

                for (int x = room.xMin; x < room.xMax; x++)
                {
                    for (int y = room.yMin; y < room.yMax; y++)
                    {
                        Vector3Int start = new Vector3Int(x, y, 0);
                        if (floorCells.Contains(start) || visited.Contains(start))
                        {
                            continue;
                        }

                        List<Vector3Int> voidRegion = CollectRoomVoidRegion(room, start, visited, out bool touchesRoomEdge);
                        if (touchesRoomEdge || voidRegion.Count == 0 || voidRegion.Count > maxFillCells)
                        {
                            continue;
                        }

                        for (int i = 0; i < voidRegion.Count; i++)
                        {
                            AddRoomFloorCell(roomIndex, voidRegion[i]);
                        }
                    }
                }
            }
        }

        private List<Vector3Int> CollectRoomVoidRegion(
            RectInt room,
            Vector3Int start,
            HashSet<Vector3Int> visited,
            out bool touchesRoomEdge)
        {
            List<Vector3Int> result = new List<Vector3Int>();
            Queue<Vector3Int> queue = new Queue<Vector3Int>();
            touchesRoomEdge = false;

            visited.Add(start);
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                Vector3Int current = queue.Dequeue();
                result.Add(current);

                if (current.x <= room.xMin || current.x >= room.xMax - 1 || current.y <= room.yMin || current.y >= room.yMax - 1)
                {
                    touchesRoomEdge = true;
                }

                TryVisitRoomVoidNeighbor(room, current + Vector3Int.right, visited, queue);
                TryVisitRoomVoidNeighbor(room, current + Vector3Int.left, visited, queue);
                TryVisitRoomVoidNeighbor(room, current + Vector3Int.up, visited, queue);
                TryVisitRoomVoidNeighbor(room, current + Vector3Int.down, visited, queue);
            }

            return result;
        }

        private void TryVisitRoomVoidNeighbor(RectInt room, Vector3Int cell, HashSet<Vector3Int> visited, Queue<Vector3Int> queue)
        {
            if (!room.Contains(new Vector2Int(cell.x, cell.y)) || floorCells.Contains(cell) || !visited.Add(cell))
            {
                return;
            }

            queue.Enqueue(cell);
        }

        private static HashSet<Vector3Int> FloodFillRoomCells(Vector3Int start, HashSet<Vector3Int> roomSet)
        {
            HashSet<Vector3Int> reachable = new HashSet<Vector3Int>();
            Queue<Vector3Int> queue = new Queue<Vector3Int>();
            if (!roomSet.Contains(start))
            {
                return reachable;
            }

            reachable.Add(start);
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                Vector3Int current = queue.Dequeue();
                TryVisitRoomNeighbor(current + Vector3Int.right, roomSet, reachable, queue);
                TryVisitRoomNeighbor(current + Vector3Int.left, roomSet, reachable, queue);
                TryVisitRoomNeighbor(current + Vector3Int.up, roomSet, reachable, queue);
                TryVisitRoomNeighbor(current + Vector3Int.down, roomSet, reachable, queue);
            }

            return reachable;
        }

        private static void TryVisitRoomNeighbor(
            Vector3Int cell,
            HashSet<Vector3Int> roomSet,
            HashSet<Vector3Int> reachable,
            Queue<Vector3Int> queue)
        {
            if (!roomSet.Contains(cell) || !reachable.Add(cell))
            {
                return;
            }

            queue.Enqueue(cell);
        }

        private static Vector3Int FindNearestCell(Vector3Int target, HashSet<Vector3Int> candidates)
        {
            bool found = false;
            int bestDistance = int.MaxValue;
            Vector3Int bestCell = target;

            foreach (Vector3Int candidate in candidates)
            {
                int distance = Mathf.Abs(candidate.x - target.x) + Mathf.Abs(candidate.y - target.y);
                if (!found || distance < bestDistance)
                {
                    found = true;
                    bestDistance = distance;
                    bestCell = candidate;
                }
            }

            return bestCell;
        }

        private void CarveRoomPath(int roomIndex, Vector3Int from, Vector3Int to)
        {
            Vector3Int current = from;
            AddRoomFloorPatch(roomIndex, current, corridorWidth);

            int xStep = current.x <= to.x ? 1 : -1;
            while (current.x != to.x)
            {
                current.x += xStep;
                AddRoomFloorPatch(roomIndex, current, corridorWidth);
            }

            int yStep = current.y <= to.y ? 1 : -1;
            while (current.y != to.y)
            {
                current.y += yStep;
                AddRoomFloorPatch(roomIndex, current, corridorWidth);
            }
        }

        private void AddRoomFloorPatch(int roomIndex, Vector3Int center, int width)
        {
            EnsureRoomFloorCellList(roomIndex);
            GetCenteredWidthOffsets(width, out int minOffset, out int maxOffset);

            for (int x = minOffset; x <= maxOffset; x++)
            {
                for (int y = minOffset; y <= maxOffset; y++)
                {
                    Vector3Int cell = new Vector3Int(center.x + x, center.y + y, 0);
                    AddRoomFloorCell(roomIndex, cell);
                }
            }
        }

        private void AddRoomFloorCell(int roomIndex, Vector3Int cell)
        {
            EnsureRoomFloorCellList(roomIndex);
            floorCells.Add(cell);

            List<Vector3Int> roomCells = roomFloorCells[roomIndex];
            if (!roomCells.Contains(cell))
            {
                roomCells.Add(cell);
            }
        }

        private void StampRoom(int roomIndex, RectInt room)
        {
            EnsureRoomFloorCellList(roomIndex);
            List<Vector3Int> roomCells = roomFloorCells[roomIndex];
            roomCells.Clear();

            bool keepRectangular = (keepBossRoomRectangular && IsBossRoomIndex(roomIndex))
                || (keepTutorialRoomRectangular && IsTutorialRoomIndex(roomIndex));
            RoomShapeMask shape = RoomShapeMask.Create(
                room,
                !keepRectangular && varyRoomShapes,
                !keepRectangular && useStructuredRoomShapes,
                keepRectangular ? 0f : structuredRoomChance,
                structureArmMinWidth,
                structureOuterPadding,
                !keepRectangular && allowEdgeCutsOnStructuredRooms,
                keepRectangular ? 0f : cornerCutChance,
                cornerCutSize,
                keepRectangular ? 0f : edgeCutChance,
                keepRectangular ? 0 : maxEdgeCutsPerRoom,
                edgeCutDepthRange,
                edgeCutLengthRange);
            for (int x = room.xMin; x < room.xMax; x++)
            {
                for (int y = room.yMin; y < room.yMax; y++)
                {
                    if (!shape.Contains(x, y))
                    {
                        continue;
                    }

                    Vector3Int cell = new Vector3Int(x, y, 0);
                    floorCells.Add(cell);
                    roomCells.Add(cell);
                }
            }
        }

        private void EnsureRoomFloorCellList(int roomIndex)
        {
            while (roomFloorCells.Count <= roomIndex)
            {
                roomFloorCells.Add(new List<Vector3Int>());
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
            GetCenteredWidthOffsets(corridorWidth, out int minOffset, out int maxOffset);
            int minX = Mathf.Min(from.x, to.x);
            int maxX = Mathf.Max(from.x, to.x);
            int minY = Mathf.Min(from.y, to.y);
            int maxY = Mathf.Max(from.y, to.y);

            for (int x = minX + minOffset; x <= maxX + maxOffset; x++)
            {
                for (int y = minY + minOffset; y <= maxY + maxOffset; y++)
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
            Vector2Int center = GetDoorCenter(room, roomCenter, direction) + direction;
            int thickness = Mathf.Max(1, doorThickness);
            GetDoorWidthOffsets(center, direction, thickness, out int minOffset, out int maxOffset);
            List<Vector3Int> cells = new List<Vector3Int>();

            for (int widthOffset = minOffset; widthOffset <= maxOffset; widthOffset++)
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
            doorDirections.Add(direction);
            doorCenters.Add(center);
            doorWidthOffsetRanges.Add(new Vector2Int(minOffset, maxOffset));
            templateDoors.Add(null);
            doorWorldCenters.Add(Vector3.positiveInfinity);
            return doorCellGroups.Count - 1;
        }

        private void GetDoorWidthOffsets(Vector2Int center, Vector2Int direction, int thickness, out int minOffset, out int maxOffset)
        {
            GetCenteredWidthOffsets(corridorWidth, out minOffset, out maxOffset);
            if (fitDoorWidthToCorridor
                && TryGetDoorwayPassageWidthOffsets(center, direction, out int corridorMinOffset, out int corridorMaxOffset))
            {
                minOffset = corridorMinOffset;
                maxOffset = corridorMaxOffset;
            }
        }

        private bool TryGetDoorwayPassageWidthOffsets(Vector2Int center, Vector2Int direction, out int minOffset, out int maxOffset)
        {
            minOffset = 0;
            maxOffset = 0;
            if (direction == Vector2Int.zero)
            {
                return false;
            }

            Vector2Int perpendicular = direction.x != 0 ? Vector2Int.up : Vector2Int.right;
            int probeDepth = Mathf.Max(doorWidthProbeDepth, doorThickness + 2);
            int maxScanDistance = Mathf.Max(corridorWidth + doorBlockerSidePaddingCells * 2 + 8, 12);
            bool found = false;
            bool inRun = false;
            int runStart = 0;
            int bestMin = 0;
            int bestMax = 0;
            int bestDistance = int.MaxValue;

            for (int offset = -maxScanDistance; offset <= maxScanDistance; offset++)
            {
                bool passable = HasDoorwayPassageAtOffset(center, direction, perpendicular, probeDepth, offset);
                if (passable && !inRun)
                {
                    inRun = true;
                    runStart = offset;
                }

                bool closesRun = inRun && (!passable || offset == maxScanDistance);
                if (!closesRun)
                {
                    continue;
                }

                int runEnd = passable && offset == maxScanDistance ? offset : offset - 1;
                int distanceToZero = runStart > 0 ? runStart : runEnd < 0 ? -runEnd : 0;
                if (!found || distanceToZero < bestDistance)
                {
                    found = true;
                    bestDistance = distanceToZero;
                    bestMin = runStart;
                    bestMax = runEnd;
                }

                inRun = false;
            }

            if (!found)
            {
                return false;
            }

            minOffset = bestMin;
            maxOffset = bestMax;
            return true;
        }

        private bool HasDoorwayPassageAtOffset(
            Vector2Int center,
            Vector2Int direction,
            Vector2Int perpendicular,
            int probeDepth,
            int widthOffset)
        {
            Vector2Int baseCell = center + perpendicular * widthOffset;
            bool hasRoomSideFloor = false;
            bool hasCorridorSideFloor = false;

            for (int depth = 1; depth <= probeDepth; depth++)
            {
                if (!hasRoomSideFloor && HasFloor(baseCell - direction * depth))
                {
                    hasRoomSideFloor = true;
                }

                if (!hasCorridorSideFloor && HasFloor(baseCell + direction * (depth - 1)))
                {
                    hasCorridorSideFloor = true;
                }

                if (hasRoomSideFloor && hasCorridorSideFloor)
                {
                    return true;
                }
            }

            return hasRoomSideFloor && hasCorridorSideFloor;
        }

        private static void GetCenteredWidthOffsets(int width, out int minOffset, out int maxOffset)
        {
            int safeWidth = Mathf.Max(1, width);
            minOffset = -((safeWidth - 1) / 2);
            maxOffset = safeWidth / 2;
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
                if (randomizeFloorTileRotation)
                {
                    floorTilemap.SetTransformMatrix(cell, GetRandomCardinalTileTransform());
                }

                wallTilemap.SetTile(cell, null);
                if (wallCollisionTilemap != null)
                {
                    wallCollisionTilemap.SetTile(cell, null);
                }
            }

            foreach (Vector3Int cell in wallCells)
            {
                if (!floorCells.Contains(cell))
                {
                    if (useDirectionalOutlineTiles)
                    {
                        if (TryGetOutlineDirection(cell, out DungeonTileSet.OutlineDirection direction))
                        {
                            TileBase outlineTile = tileSet.GetRandomOutlineTile(direction);
                            bool showOutlineTile = !ShouldSuppressVisibleOutlineTile(cell, direction);

                            if (showOutlineTile)
                            {
                                wallTilemap.SetTile(cell, outlineTile);
                            }

                            if (ShouldCreateWallCollision(direction))
                            {
                                SetWallCollisionTile(cell, outlineTile, direction);
                                SealCornerCollisionGap(cell, outlineTile, direction);
                            }
                        }

                        continue;
                    }

                    TileBase wallTile = tileSet.GetRandomWallTile();
                    wallTilemap.SetTile(cell, wallTile);
                    SetWallCollisionTile(cell, wallTile);
                }
            }

            PostProcessWallCollisionCorners();
            BuildBoundaryWallColliders();
        }

        private void SetWallCollisionTile(Vector3Int cell, TileBase tile)
        {
            SetWallCollisionTile(cell, tile, null);
        }

        private void SetWallCollisionTile(Vector3Int cell, TileBase tile, DungeonTileSet.OutlineDirection? direction)
        {
            if (!addWallCollider || wallCollisionTilemap == null || useBoundaryBoxWallColliders)
            {
                return;
            }

            TileBase collisionTile = tile != null ? tile : tileSet.GetRandomWallTile();
            if (collisionTile == null)
            {
                collisionTile = tileSet.GetRandomOutlineTile(DungeonTileSet.OutlineDirection.North);
            }

            Vector3Int collisionCell = cell;
            if (direction.HasValue)
            {
                DungeonTileSet.OutlineDirection outlineDirection = direction.Value;
                if (IsAnyCornerDirection(outlineDirection))
                {
                    if (TryGetReentrantCornerOpenDirections(cell, out _, out _))
                    {
                        // Reentrant corners block the actual corner cell. Straight-wall offset
                        // cells around this corner are pruned in SealCornerCollisionGap.
                        collisionCell = cell;
                    }
                    else if (TryGetConvexCornerOutwardDirection(cell, out Vector2Int convexOutward))
                    {
                        if (!sealCornerCollisionGaps && !collideOuterCornerTiles)
                        {
                            return;
                        }

                        int offset = Mathf.Max(0, wallCollisionOutwardOffset);
                        collisionCell = new Vector3Int(
                            cell.x + convexOutward.x * offset,
                            cell.y + convexOutward.y * offset,
                            cell.z);
                    }
                    else
                    {
                        collisionCell = cell;
                    }
                }
                else
                {
                    collisionCell = GetOffsetWallCollisionCell(cell, outlineDirection);
                }
            }

            if (floorCells.Contains(collisionCell) || IsDoorCell(collisionCell))
            {
                return;
            }

            wallCollisionTilemap.SetTile(collisionCell, collisionTile);
        }

        private void SealCornerCollisionGap(Vector3Int cell, TileBase tile, DungeonTileSet.OutlineDirection direction)
        {
            if (!sealCornerCollisionGaps || !IsAnyCornerDirection(direction))
            {
                return;
            }

            if (!TryGetReentrantCornerOpenDirections(cell, out Vector2Int openA, out Vector2Int openB))
            {
                return;
            }

            TileBase collisionTile = tile != null ? tile : tileSet.GetRandomWallTile();
            if (collisionTile == null)
            {
                collisionTile = tileSet.GetRandomOutlineTile(DungeonTileSet.OutlineDirection.North);
            }

            // Inner/reentrant corners need a blocker on the visible corner cell itself.
            // Side-arm pruning is done after all walls are stamped, otherwise a later straight
            // wall can recreate the protruding cell.
            TrySetWallCollisionCell(cell, collisionTile);
        }

        private void PostProcessWallCollisionCorners()
        {
            if (!addWallCollider || wallCollisionTilemap == null || !sealCornerCollisionGaps || useBoundaryBoxWallColliders)
            {
                return;
            }

            TileBase collisionTile = tileSet != null ? tileSet.GetRandomWallTile() : null;
            if (collisionTile == null && tileSet != null)
            {
                collisionTile = tileSet.GetRandomOutlineTile(DungeonTileSet.OutlineDirection.North);
            }

            if (collisionTile == null)
            {
                return;
            }

            List<ReentrantCornerCollision> corners = new List<ReentrantCornerCollision>();
            foreach (Vector3Int cell in wallCells)
            {
                if (floorCells.Contains(cell) || IsDoorCell(cell))
                {
                    continue;
                }

                if (!TryGetReentrantCornerOpenDirections(cell, out Vector2Int openA, out Vector2Int openB))
                {
                    continue;
                }

                corners.Add(new ReentrantCornerCollision(cell, openA, openB));
            }

            for (int i = 0; i < corners.Count; i++)
            {
                ReentrantCornerCollision corner = corners[i];
                ClearReentrantCornerSideProtrusions(corner.Cell, corner.OpenA, corner.OpenB);
            }

            for (int i = 0; i < corners.Count; i++)
            {
                TrySetWallCollisionCell(corners[i].Cell, collisionTile);
            }
        }

        private void BuildBoundaryWallColliders()
        {
            if (!addWallCollider || !useBoundaryBoxWallColliders || floorTilemap == null)
            {
                return;
            }

            ClearGeneratedWallBoundaryColliders();

            Dictionary<BoundaryLineKey, List<int>> boundaryLines = new Dictionary<BoundaryLineKey, List<int>>();
            foreach (Vector3Int floor in floorCells)
            {
                if (!HasFloor(floor.x, floor.y + 1))
                {
                    AddBoundaryLineCell(boundaryLines, new BoundaryLineKey(true, floor.y + 1, 1), floor.x);
                }

                if (!HasFloor(floor.x, floor.y - 1))
                {
                    AddBoundaryLineCell(boundaryLines, new BoundaryLineKey(true, floor.y, -1), floor.x);
                }

                if (!HasFloor(floor.x + 1, floor.y))
                {
                    AddBoundaryLineCell(boundaryLines, new BoundaryLineKey(false, floor.x + 1, 1), floor.y);
                }

                if (!HasFloor(floor.x - 1, floor.y))
                {
                    AddBoundaryLineCell(boundaryLines, new BoundaryLineKey(false, floor.x, -1), floor.y);
                }
            }

            Transform parent = GetWallBoundaryColliderRoot();
            foreach (KeyValuePair<BoundaryLineKey, List<int>> line in boundaryLines)
            {
                CreateBoundaryLineColliders(parent, line.Key, line.Value);
            }
        }

        private static void AddBoundaryLineCell(Dictionary<BoundaryLineKey, List<int>> lines, BoundaryLineKey key, int value)
        {
            if (!lines.TryGetValue(key, out List<int> values))
            {
                values = new List<int>();
                lines.Add(key, values);
            }

            values.Add(value);
        }

        private void CreateBoundaryLineColliders(Transform parent, BoundaryLineKey key, List<int> values)
        {
            if (values == null || values.Count == 0)
            {
                return;
            }

            values.Sort();
            int start = values[0];
            int previous = values[0];

            for (int i = 1; i < values.Count; i++)
            {
                int value = values[i];
                if (value <= previous + 1)
                {
                    previous = Mathf.Max(previous, value);
                    continue;
                }

                CreateBoundaryCollider(parent, key, start, previous + 1);
                start = value;
                previous = value;
            }

            CreateBoundaryCollider(parent, key, start, previous + 1);
        }

        private void CreateBoundaryCollider(Transform parent, BoundaryLineKey key, int start, int end)
        {
            if (end <= start)
            {
                return;
            }

            float thickness = Mathf.Max(0.02f, wallBoundaryColliderThickness);
            float outwardOffset = Mathf.Max(0f, wallBoundaryColliderOutwardOffsetCells);
            GetBoundaryColliderEndpointExtension(key, start, end, outwardOffset, out float startExtension, out float endExtension);
            Vector2 centerCells;
            Vector2 sizeCells;

            if (key.Horizontal)
            {
                float extendedStart = start - startExtension;
                float extendedEnd = end + endExtension;
                if (extendedEnd <= extendedStart)
                {
                    return;
                }

                centerCells = new Vector2((extendedStart + extendedEnd) * 0.5f, key.FixedLine + key.OutwardSign * (outwardOffset + thickness * 0.5f));
                sizeCells = new Vector2(extendedEnd - extendedStart, thickness);
            }
            else
            {
                float extendedStart = start - startExtension;
                float extendedEnd = end + endExtension;
                if (extendedEnd <= extendedStart)
                {
                    return;
                }

                centerCells = new Vector2(key.FixedLine + key.OutwardSign * (outwardOffset + thickness * 0.5f), (extendedStart + extendedEnd) * 0.5f);
                sizeCells = new Vector2(thickness, extendedEnd - extendedStart);
            }

            Vector3 worldCenter = GetCellSpaceWorldPosition(centerCells);
            Vector2 worldSize = GetCellSpaceWorldSize(sizeCells);

            GameObject colliderObject = new GameObject(key.Horizontal ? "Wall Boundary Horizontal" : "Wall Boundary Vertical");
            colliderObject.layer = GetDoorCollisionLayer();
            colliderObject.transform.SetParent(parent, true);
            colliderObject.transform.position = worldCenter;

            BoxCollider2D collider = colliderObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = false;
            collider.size = worldSize;
            wallBoundaryColliders.Add(collider);
        }

        private void GetBoundaryColliderEndpointExtension(
            BoundaryLineKey key,
            int start,
            int end,
            float outwardOffset,
            out float startExtension,
            out float endExtension)
        {
            startExtension = 0f;
            endExtension = 0f;
            if (outwardOffset <= 0f)
            {
                return;
            }

            if (key.Horizontal)
            {
                startExtension = GetBoundaryCornerEndpointAdjustment(start, key.FixedLine, outwardOffset);
                endExtension = GetBoundaryCornerEndpointAdjustment(end, key.FixedLine, outwardOffset);
                return;
            }

            startExtension = GetBoundaryCornerEndpointAdjustment(key.FixedLine, start, outwardOffset);
            endExtension = GetBoundaryCornerEndpointAdjustment(key.FixedLine, end, outwardOffset);
        }

        private float GetBoundaryCornerEndpointAdjustment(int vertexX, int vertexY, float outwardOffset)
        {
            int floorCount = 0;
            if (HasFloor(vertexX - 1, vertexY - 1))
            {
                floorCount++;
            }

            if (HasFloor(vertexX, vertexY - 1))
            {
                floorCount++;
            }

            if (HasFloor(vertexX - 1, vertexY))
            {
                floorCount++;
            }

            if (HasFloor(vertexX, vertexY))
            {
                floorCount++;
            }

            if (floorCount == 1)
            {
                return outwardOffset;
            }

            if (floorCount == 3)
            {
                return -outwardOffset;
            }

            return 0f;
        }

        private Vector3 GetCellSpaceWorldPosition(Vector2 cellPosition)
        {
            Vector3 origin = floorTilemap.CellToWorld(Vector3Int.zero);
            Vector3 right = floorTilemap.CellToWorld(Vector3Int.right) - origin;
            Vector3 up = floorTilemap.CellToWorld(Vector3Int.up) - origin;
            return origin + right * cellPosition.x + up * cellPosition.y;
        }

        private Vector2 GetCellSpaceWorldSize(Vector2 cellSize)
        {
            Vector3 origin = floorTilemap.CellToWorld(Vector3Int.zero);
            float xSize = (floorTilemap.CellToWorld(Vector3Int.right) - origin).magnitude;
            float ySize = (floorTilemap.CellToWorld(Vector3Int.up) - origin).magnitude;
            return new Vector2(Mathf.Abs(cellSize.x) * xSize, Mathf.Abs(cellSize.y) * ySize);
        }

        private void ClearReentrantCornerSideProtrusions(Vector3Int cell, Vector2Int openA, Vector2Int openB)
        {
            if (wallCollisionTilemap == null)
            {
                return;
            }

            int offset = Mathf.Max(0, wallCollisionOutwardOffset);
            for (int step = 1; step <= offset; step++)
            {
                ClearWallCollisionCell(new Vector3Int(cell.x + openA.x * step, cell.y + openA.y * step, cell.z));
                ClearWallCollisionCell(new Vector3Int(cell.x + openB.x * step, cell.y + openB.y * step, cell.z));
            }
        }

        private void ClearWallCollisionCell(Vector3Int cell)
        {
            if (wallCollisionTilemap == null || floorCells.Contains(cell) || IsDoorCell(cell))
            {
                return;
            }

            wallCollisionTilemap.SetTile(cell, null);
        }

        private void TrySetWallCollisionCell(Vector3Int cell, TileBase tile)
        {
            if (wallCollisionTilemap == null || floorCells.Contains(cell) || IsDoorCell(cell))
            {
                return;
            }

            wallCollisionTilemap.SetTile(cell, tile);
        }

        private Vector3Int GetOffsetWallCollisionCell(Vector3Int cell, DungeonTileSet.OutlineDirection direction)
        {
            int offset = Mathf.Max(0, wallCollisionOutwardOffset);
            if (offset == 0)
            {
                return cell;
            }

            Vector2Int outward = GetOutlineOutwardDirection(direction);
            return new Vector3Int(
                cell.x + outward.x * offset,
                cell.y + outward.y * offset,
                cell.z);
        }

        private static Vector2Int GetOutlineOutwardDirection(DungeonTileSet.OutlineDirection direction)
        {
            switch (direction)
            {
                case DungeonTileSet.OutlineDirection.North:
                    return Vector2Int.up;
                case DungeonTileSet.OutlineDirection.South:
                    return Vector2Int.down;
                case DungeonTileSet.OutlineDirection.East:
                    return Vector2Int.right;
                case DungeonTileSet.OutlineDirection.West:
                    return Vector2Int.left;
                case DungeonTileSet.OutlineDirection.NorthEast:
                case DungeonTileSet.OutlineDirection.OuterNorthEast:
                    return Vector2Int.up + Vector2Int.right;
                case DungeonTileSet.OutlineDirection.NorthWest:
                case DungeonTileSet.OutlineDirection.OuterNorthWest:
                    return Vector2Int.up + Vector2Int.left;
                case DungeonTileSet.OutlineDirection.SouthEast:
                case DungeonTileSet.OutlineDirection.OuterSouthEast:
                    return Vector2Int.down + Vector2Int.right;
                case DungeonTileSet.OutlineDirection.SouthWest:
                case DungeonTileSet.OutlineDirection.OuterSouthWest:
                    return Vector2Int.down + Vector2Int.left;
                default:
                    return Vector2Int.zero;
            }
        }

        private bool TryGetReentrantCornerOpenDirections(Vector3Int cell, out Vector2Int openA, out Vector2Int openB)
        {
            bool floorNorth = HasFloor(cell.x, cell.y + 1);
            bool floorSouth = HasFloor(cell.x, cell.y - 1);
            bool floorEast = HasFloor(cell.x + 1, cell.y);
            bool floorWest = HasFloor(cell.x - 1, cell.y);

            if (floorNorth && floorWest)
            {
                openA = Vector2Int.down;
                openB = Vector2Int.right;
                return true;
            }

            if (floorNorth && floorEast)
            {
                openA = Vector2Int.down;
                openB = Vector2Int.left;
                return true;
            }

            if (floorSouth && floorWest)
            {
                openA = Vector2Int.up;
                openB = Vector2Int.right;
                return true;
            }

            if (floorSouth && floorEast)
            {
                openA = Vector2Int.up;
                openB = Vector2Int.left;
                return true;
            }

            openA = Vector2Int.zero;
            openB = Vector2Int.zero;
            return false;
        }

        private bool TryGetConvexCornerOutwardDirection(Vector3Int cell, out Vector2Int outward)
        {
            bool floorNorth = HasFloor(cell.x, cell.y + 1);
            bool floorSouth = HasFloor(cell.x, cell.y - 1);
            bool floorEast = HasFloor(cell.x + 1, cell.y);
            bool floorWest = HasFloor(cell.x - 1, cell.y);

            if (floorNorth || floorSouth || floorEast || floorWest)
            {
                outward = Vector2Int.zero;
                return false;
            }

            bool floorNorthEast = HasFloor(cell.x + 1, cell.y + 1);
            bool floorNorthWest = HasFloor(cell.x - 1, cell.y + 1);
            bool floorSouthEast = HasFloor(cell.x + 1, cell.y - 1);
            bool floorSouthWest = HasFloor(cell.x - 1, cell.y - 1);

            if (floorNorthEast)
            {
                outward = Vector2Int.down + Vector2Int.left;
                return true;
            }

            if (floorNorthWest)
            {
                outward = Vector2Int.down + Vector2Int.right;
                return true;
            }

            if (floorSouthEast)
            {
                outward = Vector2Int.up + Vector2Int.left;
                return true;
            }

            if (floorSouthWest)
            {
                outward = Vector2Int.up + Vector2Int.right;
                return true;
            }

            outward = Vector2Int.zero;
            return false;
        }

        private bool ShouldSuppressVisibleOutlineTile(Vector3Int cell, DungeonTileSet.OutlineDirection direction)
        {
            return suppressDoorwayOuterCornerTiles
                && IsLogicalOuterCornerDirection(direction)
                && IsNearDoorCell(cell);
        }

        private bool ShouldCreateWallCollision(DungeonTileSet.OutlineDirection direction)
        {
            if (IsAnyCornerDirection(direction))
            {
                return sealCornerCollisionGaps || collideOuterCornerTiles;
            }

            return true;
        }

        private static bool IsAnyCornerDirection(DungeonTileSet.OutlineDirection direction)
        {
            return IsInnerCornerDirection(direction) || IsOuterCornerDirection(direction);
        }

        private bool IsLogicalOuterCornerDirection(DungeonTileSet.OutlineDirection direction)
        {
            return swapInnerOuterCornerTiles
                ? IsInnerCornerDirection(direction)
                : IsOuterCornerDirection(direction);
        }

        private static bool IsOuterCornerDirection(DungeonTileSet.OutlineDirection direction)
        {
            return direction == DungeonTileSet.OutlineDirection.OuterNorthEast
                || direction == DungeonTileSet.OutlineDirection.OuterNorthWest
                || direction == DungeonTileSet.OutlineDirection.OuterSouthEast
                || direction == DungeonTileSet.OutlineDirection.OuterSouthWest;
        }

        private static bool IsInnerCornerDirection(DungeonTileSet.OutlineDirection direction)
        {
            return direction == DungeonTileSet.OutlineDirection.NorthEast
                || direction == DungeonTileSet.OutlineDirection.NorthWest
                || direction == DungeonTileSet.OutlineDirection.SouthEast
                || direction == DungeonTileSet.OutlineDirection.SouthWest;
        }

        private bool IsNearDoorCell(Vector3Int cell)
        {
            for (int groupIndex = 0; groupIndex < doorCellGroups.Count; groupIndex++)
            {
                List<Vector3Int> doorCells = doorCellGroups[groupIndex];
                if (doorCells == null)
                {
                    continue;
                }

                for (int cellIndex = 0; cellIndex < doorCells.Count; cellIndex++)
                {
                    Vector3Int doorCell = doorCells[cellIndex];
                    if (Mathf.Abs(cell.x - doorCell.x) <= 1 && Mathf.Abs(cell.y - doorCell.y) <= 1)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsDoorCell(Vector3Int cell)
        {
            for (int groupIndex = 0; groupIndex < doorCellGroups.Count; groupIndex++)
            {
                List<Vector3Int> doorCells = doorCellGroups[groupIndex];
                if (doorCells == null)
                {
                    continue;
                }

                for (int cellIndex = 0; cellIndex < doorCells.Count; cellIndex++)
                {
                    if (doorCells[cellIndex] == cell)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryGetOutlineDirection(Vector3Int cell, out DungeonTileSet.OutlineDirection direction)
        {
            bool floorNorth = HasFloor(cell.x, cell.y + 1);
            bool floorSouth = HasFloor(cell.x, cell.y - 1);
            bool floorEast = HasFloor(cell.x + 1, cell.y);
            bool floorWest = HasFloor(cell.x - 1, cell.y);

            // Inner corners touch the playable floor on two cardinal sides.
            if (floorSouth && floorWest)
            {
                direction = GetCornerOutlineDirection(DungeonTileSet.OutlineDirection.NorthEast);
                return true;
            }

            if (floorSouth && floorEast)
            {
                direction = GetCornerOutlineDirection(DungeonTileSet.OutlineDirection.NorthWest);
                return true;
            }

            if (floorNorth && floorWest)
            {
                direction = GetCornerOutlineDirection(DungeonTileSet.OutlineDirection.SouthEast);
                return true;
            }

            if (floorNorth && floorEast)
            {
                direction = GetCornerOutlineDirection(DungeonTileSet.OutlineDirection.SouthWest);
                return true;
            }

            if (floorSouth)
            {
                direction = DungeonTileSet.OutlineDirection.North;
                return true;
            }

            if (floorNorth)
            {
                direction = DungeonTileSet.OutlineDirection.South;
                return true;
            }

            if (floorWest)
            {
                direction = DungeonTileSet.OutlineDirection.East;
                return true;
            }

            if (floorEast)
            {
                direction = DungeonTileSet.OutlineDirection.West;
                return true;
            }

            bool floorSouthWest = HasFloor(cell.x - 1, cell.y - 1);
            bool floorSouthEast = HasFloor(cell.x + 1, cell.y - 1);
            bool floorNorthWest = HasFloor(cell.x - 1, cell.y + 1);
            bool floorNorthEast = HasFloor(cell.x + 1, cell.y + 1);

            // Outer corners only see the playable floor diagonally, so they are visual caps.
            if (floorSouthWest)
            {
                direction = GetCornerOutlineDirection(DungeonTileSet.OutlineDirection.OuterNorthEast);
                return true;
            }

            if (floorSouthEast)
            {
                direction = GetCornerOutlineDirection(DungeonTileSet.OutlineDirection.OuterNorthWest);
                return true;
            }

            if (floorNorthWest)
            {
                direction = GetCornerOutlineDirection(DungeonTileSet.OutlineDirection.OuterSouthEast);
                return true;
            }

            if (floorNorthEast)
            {
                direction = GetCornerOutlineDirection(DungeonTileSet.OutlineDirection.OuterSouthWest);
                return true;
            }

            direction = DungeonTileSet.OutlineDirection.North;
            return false;
        }

        private DungeonTileSet.OutlineDirection GetCornerOutlineDirection(DungeonTileSet.OutlineDirection direction)
        {
            if (!swapInnerOuterCornerTiles)
            {
                return direction;
            }

            switch (direction)
            {
                case DungeonTileSet.OutlineDirection.NorthEast:
                    return DungeonTileSet.OutlineDirection.OuterNorthEast;
                case DungeonTileSet.OutlineDirection.NorthWest:
                    return DungeonTileSet.OutlineDirection.OuterNorthWest;
                case DungeonTileSet.OutlineDirection.SouthEast:
                    return DungeonTileSet.OutlineDirection.OuterSouthEast;
                case DungeonTileSet.OutlineDirection.SouthWest:
                    return DungeonTileSet.OutlineDirection.OuterSouthWest;
                case DungeonTileSet.OutlineDirection.OuterNorthEast:
                    return DungeonTileSet.OutlineDirection.NorthEast;
                case DungeonTileSet.OutlineDirection.OuterNorthWest:
                    return DungeonTileSet.OutlineDirection.NorthWest;
                case DungeonTileSet.OutlineDirection.OuterSouthEast:
                    return DungeonTileSet.OutlineDirection.SouthEast;
                case DungeonTileSet.OutlineDirection.OuterSouthWest:
                    return DungeonTileSet.OutlineDirection.SouthWest;
                default:
                    return direction;
            }
        }

        private bool HasFloor(int x, int y)
        {
            return floorCells.Contains(new Vector3Int(x, y, 0));
        }

        private bool HasFloor(Vector2Int cell)
        {
            return HasFloor(cell.x, cell.y);
        }

        private void ApplyRoomDecorations()
        {
            if (!decorateRooms || decorationTilemap == null || tileSet == null || roomDecorationChance <= 0f)
            {
                return;
            }

            int padding = Mathf.Max(0, decorationRoomPadding);
            int centerSafeRadius = Mathf.Max(0, decorationCenterSafeRadius);
            int centerSafeRadiusSqr = centerSafeRadius * centerSafeRadius;

            for (int roomIndex = 0; roomIndex < generatedRooms.Count; roomIndex++)
            {
                RectInt room = generatedRooms[roomIndex];
                Vector2 center = room.center;

                for (int x = room.xMin + padding; x < room.xMax - padding; x++)
                {
                    for (int y = room.yMin + padding; y < room.yMax - padding; y++)
                    {
                        Vector3Int cell = new Vector3Int(x, y, 0);
                        if (!floorCells.Contains(cell) || wallCells.Contains(cell))
                        {
                            continue;
                        }

                        if (centerSafeRadius > 0)
                        {
                            Vector2 delta = new Vector2(x + 0.5f, y + 0.5f) - center;
                            if (delta.sqrMagnitude < centerSafeRadiusSqr)
                            {
                                continue;
                            }
                        }

                        if (UnityEngine.Random.value > roomDecorationChance)
                        {
                            continue;
                        }

                        TileBase decorationTile = tileSet.GetRandomDecorationTile();
                        if (decorationTile != null)
                        {
                            decorationTilemap.SetTile(cell, decorationTile);
                            if (randomizeDecorationTileRotation)
                            {
                                decorationTilemap.SetTransformMatrix(cell, GetRandomCardinalTileTransform());
                            }
                        }
                    }
                }
            }
        }

        private static Matrix4x4 GetRandomCardinalTileTransform()
        {
            int quarterTurns = UnityEngine.Random.Range(0, 4);
            if (quarterTurns == 0)
            {
                return Matrix4x4.identity;
            }

            return Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, quarterTurns * 90f));
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

                UpdateDoorWidthRangeFromCurrentFloor(i);
                Tilemap doorTilemap = CreateTilemap(parent, $"Door Tilemap {i}", 3);
                doorTilemap.gameObject.layer = GetDoorCollisionLayer();
                TilemapCollider2D doorCollider = doorTilemap.gameObject.AddComponent<TilemapCollider2D>();
                doorCollider.enabled = useDoorTilemapCollider;
                BoxCollider2D doorBlocker = CreateDoorBlocker(parent, i, cells);
                Matrix4x4 doorTransform = GetDoorTileTransform(i);
                List<Vector3Int> visualCells = GetDoorVisualCells(i, cells);

                for (int cellIndex = 0; cellIndex < visualCells.Count; cellIndex++)
                {
                    Vector3Int cell = visualCells[cellIndex];
                    doorTilemap.SetTile(cell, tileSet.GetRandomDoorTile());
                    doorTilemap.SetTransformMatrix(cell, doorTransform);
                }

                doorTilemaps.Add(doorTilemap);
                doorColliders.Add(doorCollider);
                doorBlockers.Add(doorBlocker);
            }
        }

        private void UpdateDoorWidthRangeFromCurrentFloor(int doorIndex)
        {
            if (doorIndex < 0
                || doorIndex >= doorCenters.Count
                || doorIndex >= doorDirections.Count
                || doorIndex >= doorWidthOffsetRanges.Count)
            {
                return;
            }

            Vector2Int center = doorCenters[doorIndex];
            Vector2Int direction = doorDirections[doorIndex];
            if (TryGetDoorwayPassageWidthOffsets(center, direction, out int minOffset, out int maxOffset))
            {
                doorWidthOffsetRanges[doorIndex] = new Vector2Int(minOffset, maxOffset);
            }
        }

        private List<Vector3Int> GetDoorVisualCells(int doorIndex, List<Vector3Int> fallbackCells)
        {
            if (doorIndex < 0
                || doorIndex >= doorCenters.Count
                || doorIndex >= doorDirections.Count
                || doorIndex >= doorWidthOffsetRanges.Count)
            {
                return fallbackCells;
            }

            Vector2Int direction = doorDirections[doorIndex];
            if (direction == Vector2Int.zero)
            {
                return fallbackCells;
            }

            Vector2Int center = doorCenters[doorIndex];
            Vector2Int widthRange = doorWidthOffsetRanges[doorIndex];
            int thickness = Mathf.Max(1, doorThickness);
            int minOffset = widthRange.x;
            int maxOffset = widthRange.y;
            List<Vector3Int> visualCells = new List<Vector3Int>();

            for (int widthOffset = minOffset; widthOffset <= maxOffset; widthOffset++)
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

                    visualCells.Add(new Vector3Int(cell.x, cell.y, 0));
                }
            }

            return visualCells;
        }

        private Matrix4x4 GetDoorTileTransform(int doorIndex)
        {
            if (!rotateDoorTilesByDirection || doorIndex < 0 || doorIndex >= doorDirections.Count)
            {
                return Matrix4x4.identity;
            }

            Vector2Int direction = doorDirections[doorIndex];
            if (direction == Vector2Int.zero)
            {
                return Matrix4x4.identity;
            }

            Vector2 defaultDirection = GetDoorDefaultDirection();
            Vector2 targetDirection = new Vector2(direction.x, direction.y);
            float angle = Vector2.SignedAngle(defaultDirection, targetDirection);
            return Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, angle));
        }

        private int GetDoorCollisionLayer()
        {
            if (wallCollisionTilemap != null)
            {
                return wallCollisionTilemap.gameObject.layer;
            }

            if (wallTilemap != null)
            {
                return wallTilemap.gameObject.layer;
            }

            return gameObject.layer;
        }

        private Vector2 GetDoorDefaultDirection()
        {
            switch (doorSpriteDefaultFacing)
            {
                case DoorSpriteFacing.Down:
                    return Vector2.down;
                case DoorSpriteFacing.Left:
                    return Vector2.left;
                case DoorSpriteFacing.Right:
                    return Vector2.right;
                case DoorSpriteFacing.Up:
                default:
                    return Vector2.up;
            }
        }

        private BoxCollider2D CreateDoorBlocker(Transform parent, int doorIndex, List<Vector3Int> cells)
        {
            if (floorTilemap == null || cells == null || cells.Count == 0)
            {
                return null;
            }

            Vector2Int direction = doorIndex >= 0 && doorIndex < doorDirections.Count
                ? doorDirections[doorIndex]
                : Vector2Int.zero;
            Vector3 cellSize = floorTilemap.layoutGrid != null
                ? floorTilemap.layoutGrid.cellSize
                : Vector3.one;
            Bounds blockerBounds = BuildDoorBlockerBounds(doorIndex, cells, direction, cellSize);

            if (direction.x != 0)
            {
                float minHeight = Mathf.Abs(cellSize.y) * Mathf.Max(1, corridorWidth + doorBlockerSidePaddingCells * 2);
                if (blockerBounds.size.y < minHeight)
                {
                    blockerBounds.Expand(new Vector3(0f, minHeight - blockerBounds.size.y, 0f));
                }
            }
            else if (direction.y != 0)
            {
                float minWidth = Mathf.Abs(cellSize.x) * Mathf.Max(1, corridorWidth + doorBlockerSidePaddingCells * 2);
                if (blockerBounds.size.x < minWidth)
                {
                    blockerBounds.Expand(new Vector3(minWidth - blockerBounds.size.x, 0f, 0f));
                }
            }

            GameObject blockerObject = new GameObject($"Door Blocker {doorIndex}");
            blockerObject.layer = GetDoorCollisionLayer();
            blockerObject.transform.SetParent(parent, true);
            blockerObject.transform.position = blockerBounds.center;

            BoxCollider2D blocker = blockerObject.AddComponent<BoxCollider2D>();
            blocker.isTrigger = false;
            blocker.size = new Vector2(blockerBounds.size.x, blockerBounds.size.y);

            return blocker;
        }

        private Bounds BuildDoorBlockerBounds(int doorIndex, List<Vector3Int> cells, Vector2Int direction, Vector3 cellSize)
        {
            if (direction != Vector2Int.zero
                && doorIndex >= 0
                && doorIndex < doorCenters.Count
                && doorIndex < doorWidthOffsetRanges.Count)
            {
                Vector2Int center = doorCenters[doorIndex];
                Vector2Int widthRange = doorWidthOffsetRanges[doorIndex];
                int thickness = Mathf.Max(1, doorThickness);
                float sidePadding = Mathf.Max(0, doorBlockerSidePaddingCells);
                float depthPadding = Mathf.Max(0f, doorBlockerDepthPaddingCells);

                if (direction.x != 0)
                {
                    float xMinCell = direction.x > 0 ? center.x - depthPadding : center.x - thickness - depthPadding + 1f;
                    float xMaxCell = direction.x > 0 ? center.x + thickness + depthPadding : center.x + depthPadding + 1f;
                    float yMinCell = center.y + widthRange.x - sidePadding;
                    float yMaxCell = center.y + widthRange.y + sidePadding + 1f;
                    return CreateCellBounds(xMinCell, yMinCell, xMaxCell, yMaxCell);
                }

                float xMin = center.x + widthRange.x - sidePadding;
                float xMax = center.x + widthRange.y + sidePadding + 1f;
                float yMin = direction.y > 0 ? center.y - depthPadding : center.y - thickness - depthPadding + 1f;
                float yMax = direction.y > 0 ? center.y + thickness + depthPadding : center.y + depthPadding + 1f;
                return CreateCellBounds(xMin, yMin, xMax, yMax);
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
            Bounds fallbackBounds = new Bounds((worldMin + worldMax) * 0.5f, worldMax - worldMin);
            fallbackBounds.Expand(new Vector3(
                Mathf.Abs(cellSize.x) * doorBlockerSidePaddingCells,
                Mathf.Abs(cellSize.y) * doorBlockerSidePaddingCells,
                0f));
            return fallbackBounds;
        }

        private Bounds CreateCellBounds(float xMinCell, float yMinCell, float xMaxCell, float yMaxCell)
        {
            Vector3 worldMin = floorTilemap.CellToWorld(new Vector3Int(Mathf.FloorToInt(xMinCell), Mathf.FloorToInt(yMinCell), 0));
            Vector3 worldMax = floorTilemap.CellToWorld(new Vector3Int(Mathf.CeilToInt(xMaxCell), Mathf.CeilToInt(yMaxCell), 0));
            return new Bounds((worldMin + worldMax) * 0.5f, worldMax - worldMin);
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

        private Transform GetWallBoundaryColliderRoot()
        {
            Transform gridRoot = GetGridRoot();
            if (wallBoundaryColliderRoot != null && wallBoundaryColliderRoot.parent == gridRoot)
            {
                return wallBoundaryColliderRoot;
            }

            GameObject root = new GameObject("Generated Wall Boundary Colliders");
            root.transform.SetParent(gridRoot, false);
            wallBoundaryColliderRoot = root.transform;
            return wallBoundaryColliderRoot;
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

        private void ClearGeneratedWallBoundaryColliders()
        {
            wallBoundaryColliders.Clear();

            if (wallBoundaryColliderRoot != null)
            {
                DestroyGeneratedObject(wallBoundaryColliderRoot.gameObject);
                wallBoundaryColliderRoot = null;
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
                doorCollider.enabled = locked && useDoorTilemapCollider;
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
