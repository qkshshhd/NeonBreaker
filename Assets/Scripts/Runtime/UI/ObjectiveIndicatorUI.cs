using System.Collections.Generic;
using NeonBreaker.Dungeon;
using NeonBreaker.Enemies;
using NeonBreaker.Player;
using NeonBreaker.Rooms;
using NeonBreaker.Upgrades;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeonBreaker.UI
{
    public sealed class ObjectiveIndicatorUI : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private RoomRunManager runManager;
        [SerializeField] private UpgradeManager upgradeManager;
        [SerializeField] private RoomRewardSpawner rewardSpawner;
        [SerializeField] private TilemapDungeonGenerator dungeonGenerator;
        [SerializeField] private RoomExit roomExit;
        [SerializeField] private Transform player;
        [SerializeField] private Camera worldCamera;

        [Header("Bindings")]
        [SerializeField] private CanvasGroup root;
        [SerializeField] private RectTransform marker;
        [SerializeField] private RectTransform arrow;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI distanceText;

        [Header("Options")]
        [SerializeField] private bool buildFallbackUiIfMissing = true;
        [SerializeField] private bool showWhenTargetIsOnScreen = true;
        [SerializeField] private float edgePadding = 56f;
        [SerializeField] private float hideDistance = 1.15f;
        [SerializeField, Min(24f)] private float objectiveRadiusAroundPlayer = 112f;
        [SerializeField] private string rewardLabel = "UPGRADE";
        [SerializeField] private string exitLabel = "NEXT";
        [SerializeField] private bool alwaysShowExitGuide = true;
        [SerializeField] private bool preferExitDirectionFallback = true;
        [SerializeField, Min(0f)] private float exitGuideOutsideRoomCells = 1.5f;
        [SerializeField, Min(0f)] private float passedExitSwitchDistance = 0.9f;

        [Header("Enemy Direction Guide")]
        [SerializeField] private bool showEnemyDirections = true;
        [SerializeField] private bool showOnlyOffscreenEnemies = true;
        [SerializeField, Min(1)] private int maxEnemyArrows = 6;
        [SerializeField, Min(24f)] private float enemyArrowRadiusAroundPlayer = 88f;
        [SerializeField, Range(0f, 90f)] private float mergeEnemyDirectionsWithinDegrees = 18f;
        [SerializeField, Min(0f)] private float enemyScreenPadding = 24f;
        [SerializeField, Range(0.1f, 2f)] private float enemyArrowScale = 0.82f;

        [Header("Guide Colors")]
        [SerializeField] private Color enemyColor = new Color(1f, 0.18f, 0.22f, 0.96f);
        [SerializeField] private Color rewardColor = new Color(0.25f, 1f, 0.42f, 0.96f);
        [SerializeField] private Color exitColor = new Color(0.48f, 0.88f, 1f, 0.96f);

        private readonly List<EnemyDirection> enemyDirections = new List<EnemyDirection>();
        private readonly List<RectTransform> enemyArrowInstances = new List<RectTransform>();

        private void Awake()
        {
            ResolveSources();

            if (buildFallbackUiIfMissing && root == null)
            {
                BuildFallbackUi();
            }

            EnsureEnemyArrowInstances();
            SetVisible(false);
            SetEnemyArrowsVisible(0);
        }

        private void LateUpdate()
        {
            ResolveSources();

            SetEnemyArrowsVisible(0);

            if (TryGetObjective(out Objective objective))
            {
                UpdateMarker(objective);
                return;
            }

            if (UpdateEnemyDirections())
            {
                SetVisible(false);
                return;
            }

            SetVisible(false);
        }

        private bool TryGetObjective(out Objective objective)
        {
            objective = default;

            if (upgradeManager != null && upgradeManager.HasActiveChoices && !upgradeManager.ChoicesVisible)
            {
                Transform reward = GetRewardTransform();
                if (reward != null)
                {
                    objective = new Objective(reward.position, rewardLabel, rewardColor, false);
                    return true;
                }
            }

            if (runManager != null && runManager.IsWaitingForExit && runManager.HasNextRoom)
            {
                if (TryGetExitGuidePosition(out Vector3 exitPosition))
                {
                    objective = new Objective(exitPosition, exitLabel, exitColor, alwaysShowExitGuide);
                    return true;
                }

                if (roomExit != null && roomExit.gameObject.activeInHierarchy)
                {
                    objective = new Objective(roomExit.transform.position, exitLabel, exitColor, alwaysShowExitGuide);
                    return true;
                }

                if (dungeonGenerator != null)
                {
                    objective = new Objective(dungeonGenerator.GetRoomCenterWorld(runManager.CurrentRoomIndex + 1), exitLabel, exitColor, alwaysShowExitGuide);
                    return true;
                }
            }

            return false;
        }

        private bool TryGetExitGuidePosition(out Vector3 position)
        {
            position = default;
            if (runManager == null || dungeonGenerator == null || !runManager.HasNextRoom)
            {
                return false;
            }

            int currentIndex = runManager.CurrentRoomIndex;
            int nextIndex = currentIndex + 1;
            Vector3 currentCenter = dungeonGenerator.GetRoomCenterWorld(currentIndex);
            Vector3 nextCenter = dungeonGenerator.GetRoomCenterWorld(nextIndex);
            Vector3 nextRoomTarget = dungeonGenerator.TryGetSafeRoomWorldPosition(nextIndex, out Vector3 safeNextPosition)
                ? safeNextPosition
                : nextCenter;

            if (dungeonGenerator.TryGetRoomExitDoorCenterWorld(currentIndex, out Vector3 doorPosition)
                && IsExitDoorPointingTowardNextRoom(currentCenter, nextCenter, doorPosition))
            {
                position = HasPlayerPassedExit(currentCenter, nextCenter, doorPosition)
                    ? nextRoomTarget
                    : doorPosition;
                return true;
            }

            if (!preferExitDirectionFallback)
            {
                return false;
            }

            if (TryGetDirectionalExitFallback(currentIndex, currentCenter, nextCenter, out Vector3 fallbackExitPosition))
            {
                position = HasPlayerPassedExit(currentCenter, nextCenter, fallbackExitPosition)
                    ? nextRoomTarget
                    : fallbackExitPosition;
                return true;
            }

            position = nextRoomTarget;
            return true;
        }

        private bool HasPlayerPassedExit(Vector3 currentCenter, Vector3 nextCenter, Vector3 exitPosition)
        {
            if (player == null)
            {
                return false;
            }

            Vector2 directionToNext = nextCenter - currentCenter;
            if (directionToNext.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            Vector2 playerFromExit = player.position - exitPosition;
            float passedDistance = Vector2.Dot(playerFromExit, directionToNext.normalized);
            return passedDistance > Mathf.Max(0f, passedExitSwitchDistance);
        }

        private static bool IsExitDoorPointingTowardNextRoom(Vector3 currentCenter, Vector3 nextCenter, Vector3 doorPosition)
        {
            Vector2 toNext = nextCenter - currentCenter;
            Vector2 toDoor = doorPosition - currentCenter;
            if (toNext.sqrMagnitude <= 0.0001f || toDoor.sqrMagnitude <= 0.0001f)
            {
                return true;
            }

            return Vector2.Dot(toNext.normalized, toDoor.normalized) > 0.25f;
        }

        private bool TryGetDirectionalExitFallback(int currentIndex, Vector3 currentCenter, Vector3 nextCenter, out Vector3 position)
        {
            position = nextCenter;

            Vector2 toNext = nextCenter - currentCenter;
            if (toNext.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            Vector2Int direction = Mathf.Abs(toNext.x) >= Mathf.Abs(toNext.y)
                ? new Vector2Int(toNext.x >= 0f ? 1 : -1, 0)
                : new Vector2Int(0, toNext.y >= 0f ? 1 : -1);

            if (!dungeonGenerator.TryGetRoomBounds(currentIndex, out RectInt roomBounds)
                || dungeonGenerator.FloorTilemap == null)
            {
                position = currentCenter + (Vector3)(toNext.normalized * 4f);
                return true;
            }

            Vector2 roomCenter = roomBounds.center;
            int cellX = Mathf.RoundToInt(roomCenter.x);
            int cellY = Mathf.RoundToInt(roomCenter.y);
            int outsideCells = Mathf.Max(1, Mathf.RoundToInt(exitGuideOutsideRoomCells));

            if (direction.x > 0)
            {
                cellX = roomBounds.xMax + outsideCells;
            }
            else if (direction.x < 0)
            {
                cellX = roomBounds.xMin - outsideCells - 1;
            }
            else if (direction.y > 0)
            {
                cellY = roomBounds.yMax + outsideCells;
            }
            else
            {
                cellY = roomBounds.yMin - outsideCells - 1;
            }

            position = dungeonGenerator.FloorTilemap.GetCellCenterWorld(new Vector3Int(cellX, cellY, 0));
            return true;
        }

        private Transform GetRewardTransform()
        {
            if (rewardSpawner != null && rewardSpawner.ActiveReward != null && rewardSpawner.ActiveReward.gameObject.activeInHierarchy)
            {
                return rewardSpawner.ActiveReward.transform;
            }

            UpgradeRewardPickup2D reward = FindAnyObjectByType<UpgradeRewardPickup2D>();
            if (reward != null && reward.gameObject.activeInHierarchy)
            {
                return reward.transform;
            }

            return null;
        }

        private void UpdateMarker(Objective objective)
        {
            if (worldCamera == null || marker == null)
            {
                SetVisible(false);
                return;
            }

            Vector3 screenPoint = worldCamera.WorldToScreenPoint(objective.WorldPosition);
            if (screenPoint.z < 0f)
            {
                screenPoint.x = Screen.width - screenPoint.x;
                screenPoint.y = Screen.height - screenPoint.y;
            }

            float safePadding = Mathf.Max(0f, edgePadding);
            bool targetOnScreen = screenPoint.z >= 0f
                && screenPoint.x >= safePadding
                && screenPoint.x <= Screen.width - safePadding
                && screenPoint.y >= safePadding
                && screenPoint.y <= Screen.height - safePadding;

            float distance = player != null ? Vector2.Distance(player.position, objective.WorldPosition) : 0f;
            if (targetOnScreen && !showWhenTargetIsOnScreen && !objective.AlwaysShowWhenOnScreen || distance <= hideDistance)
            {
                SetVisible(false);
                return;
            }

            Vector2 playerScreenPosition = GetPlayerScreenPosition();
            Vector2 direction = GetScreenDirection(screenPoint, playerScreenPosition, objective.WorldPosition);
            Vector2 desiredPosition = playerScreenPosition + direction * objectiveRadiusAroundPlayer;
            Vector2 clampedPosition = ClampToScreen(desiredPosition, safePadding);

            marker.position = clampedPosition;
            RotateArrow(arrow, direction);

            SetText(titleText, objective.Label);
            SetText(distanceText, distance > 0f ? $"{distance:0}m" : string.Empty);
            SetColor(objective.Color);
            SetVisible(true);
        }

        private bool UpdateEnemyDirections()
        {
            if (!showEnemyDirections || player == null || worldCamera == null)
            {
                return false;
            }

            enemyDirections.Clear();
            Vector2 playerScreenPosition = GetPlayerScreenPosition();

            foreach (EnemyController enemy in EnemyController.ActiveEnemies)
            {
                if (enemy == null
                    || enemy.IsDead
                    || !enemy.gameObject.activeInHierarchy)
                {
                    continue;
                }

                float distance = Vector2.Distance(player.position, enemy.transform.position);
                if (distance <= hideDistance)
                {
                    continue;
                }

                Vector3 screenPoint = worldCamera.WorldToScreenPoint(enemy.transform.position);
                if (showOnlyOffscreenEnemies && IsOnScreen(screenPoint, enemyScreenPadding))
                {
                    continue;
                }

                Vector2 direction = GetScreenDirection(screenPoint, playerScreenPosition, enemy.transform.position);
                if (direction.sqrMagnitude <= 0.0001f)
                {
                    continue;
                }

                enemyDirections.Add(new EnemyDirection(direction, distance));
            }

            if (enemyDirections.Count == 0)
            {
                return false;
            }

            enemyDirections.Sort((left, right) => left.Distance.CompareTo(right.Distance));
            EnsureEnemyArrowInstances();

            int visibleCount = 0;
            int safeMaximum = Mathf.Min(Mathf.Max(1, maxEnemyArrows), enemyArrowInstances.Count);
            for (int i = 0; i < enemyDirections.Count && visibleCount < safeMaximum; i++)
            {
                Vector2 direction = enemyDirections[i].Direction;
                if (HasNearbyDirection(direction, visibleCount))
                {
                    continue;
                }

                RectTransform enemyArrow = enemyArrowInstances[visibleCount];
                Vector2 desiredPosition = playerScreenPosition + direction * enemyArrowRadiusAroundPlayer;
                enemyArrow.position = ClampToScreen(desiredPosition, edgePadding);
                enemyArrow.localScale = Vector3.one * enemyArrowScale;
                RotateArrow(enemyArrow, direction);
                SetArrowColor(enemyArrow, enemyColor);
                enemyArrow.gameObject.SetActive(true);
                visibleCount++;
            }

            SetEnemyArrowsVisible(visibleCount);
            return visibleCount > 0;
        }

        private bool HasNearbyDirection(Vector2 direction, int activeCount)
        {
            float threshold = Mathf.Max(0f, mergeEnemyDirectionsWithinDegrees);
            for (int i = 0; i < activeCount; i++)
            {
                RectTransform activeArrow = enemyArrowInstances[i];
                Vector2 activeDirection = Quaternion.Euler(0f, 0f, activeArrow.localEulerAngles.z + 90f) * Vector2.right;
                if (Vector2.Angle(activeDirection, direction) <= threshold)
                {
                    return true;
                }
            }

            return false;
        }

        private void EnsureEnemyArrowInstances()
        {
            if (arrow == null || marker == null)
            {
                return;
            }

            int requiredCount = Mathf.Max(1, maxEnemyArrows);
            Transform parent = marker.parent;
            while (enemyArrowInstances.Count < requiredCount)
            {
                GameObject clone = Instantiate(arrow.gameObject, parent, false);
                clone.name = $"Enemy Direction Arrow {enemyArrowInstances.Count + 1}";
                RectTransform cloneRect = clone.GetComponent<RectTransform>();
                cloneRect.gameObject.SetActive(false);
                enemyArrowInstances.Add(cloneRect);
            }
        }

        private void SetEnemyArrowsVisible(int visibleCount)
        {
            for (int i = 0; i < enemyArrowInstances.Count; i++)
            {
                if (enemyArrowInstances[i] != null)
                {
                    enemyArrowInstances[i].gameObject.SetActive(i < visibleCount);
                }
            }
        }

        private Vector2 GetPlayerScreenPosition()
        {
            return player != null && worldCamera != null
                ? (Vector2)worldCamera.WorldToScreenPoint(player.position)
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        private Vector2 GetScreenDirection(Vector3 targetScreenPoint, Vector2 playerScreenPosition, Vector3 targetWorldPosition)
        {
            Vector2 direction = (Vector2)targetScreenPoint - playerScreenPosition;
            if (targetScreenPoint.z < 0f)
            {
                direction = -direction;
            }

            if (direction.sqrMagnitude <= 0.0001f && player != null)
            {
                direction = (Vector2)(targetWorldPosition - player.position);
            }

            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
        }

        private static bool IsOnScreen(Vector3 screenPoint, float padding)
        {
            float safePadding = Mathf.Max(0f, padding);
            return screenPoint.z >= 0f
                && screenPoint.x >= safePadding
                && screenPoint.x <= Screen.width - safePadding
                && screenPoint.y >= safePadding
                && screenPoint.y <= Screen.height - safePadding;
        }

        private static Vector2 ClampToScreen(Vector2 position, float padding)
        {
            float safePadding = Mathf.Max(0f, padding);
            return new Vector2(
                Mathf.Clamp(position.x, safePadding, Screen.width - safePadding),
                Mathf.Clamp(position.y, safePadding, Screen.height - safePadding));
        }

        private static void RotateArrow(RectTransform targetArrow, Vector2 direction)
        {
            if (targetArrow == null)
            {
                return;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            targetArrow.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private static void SetArrowColor(RectTransform targetArrow, Color color)
        {
            if (targetArrow != null && targetArrow.TryGetComponent(out Graphic graphic))
            {
                graphic.color = color;
            }
        }

        private void ResolveSources()
        {
            if (runManager == null)
            {
                runManager = FindAnyObjectByType<RoomRunManager>();
            }

            if (upgradeManager == null)
            {
                upgradeManager = FindAnyObjectByType<UpgradeManager>();
            }

            if (rewardSpawner == null)
            {
                rewardSpawner = FindAnyObjectByType<RoomRewardSpawner>();
            }

            if (dungeonGenerator == null)
            {
                dungeonGenerator = FindAnyObjectByType<TilemapDungeonGenerator>();
            }

            if (roomExit == null)
            {
                roomExit = FindAnyObjectByType<RoomExit>();
            }

            if (player == null)
            {
                PlayerController playerController = FindAnyObjectByType<PlayerController>();
                if (playerController != null)
                {
                    player = playerController.transform;
                }
            }

            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }
        }

        private void SetVisible(bool visible)
        {
            if (root == null)
            {
                return;
            }

            root.alpha = visible ? 1f : 0f;
            root.interactable = false;
            root.blocksRaycasts = false;
        }

        private void SetColor(Color color)
        {
            SetTextColor(titleText, color);
            SetTextColor(distanceText, new Color(color.r, color.g, color.b, 0.82f));

            if (arrow != null && arrow.TryGetComponent(out Graphic graphic))
            {
                graphic.color = color;
            }
        }

        private void BuildFallbackUi()
        {
            GameObject canvasObject = new GameObject("Objective Indicator Canvas");
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 240;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject markerObject = CreateUiObject("Objective Marker", canvasObject.transform);
            marker = markerObject.GetComponent<RectTransform>();
            marker.sizeDelta = new Vector2(132f, 86f);
            root = markerObject.AddComponent<CanvasGroup>();

            GameObject arrowObject = CreateUiObject("Arrow", markerObject.transform);
            arrow = arrowObject.GetComponent<RectTransform>();
            arrow.anchorMin = new Vector2(0.5f, 1f);
            arrow.anchorMax = new Vector2(0.5f, 1f);
            arrow.pivot = new Vector2(0.5f, 0.5f);
            arrow.anchoredPosition = new Vector2(0f, -18f);
            arrow.sizeDelta = new Vector2(36f, 36f);

            TextMeshProUGUI arrowText = arrowObject.AddComponent<TextMeshProUGUI>();
            arrowText.text = "^";
            arrowText.fontSize = 34f;
            arrowText.fontStyle = FontStyles.Bold;
            arrowText.alignment = TextAlignmentOptions.Center;
            arrowText.raycastTarget = false;

            titleText = CreateText(markerObject.transform, "Title", rewardLabel, 20f, FontStyles.Bold, new Vector2(0f, -50f));
            distanceText = CreateText(markerObject.transform, "Distance", "0m", 16f, FontStyles.Normal, new Vector2(0f, -72f));
        }

        private static TextMeshProUGUI CreateText(Transform parent, string objectName, string value, float size, FontStyles style, Vector2 anchoredPosition)
        {
            GameObject textObject = CreateUiObject(objectName, parent);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(132f, 24f);

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        private static GameObject CreateUiObject(string objectName, Transform parent)
        {
            GameObject uiObject = new GameObject(objectName);
            uiObject.transform.SetParent(parent, false);
            uiObject.AddComponent<RectTransform>();
            return uiObject;
        }

        private static void SetText(TextMeshProUGUI target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }

        private static void SetTextColor(TextMeshProUGUI target, Color color)
        {
            if (target != null)
            {
                target.color = color;
            }
        }

        private readonly struct Objective
        {
            public Objective(Vector3 worldPosition, string label, Color color, bool alwaysShowWhenOnScreen)
            {
                WorldPosition = worldPosition;
                Label = label;
                Color = color;
                AlwaysShowWhenOnScreen = alwaysShowWhenOnScreen;
            }

            public Vector3 WorldPosition { get; }
            public string Label { get; }
            public Color Color { get; }
            public bool AlwaysShowWhenOnScreen { get; }
        }

        private readonly struct EnemyDirection
        {
            public EnemyDirection(Vector2 direction, float distance)
            {
                Direction = direction;
                Distance = distance;
            }

            public Vector2 Direction { get; }
            public float Distance { get; }
        }
    }
}
