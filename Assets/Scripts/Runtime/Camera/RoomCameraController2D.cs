using NeonBreaker.Dungeon;
using NeonBreaker.Player;
using NeonBreaker.Rooms;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace NeonBreaker.CameraSystem
{
    public sealed class RoomCameraController2D : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform target;
        [SerializeField] private RoomRunManager runManager;
        [SerializeField] private RoomManager roomManager;
        [SerializeField] private TilemapDungeonGenerator dungeonGenerator;

        [Header("Follow")]
        [SerializeField] private Vector3 followOffset = new Vector3(0f, 0f, -10f);
        [SerializeField, Min(0f)] private float smoothTime = 0.12f;
        [SerializeField] private bool snapOnRoomStarted = true;
        [SerializeField] private bool snapOnlyOnFirstRoom = true;
        [SerializeField, Min(0f)] private float roomTransitionDuration = 0.45f;
        [SerializeField, Min(0f)] private float roomTransitionSmoothTime = 0.28f;
        [SerializeField] private bool followUnscaledTime;

        [Header("Room Bounds")]
        [SerializeField] private bool confineToCurrentRoom = true;
        [SerializeField, Min(0f)] private float roomPadding = 0.5f;
        [SerializeField] private bool useActualRoomFloorBounds = true;
        [SerializeField] private bool followTargetWhenRoomFitsView = true;
        [SerializeField] private bool lockToRoomCenterWhenRoomFitsViewDuringCombat = true;
        [SerializeField] private bool includeCorridorWhileRoomCleared = true;
        [SerializeField] private bool freeFollowWhileMovingBetweenRooms = true;
        [SerializeField] private bool freeFollowInRestRooms = true;

        [Header("Zoom")]
        [SerializeField] private bool fitZoomToRoom = true;
        [SerializeField, Min(1f)] private float minOrthographicSize = 5.5f;
        [SerializeField, Min(1f)] private float maxOrthographicSize = 8.5f;
        [SerializeField, Min(0f)] private float zoomSmoothTime = 0.16f;
        [SerializeField, Range(0.1f, 1f)] private float roomFitRatio = 0.72f;
        [SerializeField] private bool keepRoomZoomWhileMovingBetweenRooms = true;

        [Header("Boss Intro Focus")]
        [SerializeField] private bool focusRoomCenterDuringBossIntro = true;
        [SerializeField, Min(0f)] private float bossIntroFocusSmoothTime = 0.16f;
        [SerializeField, Min(0f)] private float bossIntroReturnSmoothTime = 0.24f;

        private Vector3 moveVelocity;
        private float zoomVelocity;
        private int activeRoomIndex = -1;
        private bool hasRoomBounds;
        private bool ignoresRoomBoundsForActiveRoom;
        private Bounds activeRoomBounds;
        private float roomTransitionTimer;
        private bool isBossIntroFocusActive;
        private float bossIntroReturnTimer;

        private void Awake()
        {
            ResolveSources();
        }

        private void OnEnable()
        {
            ResolveSources();
            Subscribe();
            RefreshRoomBounds();
            SnapToTarget();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void LateUpdate()
        {
            ResolveSources();

            if (target == null)
            {
                return;
            }

            if (runManager != null && runManager.CurrentRoomIndex != activeRoomIndex)
            {
                RefreshRoomBounds();
            }

            float deltaTime = ShouldUseUnscaledCameraTime() ? Time.unscaledDeltaTime : Time.deltaTime;
            Vector3 desiredPosition = GetDesiredCameraPosition();
            float currentSmoothTime = GetCurrentSmoothTime();

            transform.position = currentSmoothTime <= 0f
                ? desiredPosition
                : Vector3.SmoothDamp(transform.position, desiredPosition, ref moveVelocity, currentSmoothTime, Mathf.Infinity, deltaTime);

            UpdateZoom(deltaTime);
            UpdateRoomTransition(deltaTime);
        }

        public void SnapToTarget()
        {
            if (target == null)
            {
                ResolveSources();
            }

            if (target == null)
            {
                return;
            }

            moveVelocity = Vector3.zero;
            transform.position = GetDesiredCameraPosition();

            if (targetCamera != null && fitZoomToRoom && hasRoomBounds)
            {
                targetCamera.orthographicSize = GetTargetOrthographicSize();
                zoomVelocity = 0f;
            }
        }

        private void Subscribe()
        {
            if (runManager != null)
            {
                runManager.RunRoomStarted += HandleRunRoomStarted;
                runManager.RunRoomCleared += HandleRunRoomCleared;
            }

            if (roomManager != null)
            {
                roomManager.RoomIntroStarted += HandleRoomIntroStarted;
                roomManager.RoomIntroFinished += HandleRoomIntroFinished;
            }

            if (dungeonGenerator != null)
            {
                dungeonGenerator.DungeonGenerated += HandleDungeonGenerated;
            }
        }

        private void Unsubscribe()
        {
            if (runManager != null)
            {
                runManager.RunRoomStarted -= HandleRunRoomStarted;
                runManager.RunRoomCleared -= HandleRunRoomCleared;
            }

            if (roomManager != null)
            {
                roomManager.RoomIntroStarted -= HandleRoomIntroStarted;
                roomManager.RoomIntroFinished -= HandleRoomIntroFinished;
            }

            if (dungeonGenerator != null)
            {
                dungeonGenerator.DungeonGenerated -= HandleDungeonGenerated;
            }
        }

        private void HandleRunRoomStarted(int roomIndex, RoomDefinition room)
        {
            isBossIntroFocusActive = false;
            bossIntroReturnTimer = 0f;

            if (ShouldFreeFollowForRoom(room))
            {
                UseFreeFollowForRoom(roomIndex);
                return;
            }

            ignoresRoomBoundsForActiveRoom = false;
            RefreshRoomBounds();
            roomTransitionTimer = roomTransitionDuration;

            if (snapOnRoomStarted && (!snapOnlyOnFirstRoom || roomIndex <= 0))
            {
                SnapToTarget();
                roomTransitionTimer = 0f;
            }
        }

        private void HandleRunRoomCleared(int roomIndex, RoomDefinition room)
        {
            if (ignoresRoomBoundsForActiveRoom)
            {
                return;
            }

            if (includeCorridorWhileRoomCleared)
            {
                RefreshRoomBounds();
            }
        }

        private void HandleDungeonGenerated(System.Collections.Generic.IReadOnlyList<RectInt> rooms)
        {
            if (ignoresRoomBoundsForActiveRoom)
            {
                return;
            }

            RefreshRoomBounds();
            SnapToTarget();
        }

        private void HandleRoomIntroStarted(RoomDefinition room, string title, string subtitle, float duration)
        {
            if (!focusRoomCenterDuringBossIntro || room == null || room.RoomType != RoomType.Boss)
            {
                return;
            }

            ignoresRoomBoundsForActiveRoom = false;
            RefreshRoomBounds();

            if (!hasRoomBounds)
            {
                return;
            }

            isBossIntroFocusActive = true;
            bossIntroReturnTimer = 0f;
            roomTransitionTimer = 0f;
            moveVelocity = Vector3.zero;
        }

        private void HandleRoomIntroFinished(RoomDefinition room)
        {
            if (!isBossIntroFocusActive)
            {
                return;
            }

            isBossIntroFocusActive = false;
            bossIntroReturnTimer = bossIntroReturnSmoothTime;
            moveVelocity = Vector3.zero;
        }

        private bool ShouldFreeFollowForRoom(RoomDefinition room)
        {
            return freeFollowInRestRooms
                && room != null
                && room.RoomType == RoomType.Rest;
        }

        private void UseFreeFollowForRoom(int roomIndex)
        {
            activeRoomIndex = roomIndex;
            hasRoomBounds = false;
            ignoresRoomBoundsForActiveRoom = true;
            roomTransitionTimer = 0f;
            moveVelocity = Vector3.zero;
            zoomVelocity = 0f;
        }

        private Vector3 GetDesiredCameraPosition()
        {
            if (isBossIntroFocusActive && hasRoomBounds)
            {
                Vector3 centerPosition = activeRoomBounds.center;
                centerPosition.z = followOffset.z;
                return centerPosition;
            }

            Vector3 desired = target.position + followOffset;
            desired.z = followOffset.z;

            if (ShouldFreeFollowBetweenRooms())
            {
                return desired;
            }

            if (ignoresRoomBoundsForActiveRoom || !confineToCurrentRoom || !hasRoomBounds || targetCamera == null || !targetCamera.orthographic)
            {
                return desired;
            }

            float halfHeight = targetCamera.orthographicSize;
            float halfWidth = halfHeight * targetCamera.aspect;

            Vector3 min = activeRoomBounds.min + Vector3.one * roomPadding;
            Vector3 max = activeRoomBounds.max - Vector3.one * roomPadding;
            Vector3 center = activeRoomBounds.center;
            bool followWhenRoomFitsView = ShouldFollowTargetWhenRoomFitsView();

            desired.x = GetClampedAxis(desired.x, min.x + halfWidth, max.x - halfWidth, center.x, followWhenRoomFitsView);
            desired.y = GetClampedAxis(desired.y, min.y + halfHeight, max.y - halfHeight, center.y, followWhenRoomFitsView);
            return desired;
        }

        private void UpdateZoom(float deltaTime)
        {
            if (ignoresRoomBoundsForActiveRoom || !fitZoomToRoom || targetCamera == null || !targetCamera.orthographic || !hasRoomBounds)
            {
                return;
            }

            if (keepRoomZoomWhileMovingBetweenRooms && ShouldFreeFollowBetweenRooms())
            {
                return;
            }

            float targetSize = GetTargetOrthographicSize();
            targetCamera.orthographicSize = zoomSmoothTime <= 0f
                ? targetSize
                : Mathf.SmoothDamp(targetCamera.orthographicSize, targetSize, ref zoomVelocity, zoomSmoothTime, Mathf.Infinity, deltaTime);
        }

        private float GetTargetOrthographicSize()
        {
            if (targetCamera == null || !hasRoomBounds)
            {
                return minOrthographicSize;
            }

            float safeRatio = Mathf.Clamp(roomFitRatio, 0.1f, 1f);
            float roomHeightSize = activeRoomBounds.size.y * 0.5f / safeRatio;
            float roomWidthSize = activeRoomBounds.size.x * 0.5f / Mathf.Max(0.1f, targetCamera.aspect) / safeRatio;
            float fittedSize = Mathf.Min(roomHeightSize, roomWidthSize);
            return Mathf.Clamp(fittedSize, minOrthographicSize, maxOrthographicSize);
        }

        private float GetCurrentSmoothTime()
        {
            if (isBossIntroFocusActive)
            {
                return bossIntroFocusSmoothTime;
            }

            if (bossIntroReturnTimer > 0f)
            {
                return Mathf.Max(smoothTime, bossIntroReturnSmoothTime);
            }

            if (roomTransitionTimer <= 0f)
            {
                return smoothTime;
            }

            return Mathf.Max(smoothTime, roomTransitionSmoothTime);
        }

        private void UpdateRoomTransition(float deltaTime)
        {
            if (roomTransitionTimer > 0f)
            {
                roomTransitionTimer = Mathf.Max(0f, roomTransitionTimer - deltaTime);
            }

            if (bossIntroReturnTimer > 0f)
            {
                bossIntroReturnTimer = Mathf.Max(0f, bossIntroReturnTimer - deltaTime);
            }
        }

        private bool ShouldUseUnscaledCameraTime()
        {
            return followUnscaledTime || isBossIntroFocusActive || bossIntroReturnTimer > 0f;
        }

        private bool ShouldFreeFollowBetweenRooms()
        {
            return freeFollowWhileMovingBetweenRooms
                && runManager != null
                && runManager.IsWaitingForExit
                && runManager.HasNextRoom;
        }

        private bool ShouldFollowTargetWhenRoomFitsView()
        {
            if (!followTargetWhenRoomFitsView)
            {
                return false;
            }

            return !lockToRoomCenterWhenRoomFitsViewDuringCombat || ShouldFreeFollowBetweenRooms();
        }

        private void RefreshRoomBounds()
        {
            activeRoomIndex = runManager != null ? runManager.CurrentRoomIndex : -1;
            ignoresRoomBoundsForActiveRoom = false;
            hasRoomBounds = TryBuildCurrentRoomBounds(out activeRoomBounds);
        }

        private bool TryBuildCurrentRoomBounds(out Bounds worldBounds)
        {
            worldBounds = default;

            if (dungeonGenerator == null || activeRoomIndex < 0 || !TryGetCameraRoomBounds(activeRoomIndex, out RectInt roomBounds))
            {
                return false;
            }

            RectInt bounds = roomBounds;
            if (includeCorridorWhileRoomCleared && runManager != null && runManager.IsWaitingForExit && runManager.HasNextRoom)
            {
                if (TryGetCameraRoomBounds(activeRoomIndex + 1, out RectInt nextRoomBounds))
                {
                    bounds = Union(bounds, nextRoomBounds);
                }
            }

            Tilemap floorTilemap = dungeonGenerator.FloorTilemap;
            if (floorTilemap == null)
            {
                return false;
            }

            Vector3 min = floorTilemap.CellToWorld(new Vector3Int(bounds.xMin, bounds.yMin, 0));
            Vector3 max = floorTilemap.CellToWorld(new Vector3Int(bounds.xMax, bounds.yMax, 0));
            Vector3 center = (min + max) * 0.5f;
            Vector3 size = new Vector3(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y), 0f);
            worldBounds = new Bounds(center, size);
            return true;
        }

        private bool TryGetCameraRoomBounds(int roomIndex, out RectInt roomBounds)
        {
            if (dungeonGenerator == null)
            {
                roomBounds = default;
                return false;
            }

            return useActualRoomFloorBounds
                ? dungeonGenerator.TryGetRoomFloorBounds(roomIndex, out roomBounds)
                : dungeonGenerator.TryGetRoomBounds(roomIndex, out roomBounds);
        }

        private void ResolveSources()
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponent<Camera>();
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (target == null)
            {
                PlayerController player = FindAnyObjectByType<PlayerController>();
                if (player != null)
                {
                    target = player.transform;
                }
            }

            if (runManager == null)
            {
                runManager = FindAnyObjectByType<RoomRunManager>();
            }

            if (roomManager == null)
            {
                roomManager = FindAnyObjectByType<RoomManager>();
            }

            if (dungeonGenerator == null)
            {
                dungeonGenerator = FindAnyObjectByType<TilemapDungeonGenerator>();
            }
        }

        private static RectInt Union(RectInt a, RectInt b)
        {
            int xMin = Mathf.Min(a.xMin, b.xMin);
            int yMin = Mathf.Min(a.yMin, b.yMin);
            int xMax = Mathf.Max(a.xMax, b.xMax);
            int yMax = Mathf.Max(a.yMax, b.yMax);
            return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        private float GetClampedAxis(float value, float min, float max, float fallback, bool followWhenRoomFitsView)
        {
            if (min > max)
            {
                return followWhenRoomFitsView ? value : fallback;
            }

            return Mathf.Clamp(value, min, max);
        }
    }
}
