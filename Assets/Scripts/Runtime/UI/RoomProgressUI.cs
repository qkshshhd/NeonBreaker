using System.Collections.Generic;
using NeonBreaker.Rooms;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeonBreaker.UI
{
    public sealed class RoomProgressUI : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private RoomRunManager runManager;
        [SerializeField] private RoomManager roomManager;

        [Header("Bindings")]
        [SerializeField] private CanvasGroup root;
        [SerializeField] private TextMeshProUGUI roomText;
        [SerializeField] private TextMeshProUGUI waveText;
        [SerializeField] private TextMeshProUGUI enemyText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Image waveFill;

        [Header("Segment Bindings")]
        [SerializeField] private RectTransform roomSegmentRoot;
        [SerializeField] private RectTransform waveSegmentRoot;
        [SerializeField] private Image segmentTemplate;

        [Header("Options")]
        [SerializeField] private bool findTextInChildren = true;
        [SerializeField] private bool buildFallbackUiIfMissing = false;
        [SerializeField] private bool hideWhenRunCleared = false;
        [SerializeField] private bool showEnemyCount;
        [SerializeField] private bool showStatusText;
        [SerializeField] private string roomPrefix = "구역";
        [SerializeField] private string wavePrefix = "웨이브";

        [Header("Segment Style")]
        [SerializeField] private Color completedSegmentColor = new Color(0.18f, 0.95f, 1f, 1f);
        [SerializeField] private Color currentSegmentColor = new Color(0.92f, 1f, 1f, 1f);
        [SerializeField] private Color pendingSegmentColor = new Color(0.12f, 0.24f, 0.3f, 0.85f);
        [SerializeField, Min(1f)] private float currentSegmentPulseSpeed = 4f;
        [SerializeField, Range(0f, 0.5f)] private float currentSegmentPulseAmount = 0.12f;

        private RoomDefinition currentRoom;
        private int currentRoomIndex = -1;
        private int currentWaveIndex = -1;
        private int currentWaveCount;
        private int aliveEnemies;
        private bool currentRoomCleared;
        private bool currentWaveCleared;
        private readonly List<Image> roomSegments = new List<Image>();
        private readonly List<Image> waveSegments = new List<Image>();
        private Image currentRoomSegment;
        private Image currentWaveSegment;

        private void Awake()
        {
            if (runManager == null)
            {
                runManager = FindAnyObjectByType<RoomRunManager>();
            }

            if (roomManager == null)
            {
                roomManager = FindAnyObjectByType<RoomManager>();
            }

            if (root == null)
            {
                root = GetComponentInChildren<CanvasGroup>(true);
            }

            if (findTextInChildren)
            {
                AutoBindTexts();
            }

            if (buildFallbackUiIfMissing && (root == null || roomText == null || waveText == null || enemyText == null))
            {
                BuildFallbackUi();
            }

            SetVisible(true);
            Refresh();
        }

        private void Update()
        {
            UpdateCurrentSegmentPulse(currentRoomSegment);
            UpdateCurrentSegmentPulse(currentWaveSegment);
        }

        private void OnEnable()
        {
            if (runManager != null)
            {
                runManager.RunRoomStarted += HandleRunRoomStarted;
                runManager.RunRoomCombatCleared += HandleRunRoomCombatCleared;
                runManager.RunRoomCleared += HandleRunRoomCleared;
                runManager.RunCleared += HandleRunCleared;
            }

            if (roomManager != null)
            {
                roomManager.RoomStarted += HandleRoomStarted;
                roomManager.RoomWaveStarted += HandleRoomWaveStarted;
                roomManager.RoomWaveCleared += HandleRoomWaveCleared;
                roomManager.EnemyCountChanged += HandleEnemyCountChanged;
            }
        }

        private void OnDisable()
        {
            if (runManager != null)
            {
                runManager.RunRoomStarted -= HandleRunRoomStarted;
                runManager.RunRoomCombatCleared -= HandleRunRoomCombatCleared;
                runManager.RunRoomCleared -= HandleRunRoomCleared;
                runManager.RunCleared -= HandleRunCleared;
            }

            if (roomManager != null)
            {
                roomManager.RoomStarted -= HandleRoomStarted;
                roomManager.RoomWaveStarted -= HandleRoomWaveStarted;
                roomManager.RoomWaveCleared -= HandleRoomWaveCleared;
                roomManager.EnemyCountChanged -= HandleEnemyCountChanged;
            }
        }

        private void HandleRunRoomStarted(int roomIndex, RoomDefinition room)
        {
            currentRoomIndex = roomIndex;
            currentRoom = room;
            currentWaveIndex = -1;
            currentWaveCount = GetWaveCount(room);
            currentRoomCleared = false;
            currentWaveCleared = false;
            SetVisible(true);
            SetStatus(GetRoomStatusLabel(room));
            Refresh();
        }

        private void HandleRoomStarted(RoomDefinition room)
        {
            currentRoom = room;
            currentWaveIndex = -1;
            currentWaveCount = GetWaveCount(room);
            aliveEnemies = roomManager != null ? roomManager.AliveEnemyCount : 0;
            currentRoomCleared = false;
            currentWaveCleared = false;
            SetStatus(GetRoomStatusLabel(room));
            Refresh();
        }

        private void HandleRoomWaveStarted(RoomDefinition room, int waveIndex, RoomDefinition.EncounterWave wave)
        {
            currentRoom = room;
            currentWaveIndex = waveIndex;
            currentWaveCount = GetWaveCount(room);
            aliveEnemies = roomManager != null ? roomManager.AliveEnemyCount : 0;
            currentWaveCleared = false;
            SetStatus(wave != null && !string.IsNullOrWhiteSpace(wave.DisplayName) ? wave.DisplayName : "Wave");
            Refresh();
        }

        private void HandleRoomWaveCleared(RoomDefinition room, int waveIndex, RoomDefinition.EncounterWave wave)
        {
            currentRoom = room;
            currentWaveIndex = waveIndex;
            currentWaveCount = GetWaveCount(room);
            aliveEnemies = 0;
            currentWaveCleared = true;
            SetStatus("Wave Clear");
            Refresh();
        }

        private void HandleEnemyCountChanged(int count)
        {
            aliveEnemies = Mathf.Max(0, count);
            Refresh();
        }

        private void HandleRunRoomCombatCleared(int roomIndex, RoomDefinition room)
        {
            currentRoomIndex = roomIndex;
            currentRoom = room;
            currentWaveCount = GetWaveCount(room);
            currentWaveIndex = Mathf.Max(currentWaveIndex, currentWaveCount - 1);
            aliveEnemies = 0;
            currentWaveCleared = true;
            SetStatus(GetClearedStatusLabel(room));
            Refresh();
        }

        private void HandleRunRoomCleared(int roomIndex, RoomDefinition room)
        {
            currentRoomIndex = roomIndex;
            currentRoom = room;
            aliveEnemies = 0;
            currentRoomCleared = true;
            currentWaveCleared = true;
            SetStatus("Exit Open");
            Refresh();
        }

        private void HandleRunCleared()
        {
            SetStatus("Run Clear");
            Refresh();

            if (hideWhenRunCleared)
            {
                SetVisible(false);
            }
        }

        private void Refresh()
        {
            SetText(roomText, BuildRoomLabel());
            SetText(waveText, BuildWaveLabel());
            SetText(enemyText, showEnemyCount ? $"적 {aliveEnemies}" : string.Empty);

            if (enemyText != null)
            {
                enemyText.gameObject.SetActive(showEnemyCount);
            }

            if (statusText != null)
            {
                statusText.gameObject.SetActive(showStatusText);
            }

            if (waveFill != null)
            {
                waveFill.fillAmount = GetWaveProgress();
            }

            RefreshSegments();
        }

        private string BuildRoomLabel()
        {
            int totalRooms = runManager != null ? runManager.TotalRoomCount : 0;
            if (currentRoomIndex < 0)
            {
                return totalRooms > 0 ? $"{roomPrefix} - / {totalRooms}" : $"{roomPrefix} -";
            }

            return totalRooms > 0
                ? $"{roomPrefix} {currentRoomIndex + 1} / {totalRooms}"
                : $"{roomPrefix} {currentRoomIndex + 1}";
        }

        private string BuildWaveLabel()
        {
            if (currentWaveCount <= 0)
            {
                return $"{wavePrefix} -";
            }

            if (currentWaveIndex < 0)
            {
                return $"{wavePrefix} - / {currentWaveCount}";
            }

            return $"{wavePrefix} {currentWaveIndex + 1} / {currentWaveCount}";
        }

        private void RefreshSegments()
        {
            int totalRooms = runManager != null ? runManager.TotalRoomCount : 0;
            EnsureSegments(roomSegmentRoot, roomSegments, totalRooms, "Room Segment");
            EnsureSegments(waveSegmentRoot, waveSegments, currentWaveCount, "Wave Segment");

            currentRoomSegment = ApplySegmentStates(
                roomSegments,
                currentRoomIndex,
                currentRoomCleared);

            currentWaveSegment = ApplySegmentStates(
                waveSegments,
                currentWaveIndex,
                currentWaveCleared);
        }

        private void EnsureSegments(
            RectTransform targetRoot,
            List<Image> segments,
            int requiredCount,
            string objectName)
        {
            if (targetRoot == null || requiredCount < 0)
            {
                return;
            }

            while (segments.Count < requiredCount)
            {
                Image segment = CreateSegment(targetRoot, objectName, segments.Count);
                if (segment == null)
                {
                    break;
                }

                segments.Add(segment);
            }

            for (int i = 0; i < segments.Count; i++)
            {
                if (segments[i] != null)
                {
                    segments[i].gameObject.SetActive(i < requiredCount);
                }
            }
        }

        private Image CreateSegment(RectTransform parent, string objectName, int index)
        {
            Image segment;
            if (segmentTemplate != null)
            {
                segment = Instantiate(segmentTemplate, parent, false);
                segment.gameObject.SetActive(true);
            }
            else
            {
                GameObject segmentObject = CreateUiObject($"{objectName} {index + 1}", parent);
                RectTransform rect = segmentObject.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(18f, 6f);

                LayoutElement layout = segmentObject.AddComponent<LayoutElement>();
                layout.preferredWidth = 18f;
                layout.preferredHeight = 6f;

                segment = segmentObject.AddComponent<Image>();
            }

            segment.name = $"{objectName} {index + 1}";
            segment.raycastTarget = false;
            return segment;
        }

        private Image ApplySegmentStates(List<Image> segments, int currentIndex, bool allCompleted)
        {
            Image current = null;
            for (int i = 0; i < segments.Count; i++)
            {
                Image segment = segments[i];
                if (segment == null || !segment.gameObject.activeSelf)
                {
                    continue;
                }

                segment.rectTransform.localScale = Vector3.one;

                if (allCompleted || i < currentIndex)
                {
                    segment.color = completedSegmentColor;
                }
                else if (i == currentIndex)
                {
                    segment.color = currentSegmentColor;
                    current = segment;
                }
                else
                {
                    segment.color = pendingSegmentColor;
                }
            }

            return current;
        }

        private void UpdateCurrentSegmentPulse(Image segment)
        {
            if (segment == null || !segment.gameObject.activeInHierarchy)
            {
                return;
            }

            float wave = (Mathf.Sin(Time.unscaledTime * currentSegmentPulseSpeed) + 1f) * 0.5f;
            float scale = 1f + wave * currentSegmentPulseAmount;
            segment.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        private float GetWaveProgress()
        {
            if (currentWaveCount <= 0)
            {
                return 0f;
            }

            if (currentWaveIndex < 0)
            {
                return 0f;
            }

            return Mathf.Clamp01((currentWaveIndex + 1f) / currentWaveCount);
        }

        private void SetStatus(string status)
        {
            SetText(statusText, status);
        }

        private static string GetRoomStatusLabel(RoomDefinition room)
        {
            if (room == null)
            {
                return "Room";
            }

            return room.RoomType switch
            {
                RoomType.Elite => "Elite",
                RoomType.Reward => "Reward",
                RoomType.Rest => "Rest",
                RoomType.Boss => "Boss",
                _ => "Combat"
            };
        }

        private static string GetClearedStatusLabel(RoomDefinition room)
        {
            if (room == null)
            {
                return "Clear";
            }

            if (room.GrantsUpgradeReward)
            {
                return "Reward";
            }

            if (room.GrantsHealReward)
            {
                return "Healed";
            }

            return "Clear";
        }

        private void SetVisible(bool visible)
        {
            if (root == null)
            {
                return;
            }

            root.alpha = visible ? 1f : 0f;
            root.blocksRaycasts = false;
            root.interactable = false;
        }

        private void AutoBindTexts()
        {
            TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                string lowerName = texts[i].name.ToLowerInvariant();
                if (roomText == null && lowerName.Contains("room"))
                {
                    roomText = texts[i];
                }
                else if (waveText == null && lowerName.Contains("wave"))
                {
                    waveText = texts[i];
                }
                else if (enemyText == null && (lowerName.Contains("enemy") || lowerName.Contains("enemies")))
                {
                    enemyText = texts[i];
                }
                else if (statusText == null && lowerName.Contains("status"))
                {
                    statusText = texts[i];
                }
            }
        }

        private void BuildFallbackUi()
        {
            GameObject canvasObject = new GameObject("Room Progress Canvas");
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject rootObject = CreateUiObject("Room Progress Root", canvasObject.transform);
            root = rootObject.AddComponent<CanvasGroup>();

            RectTransform rootRect = rootObject.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.anchoredPosition = new Vector2(0f, -24f);
            rootRect.sizeDelta = new Vector2(620f, 86f);

            Image panel = rootObject.AddComponent<Image>();
            panel.color = new Color(0.03f, 0.035f, 0.05f, 0.76f);

            HorizontalLayoutGroup layout = rootObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 12, 12);
            layout.spacing = 24f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            roomText = CreateLabel(rootObject.transform, "Room Text", 170f);
            waveText = CreateLabel(rootObject.transform, "Wave Text", 170f);
            enemyText = CreateLabel(rootObject.transform, "Enemies Text", 130f);
            statusText = CreateLabel(rootObject.transform, "Status Text", 100f);

            GameObject roomSegmentsObject = CreateUiObject("Room Segments", rootObject.transform);
            roomSegmentRoot = roomSegmentsObject.GetComponent<RectTransform>();
            ConfigureSegmentLayout(roomSegmentsObject);

            GameObject waveSegmentsObject = CreateUiObject("Wave Segments", rootObject.transform);
            waveSegmentRoot = waveSegmentsObject.GetComponent<RectTransform>();
            ConfigureSegmentLayout(waveSegmentsObject);
        }

        private static void ConfigureSegmentLayout(GameObject target)
        {
            HorizontalLayoutGroup layout = target.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        private static TextMeshProUGUI CreateLabel(Transform parent, string objectName, float width)
        {
            GameObject textObject = CreateUiObject(objectName, parent);
            LayoutElement layout = textObject.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = 44f;

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = 24f;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.color = new Color(0.9f, 0.96f, 1f, 1f);
            return text;
        }

        private static GameObject CreateUiObject(string objectName, Transform parent)
        {
            GameObject uiObject = new GameObject(objectName);
            uiObject.transform.SetParent(parent, false);
            uiObject.AddComponent<RectTransform>();
            return uiObject;
        }

        private static int GetWaveCount(RoomDefinition room)
        {
            return room != null && room.Waves != null ? room.Waves.Length : 0;
        }

        private static void SetText(TextMeshProUGUI target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }
    }
}
