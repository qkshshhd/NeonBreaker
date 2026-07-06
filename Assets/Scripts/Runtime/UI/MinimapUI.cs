using System.Collections.Generic;
using NeonBreaker.Dungeon;
using NeonBreaker.Rooms;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeonBreaker.UI
{
    public sealed class MinimapUI : MonoBehaviour
    {
        private enum MinimapLayoutMode
        {
            StableProgression,
            StableDungeonSteps,
            ScaledDungeon,
            Linear
        }

        [Header("Sources")]
        [SerializeField] private RoomRunManager runManager;
        [SerializeField] private TilemapDungeonGenerator dungeonGenerator;

        [Header("Bindings")]
        [SerializeField] private CanvasGroup root;
        [SerializeField] private RectTransform mapRoot;
        [SerializeField] private TextMeshProUGUI titleText;

        [Header("Layout")]
        [SerializeField] private MinimapLayoutMode layoutMode = MinimapLayoutMode.StableProgression;
        [SerializeField] private bool useDungeonLayout = true;
        [SerializeField] private bool preserveDungeonAspectRatio = true;
        [SerializeField] private bool centerCurrentRoom = true;
        [SerializeField] private bool fitAllRoomsWhenCentered = false;
        [SerializeField] private bool clipMapToRoot = true;
        [SerializeField] private Vector2 fallbackMapSize = new Vector2(260f, 170f);
        [SerializeField] private Vector2 mapPadding = new Vector2(24f, 20f);
        [SerializeField] private float linearNodeSpacing = 44f;
        [SerializeField] private Vector2 stableNodeSpacing = new Vector2(44f, 34f);
        [SerializeField, Min(0)] private int stableVerticalLaneLimit = 2;
        [SerializeField, Range(0f, 1f)] private float stableVerticalTurnThreshold = 0.35f;
        [SerializeField] private float nodeSize = 18f;
        [SerializeField, Min(0f)] private float typeMarkerHeight = 3f;
        [SerializeField, Min(0f)] private float typeGlyphSize = 12f;
        [SerializeField] private float lineThickness = 4f;
        [SerializeField] private bool useOrthogonalConnectors = true;
        [SerializeField] private float edgeSafePadding = 10f;

        [Header("Options")]
        [SerializeField] private bool findBindingsInChildren = true;
        [SerializeField] private bool buildFallbackUiIfMissing = true;
        [SerializeField] private bool showFutureRooms = true;
        [SerializeField] private bool revealNextRoom = true;
        [SerializeField] private bool hideWhenRunCleared = false;
        [SerializeField] private bool pulseCurrentRoom = true;
        [SerializeField] private float pulseSpeed = 5f;
        [SerializeField] private float pulseAmount = 0.14f;

        [Header("Colors")]
        [SerializeField] private Color unknownColor = new Color(0.16f, 0.18f, 0.24f, 0.42f);
        [SerializeField] private Color discoveredColor = new Color(0.25f, 0.34f, 0.45f, 0.9f);
        [SerializeField] private Color clearedColor = new Color(0.54f, 0.98f, 0.72f, 1f);
        [SerializeField] private Color currentColor = new Color(0.36f, 0.92f, 1f, 1f);
        [SerializeField] private Color eliteColor = new Color(1f, 0.44f, 0.56f, 1f);
        [SerializeField] private Color rewardColor = new Color(1f, 0.82f, 0.28f, 1f);
        [SerializeField] private Color restColor = new Color(0.42f, 1f, 0.76f, 1f);
        [SerializeField] private Color bossColor = new Color(1f, 0.18f, 0.34f, 1f);
        [SerializeField] private Color combatTypeColor = new Color(0.52f, 0.7f, 0.9f, 1f);
        [SerializeField] private Color unknownTypeColor = new Color(0.45f, 0.5f, 0.6f, 0.5f);
        [SerializeField] private Color activeLineColor = new Color(0.38f, 0.75f, 0.9f, 0.85f);
        [SerializeField] private Color nextLineColor = new Color(0.36f, 0.92f, 1f, 0.7f);
        [SerializeField] private Color inactiveLineColor = new Color(0.16f, 0.2f, 0.28f, 0.36f);

        private readonly List<MinimapNode> nodes = new List<MinimapNode>();
        private readonly List<MinimapConnection> connections = new List<MinimapConnection>();
        private RectTransform mapContentRoot;
        private bool[] discoveredRooms;
        private bool[] clearedRooms;
        private int currentRoomIndex = -1;
        private bool runCleared;

        private void Awake()
        {
            if (runManager == null)
            {
                runManager = FindAnyObjectByType<RoomRunManager>();
            }

            if (dungeonGenerator == null)
            {
                dungeonGenerator = FindAnyObjectByType<TilemapDungeonGenerator>();
            }

            if (root == null)
            {
                root = GetComponentInChildren<CanvasGroup>(true);
            }

            if (findBindingsInChildren)
            {
                AutoBind();
            }

            if (buildFallbackUiIfMissing && (root == null || mapRoot == null))
            {
                BuildFallbackUi();
            }

            Canvas.ForceUpdateCanvases();
            EnsureMapViewport();
            RebuildMap();
            SyncFromRunManager();
            Refresh();
        }

        private void OnEnable()
        {
            if (runManager != null)
            {
                runManager.RunRoomStarted += HandleRunRoomStarted;
                runManager.RunRoomCleared += HandleRunRoomCleared;
                runManager.RunCleared += HandleRunCleared;
            }

            if (dungeonGenerator != null)
            {
                dungeonGenerator.DungeonGenerated += HandleDungeonGenerated;
            }
        }

        private void OnDisable()
        {
            if (runManager != null)
            {
                runManager.RunRoomStarted -= HandleRunRoomStarted;
                runManager.RunRoomCleared -= HandleRunRoomCleared;
                runManager.RunCleared -= HandleRunCleared;
            }

            if (dungeonGenerator != null)
            {
                dungeonGenerator.DungeonGenerated -= HandleDungeonGenerated;
            }
        }

        private void Update()
        {
            if (!pulseCurrentRoom || currentRoomIndex < 0 || currentRoomIndex >= nodes.Count)
            {
                return;
            }

            RectTransform current = nodes[currentRoomIndex].Rect;
            if (current == null)
            {
                return;
            }

            float pulse = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmount;
            current.localScale = Vector3.one * pulse;
        }

        private void HandleDungeonGenerated(IReadOnlyList<RectInt> rooms)
        {
            RebuildMap();
            SyncFromRunManager();
            Refresh();
        }

        private void HandleRunRoomStarted(int roomIndex, RoomDefinition room)
        {
            EnsureStateSize();
            currentRoomIndex = roomIndex;
            runCleared = false;

            MarkDiscovered(roomIndex);
            if (revealNextRoom)
            {
                MarkDiscovered(roomIndex + 1);
            }

            Refresh();
            SetVisible(true);
        }

        private void HandleRunRoomCleared(int roomIndex, RoomDefinition room)
        {
            EnsureStateSize();
            MarkDiscovered(roomIndex);

            if (roomIndex >= 0 && roomIndex < clearedRooms.Length)
            {
                clearedRooms[roomIndex] = true;
            }

            if (revealNextRoom)
            {
                MarkDiscovered(roomIndex + 1);
            }

            Refresh();
        }

        private void HandleRunCleared()
        {
            runCleared = true;

            if (currentRoomIndex >= 0 && currentRoomIndex < clearedRooms.Length)
            {
                clearedRooms[currentRoomIndex] = true;
            }

            Refresh();

            if (hideWhenRunCleared)
            {
                SetVisible(false);
            }
        }

        private void RebuildMap()
        {
            int roomCount = GetRoomCount();
            if (roomCount <= 0 || mapRoot == null)
            {
                ClearMap();
                EnsureStateSize();
                return;
            }

            EnsureStateSize();
            EnsureMapViewport();
            EnsureMapContentRoot();
            ClearMap();
            Vector2[] positions = BuildNodePositions(roomCount);

            for (int i = 0; i < roomCount - 1; i++)
            {
                connections.Add(CreateConnection(i, positions[i], positions[i + 1]));
            }

            for (int i = 0; i < roomCount; i++)
            {
                nodes.Add(CreateNode(i, positions[i]));
            }
        }

        private Vector2[] BuildNodePositions(int roomCount)
        {
            Vector2[] positions = new Vector2[roomCount];

            switch (layoutMode)
            {
                case MinimapLayoutMode.StableProgression:
                    return BuildStableProgressionPositions(roomCount);
                case MinimapLayoutMode.StableDungeonSteps:
                    return BuildStableDungeonStepPositions(roomCount);
                case MinimapLayoutMode.Linear:
                    for (int i = 0; i < roomCount; i++)
                    {
                        positions[i] = BuildLinearPosition(i, roomCount);
                    }

                    return positions;
            }

            if (useDungeonLayout && dungeonGenerator != null && dungeonGenerator.GeneratedRooms.Count > 0)
            {
                IReadOnlyList<RectInt> rooms = dungeonGenerator.GeneratedRooms;
                int count = Mathf.Min(roomCount, rooms.Count);
                Vector2 min = rooms[0].center;
                Vector2 max = rooms[0].center;

                for (int i = 1; i < count; i++)
                {
                    Vector2 center = rooms[i].center;
                    min = Vector2.Min(min, center);
                    max = Vector2.Max(max, center);
                }

                if (preserveDungeonAspectRatio)
                {
                    return BuildAspectPreservedDungeonPositions(roomCount, rooms, count, min, max);
                }

                Vector2 usableSize = GetPositionLayoutSize();
                for (int i = 0; i < roomCount; i++)
                {
                    if (i >= rooms.Count)
                    {
                        positions[i] = BuildLinearPosition(i, roomCount);
                        continue;
                    }

                    Vector2 normalized = new Vector2(
                        Mathf.Approximately(max.x, min.x) ? 0.5f : Mathf.InverseLerp(min.x, max.x, rooms[i].center.x),
                        Mathf.Approximately(max.y, min.y) ? 0.5f : Mathf.InverseLerp(min.y, max.y, rooms[i].center.y));

                    positions[i] = new Vector2(
                        -usableSize.x * 0.5f + normalized.x * usableSize.x,
                        -usableSize.y * 0.5f + normalized.y * usableSize.y);
                }

                return positions;
            }

            for (int i = 0; i < roomCount; i++)
            {
                positions[i] = BuildLinearPosition(i, roomCount);
            }

            return positions;
        }

        private Vector2[] BuildStableProgressionPositions(int roomCount)
        {
            Vector2[] positions = new Vector2[roomCount];
            float spacingX = GetStableHorizontalSpacing();
            float spacingY = GetStableVerticalSpacing();
            float totalWidth = Mathf.Max(0, roomCount - 1) * spacingX;
            int lane = 0;

            for (int i = 0; i < roomCount; i++)
            {
                if (i > 0)
                {
                    lane = GetNextStableLane(i, lane);
                }

                positions[i] = new Vector2(i * spacingX - totalWidth * 0.5f, lane * spacingY);
            }

            return positions;
        }

        private Vector2[] BuildStableDungeonStepPositions(int roomCount)
        {
            Vector2[] positions = new Vector2[roomCount];
            if (dungeonGenerator == null || dungeonGenerator.GeneratedRooms.Count <= 0)
            {
                for (int i = 0; i < roomCount; i++)
                {
                    positions[i] = BuildLinearPosition(i, roomCount);
                }

                return positions;
            }

            IReadOnlyList<RectInt> rooms = dungeonGenerator.GeneratedRooms;
            int count = Mathf.Min(roomCount, rooms.Count);
            float spacingX = GetStableHorizontalSpacing();
            float spacingY = GetStableVerticalSpacing();

            for (int i = 1; i < count; i++)
            {
                Vector2 delta = rooms[i].center - rooms[i - 1].center;
                Vector2 step;
                if (Mathf.Abs(delta.y) > Mathf.Abs(delta.x) * stableVerticalTurnThreshold)
                {
                    step = new Vector2(0f, Mathf.Sign(delta.y) * spacingY);
                }
                else
                {
                    step = new Vector2(Mathf.Sign(Mathf.Approximately(delta.x, 0f) ? 1f : delta.x) * spacingX, 0f);
                }

                positions[i] = positions[i - 1] + step;
            }

            for (int i = count; i < roomCount; i++)
            {
                positions[i] = positions[i - 1] + new Vector2(spacingX, 0f);
            }

            CenterPositions(positions);
            return positions;
        }

        private int GetNextStableLane(int roomIndex, int currentLane)
        {
            if (dungeonGenerator == null || roomIndex <= 0 || roomIndex >= dungeonGenerator.GeneratedRooms.Count)
            {
                return currentLane;
            }

            IReadOnlyList<RectInt> rooms = dungeonGenerator.GeneratedRooms;
            Vector2 delta = rooms[roomIndex].center - rooms[roomIndex - 1].center;
            if (Mathf.Abs(delta.y) <= Mathf.Abs(delta.x) * stableVerticalTurnThreshold)
            {
                return currentLane;
            }

            int laneLimit = Mathf.Max(0, stableVerticalLaneLimit);
            return Mathf.Clamp(currentLane + (delta.y >= 0f ? 1 : -1), -laneLimit, laneLimit);
        }

        private static void CenterPositions(Vector2[] positions)
        {
            if (positions == null || positions.Length == 0)
            {
                return;
            }

            Vector2 min = positions[0];
            Vector2 max = positions[0];
            for (int i = 1; i < positions.Length; i++)
            {
                min = Vector2.Min(min, positions[i]);
                max = Vector2.Max(max, positions[i]);
            }

            Vector2 center = (min + max) * 0.5f;
            for (int i = 0; i < positions.Length; i++)
            {
                positions[i] -= center;
            }
        }

        private Vector2[] BuildAspectPreservedDungeonPositions(
            int roomCount,
            IReadOnlyList<RectInt> rooms,
            int generatedRoomCount,
            Vector2 min,
            Vector2 max)
        {
            Vector2[] positions = new Vector2[roomCount];
            Vector2 usableSize = GetPositionLayoutSize();
            Vector2 dungeonSize = max - min;
            Vector2 dungeonCenter = (min + max) * 0.5f;

            float widthScale = dungeonSize.x <= 0.001f ? float.PositiveInfinity : usableSize.x / dungeonSize.x;
            float heightScale = dungeonSize.y <= 0.001f ? float.PositiveInfinity : usableSize.y / dungeonSize.y;
            float scale = Mathf.Min(widthScale, heightScale);

            if (float.IsInfinity(scale) || scale <= 0.001f)
            {
                scale = linearNodeSpacing;
            }

            for (int i = 0; i < roomCount; i++)
            {
                if (i >= generatedRoomCount)
                {
                    positions[i] = BuildLinearPosition(i, roomCount);
                    continue;
                }

                positions[i] = (rooms[i].center - dungeonCenter) * scale;
            }

            return positions;
        }

        private Vector2 BuildLinearPosition(int index, int roomCount)
        {
            float spacing = GetSafeLinearNodeSpacing(roomCount);
            float totalWidth = Mathf.Max(0, roomCount - 1) * spacing;
            return new Vector2(index * spacing - totalWidth * 0.5f, 0f);
        }

        private float GetStableHorizontalSpacing()
        {
            return stableNodeSpacing.x > 0.01f ? stableNodeSpacing.x : linearNodeSpacing;
        }

        private float GetStableVerticalSpacing()
        {
            return stableNodeSpacing.y > 0.01f ? stableNodeSpacing.y : linearNodeSpacing * 0.75f;
        }

        private MinimapConnection CreateConnection(int index, Vector2 from, Vector2 to)
        {
            if (!useOrthogonalConnectors || Mathf.Approximately(from.x, to.x) || Mathf.Approximately(from.y, to.y))
            {
                return new MinimapConnection(CreateLineSegment(index, 0, from, to));
            }

            Vector2 corner = new Vector2(to.x, from.y);
            Image first = CreateLineSegment(index, 0, from, corner);
            Image second = CreateLineSegment(index, 1, corner, to);
            return new MinimapConnection(first, second);
        }

        private Image CreateLineSegment(int connectionIndex, int segmentIndex, Vector2 from, Vector2 to)
        {
            GameObject lineObject = CreateUiObject($"Minimap Line {connectionIndex}-{segmentIndex}", mapContentRoot);
            RectTransform rect = lineObject.GetComponent<RectTransform>();
            Vector2 delta = to - from;
            rect.anchoredPosition = (from + to) * 0.5f;
            rect.localRotation = Quaternion.identity;
            rect.sizeDelta = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                ? new Vector2(Mathf.Max(lineThickness, Mathf.Abs(delta.x)), lineThickness)
                : new Vector2(lineThickness, Mathf.Max(lineThickness, Mathf.Abs(delta.y)));

            Image image = lineObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.color = inactiveLineColor;
            return image;
        }

        private MinimapNode CreateNode(int index, Vector2 position)
        {
            GameObject nodeObject = CreateUiObject($"Minimap Room {index + 1}", mapContentRoot);
            RectTransform rect = nodeObject.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = Vector2.one * nodeSize;

            Image image = nodeObject.AddComponent<Image>();
            image.raycastTarget = false;

            RoomDefinition room = GetRoomDefinition(index);
            Image marker = CreateNodeTypeMarker(rect);
            TextMeshProUGUI glyph = CreateNodeTypeGlyph(rect);
            if (room != null && room.RoomType == RoomType.Reward)
            {
                rect.localRotation = Quaternion.Euler(0f, 0f, 45f);
                SetCounterRotation(glyph, rect);
            }
            else if (room != null && room.RoomType == RoomType.Boss)
            {
                rect.sizeDelta = Vector2.one * (nodeSize * 1.22f);
            }

            return new MinimapNode(rect, image, marker, glyph);
        }

        private Image CreateNodeTypeMarker(RectTransform parent)
        {
            GameObject markerObject = CreateUiObject("Type Marker", parent);
            RectTransform rect = markerObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(nodeSize * 0.72f, Mathf.Max(1f, typeMarkerHeight));

            Image marker = markerObject.AddComponent<Image>();
            marker.raycastTarget = false;
            marker.color = combatTypeColor;
            return marker;
        }

        private TextMeshProUGUI CreateNodeTypeGlyph(RectTransform parent)
        {
            TextMeshProUGUI glyph = CreateText(parent, "Type Glyph", string.Empty, Mathf.Max(1f, typeGlyphSize), FontStyles.Bold);
            RectTransform rect = glyph.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            glyph.alignment = TextAlignmentOptions.Center;
            glyph.raycastTarget = false;
            return glyph;
        }

        private static void SetCounterRotation(TextMeshProUGUI glyph, RectTransform parent)
        {
            if (glyph == null || parent == null)
            {
                return;
            }

            glyph.rectTransform.localRotation = Quaternion.Inverse(parent.localRotation);
        }

        private void Refresh()
        {
            EnsureStateSize();

            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Rect != null && i != currentRoomIndex)
                {
                    nodes[i].Rect.localScale = Vector3.one;
                }

                bool visible = IsRoomVisible(i);
                if (nodes[i].Rect != null)
                {
                    nodes[i].Rect.gameObject.SetActive(visible);
                }

                if (nodes[i].Image != null)
                {
                    nodes[i].Image.color = GetNodeColor(i);
                }

                UpdateNodeTypeVisual(i);
            }

            for (int i = 0; i < connections.Count; i++)
            {
                bool visible = showFutureRooms || IsRoomVisible(i) || IsRoomVisible(i + 1);
                connections[i].SetVisible(visible);
                connections[i].SetColor(GetLineColor(i));
            }

            if (titleText != null)
            {
                titleText.text = runCleared ? "RUN CLEAR" : "MINIMAP";
            }

            CenterMapOnCurrentRoom();
        }

        private Color GetNodeColor(int index)
        {
            RoomDefinition room = GetRoomDefinition(index);
            bool discovered = index >= 0 && index < discoveredRooms.Length && discoveredRooms[index];
            bool cleared = index >= 0 && index < clearedRooms.Length && clearedRooms[index];

            if (index == currentRoomIndex && !runCleared)
            {
                return currentColor;
            }

            if (cleared)
            {
                return clearedColor;
            }

            if (!discovered)
            {
                return unknownColor;
            }

            if (room == null)
            {
                return discoveredColor;
            }

            switch (room.RoomType)
            {
                case RoomType.Elite:
                    return eliteColor;
                case RoomType.Reward:
                    return rewardColor;
                case RoomType.Rest:
                    return restColor;
                case RoomType.Boss:
                    return bossColor;
                default:
                    return discoveredColor;
            }
        }

        private void UpdateNodeTypeVisual(int index)
        {
            if (index < 0 || index >= nodes.Count)
            {
                return;
            }

            bool discovered = index >= 0 && index < discoveredRooms.Length && discoveredRooms[index];
            bool visibleType = showFutureRooms || discovered;
            RoomDefinition room = GetRoomDefinition(index);
            Color typeColor = visibleType ? GetRoomTypeColor(room) : unknownTypeColor;
            string glyph = visibleType ? GetRoomTypeGlyph(room) : "?";

            if (nodes[index].TypeMarker != null)
            {
                nodes[index].TypeMarker.gameObject.SetActive(typeMarkerHeight > 0.01f);
                nodes[index].TypeMarker.color = typeColor;
            }

            if (nodes[index].Glyph != null)
            {
                nodes[index].Glyph.gameObject.SetActive(typeGlyphSize > 0.01f);
                nodes[index].Glyph.text = glyph;
                nodes[index].Glyph.color = typeColor;
            }
        }

        private Color GetRoomTypeColor(RoomDefinition room)
        {
            if (room == null)
            {
                return unknownTypeColor;
            }

            switch (room.RoomType)
            {
                case RoomType.Elite:
                    return eliteColor;
                case RoomType.Reward:
                    return rewardColor;
                case RoomType.Rest:
                    return restColor;
                case RoomType.Boss:
                    return bossColor;
                default:
                    return combatTypeColor;
            }
        }

        private static string GetRoomTypeGlyph(RoomDefinition room)
        {
            if (room == null)
            {
                return "?";
            }

            switch (room.RoomType)
            {
                case RoomType.Elite:
                    return "!";
                case RoomType.Reward:
                    return "*";
                case RoomType.Rest:
                    return "+";
                case RoomType.Boss:
                    return "B";
                default:
                    return "";
            }
        }

        private bool IsLineActive(int lineIndex)
        {
            if (lineIndex < 0 || lineIndex >= clearedRooms.Length)
            {
                return false;
            }

            return clearedRooms[lineIndex] || currentRoomIndex > lineIndex;
        }

        private Color GetLineColor(int lineIndex)
        {
            if (IsLineActive(lineIndex))
            {
                return activeLineColor;
            }

            if (lineIndex == currentRoomIndex)
            {
                return nextLineColor;
            }

            return inactiveLineColor;
        }

        private bool IsRoomVisible(int index)
        {
            if (showFutureRooms)
            {
                return true;
            }

            return index >= 0 && index < discoveredRooms.Length && discoveredRooms[index];
        }

        private void SyncFromRunManager()
        {
            EnsureStateSize();

            currentRoomIndex = runManager != null ? runManager.CurrentRoomIndex : -1;
            for (int i = 0; i < discoveredRooms.Length; i++)
            {
                discoveredRooms[i] = currentRoomIndex >= 0 && i <= currentRoomIndex;
                clearedRooms[i] = currentRoomIndex > i;
            }

            if (revealNextRoom)
            {
                MarkDiscovered(currentRoomIndex + 1);
            }
        }

        private void MarkDiscovered(int roomIndex)
        {
            if (roomIndex >= 0 && roomIndex < discoveredRooms.Length)
            {
                discoveredRooms[roomIndex] = true;
            }
        }

        private void EnsureStateSize()
        {
            int roomCount = GetRoomCount();
            if (discoveredRooms != null && discoveredRooms.Length == roomCount)
            {
                return;
            }

            discoveredRooms = new bool[roomCount];
            clearedRooms = new bool[roomCount];
        }

        private int GetRoomCount()
        {
            if (runManager != null && runManager.TotalRoomCount > 0)
            {
                return runManager.TotalRoomCount;
            }

            return dungeonGenerator != null ? dungeonGenerator.RoomCount : 0;
        }

        private RoomDefinition GetRoomDefinition(int index)
        {
            return runManager != null ? runManager.GetRoomDefinition(index) : null;
        }

        private Vector2 GetMapSize()
        {
            if (mapRoot == null)
            {
                return fallbackMapSize;
            }

            Rect rect = mapRoot.rect;
            return new Vector2(
                rect.width > 1f ? rect.width : fallbackMapSize.x,
                rect.height > 1f ? rect.height : fallbackMapSize.y);
        }

        private void EnsureMapRootSize()
        {
            if (mapRoot == null)
            {
                return;
            }

            Rect rect = mapRoot.rect;
            if (rect.width > 1f && rect.height > 1f)
            {
                return;
            }

            mapRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(1f, fallbackMapSize.x));
            mapRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(1f, fallbackMapSize.y));
        }

        private Vector2 GetUsableMapSize(Vector2 mapSize)
        {
            return new Vector2(
                Mathf.Max(1f, mapSize.x - mapPadding.x * 2f),
                Mathf.Max(1f, mapSize.y - mapPadding.y * 2f));
        }

        private Vector2 GetPositionLayoutSize()
        {
            return GetUsableMapSize(GetMapSize());
        }

        private float GetSafeLinearNodeSpacing(int roomCount)
        {
            return linearNodeSpacing;
        }

        private void CenterMapOnCurrentRoom()
        {
            if (!centerCurrentRoom || mapContentRoot == null)
            {
                return;
            }

            if (currentRoomIndex < 0 || currentRoomIndex >= nodes.Count || nodes[currentRoomIndex].Rect == null)
            {
                mapContentRoot.anchoredPosition = Vector2.zero;
                return;
            }

            mapContentRoot.anchoredPosition = -nodes[currentRoomIndex].Rect.anchoredPosition;
        }

        private void ClearMap()
        {
            nodes.Clear();
            connections.Clear();

            if (mapContentRoot == null)
            {
                return;
            }

            for (int i = mapContentRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = mapContentRoot.GetChild(i);
                if (!IsGeneratedMapChild(child))
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private bool IsGeneratedMapChild(Transform child)
        {
            if (child == null)
            {
                return false;
            }

            return child == mapContentRoot
                || child.name.StartsWith("Minimap Line")
                || child.name.StartsWith("Minimap Room");
        }

        private static bool IsGeneratedMapContentName(string objectName)
        {
            return objectName.StartsWith("Generated Minimap Content");
        }

        private static bool IsMapContentLikeName(string objectName)
        {
            string lowerName = objectName.ToLowerInvariant();
            return IsGeneratedMapContentName(objectName)
                || lowerName.Contains("content")
                || lowerName.Contains("node root")
                || lowerName.Contains("room root");
        }

        private void EnsureMapViewport()
        {
            if (mapRoot == null)
            {
                return;
            }

            if (IsMapContentLikeName(mapRoot.name) && mapRoot.parent is RectTransform parentRect)
            {
                mapRoot = parentRect;
            }

            EnsureMapRootSize();

            RectMask2D mask = mapRoot.GetComponent<RectMask2D>();
            if (!clipMapToRoot)
            {
                if (mask != null)
                {
                    mask.enabled = false;
                }

                return;
            }

            if (mask == null)
            {
                mask = mapRoot.gameObject.AddComponent<RectMask2D>();
            }

            mask.enabled = true;
            float clipInset = Mathf.Max(0f, edgeSafePadding);
            mask.padding = new Vector4(clipInset, clipInset, clipInset, clipInset);
        }

        private void EnsureMapContentRoot()
        {
            if (mapRoot == null)
            {
                return;
            }

            if (mapContentRoot != null && mapContentRoot.parent == mapRoot)
            {
                return;
            }

            Transform existing = mapRoot.Find("Generated Minimap Content");
            if (existing != null)
            {
                mapContentRoot = existing as RectTransform;
                if (mapContentRoot != null)
                {
                    SetupMapContentRoot();
                    return;
                }
            }

            GameObject contentObject = CreateUiObject("Generated Minimap Content", mapRoot);
            mapContentRoot = contentObject.GetComponent<RectTransform>();
            SetupMapContentRoot();
        }

        private void SetupMapContentRoot()
        {
            if (mapContentRoot == null)
            {
                return;
            }

            mapContentRoot.anchorMin = new Vector2(0.5f, 0.5f);
            mapContentRoot.anchorMax = new Vector2(0.5f, 0.5f);
            mapContentRoot.pivot = new Vector2(0.5f, 0.5f);
            mapContentRoot.sizeDelta = GetMapSize();
            mapContentRoot.localScale = Vector3.one;
            mapContentRoot.localRotation = Quaternion.identity;
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

        private void AutoBind()
        {
            if (mapRoot == null)
            {
                RectTransform[] rects = GetComponentsInChildren<RectTransform>(true);
                for (int i = 0; i < rects.Length; i++)
                {
                    string lowerName = rects[i].name.ToLowerInvariant();
                    if (rects[i].transform == transform)
                    {
                        continue;
                    }

                    if (lowerName == "map root"
                        || lowerName == "minimap map root"
                        || lowerName.Contains("map viewport")
                        || lowerName.Contains("minimap viewport"))
                    {
                        mapRoot = rects[i];
                        break;
                    }
                }

                if (mapRoot == null)
                {
                    for (int i = 0; i < rects.Length; i++)
                    {
                        string lowerName = rects[i].name.ToLowerInvariant();
                        if (rects[i].transform != transform
                            && lowerName.Contains("map")
                            && !lowerName.Contains("content")
                            && !lowerName.Contains("node")
                            && !lowerName.Contains("room")
                            && !lowerName.Contains("canvas")
                            && !lowerName.Contains("panel")
                            && !lowerName.Contains("title"))
                        {
                            mapRoot = rects[i];
                            break;
                        }
                    }
                }
            }

            if (titleText == null)
            {
                TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);
                for (int i = 0; i < texts.Length; i++)
                {
                    string lowerName = texts[i].name.ToLowerInvariant();
                    if (lowerName.Contains("title") || lowerName.Contains("minimap"))
                    {
                        titleText = texts[i];
                        break;
                    }
                }
            }
        }

        private void BuildFallbackUi()
        {
            GameObject canvasObject = new GameObject("Minimap Canvas");
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 220;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject rootObject = CreateUiObject("Minimap Root", canvasObject.transform);
            root = rootObject.AddComponent<CanvasGroup>();

            RectTransform rootRect = rootObject.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(1f, 1f);
            rootRect.anchorMax = new Vector2(1f, 1f);
            rootRect.pivot = new Vector2(1f, 1f);
            rootRect.anchoredPosition = new Vector2(-28f, -28f);
            rootRect.sizeDelta = new Vector2(310f, 230f);

            Image panel = rootObject.AddComponent<Image>();
            panel.raycastTarget = false;
            panel.color = new Color(0.025f, 0.03f, 0.045f, 0.72f);

            VerticalLayoutGroup layout = rootObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 14, 16);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            titleText = CreateText(rootObject.transform, "Minimap Title", "MINIMAP", 18f, FontStyles.Bold);
            AddLayout(titleText.gameObject, 274f, 24f);

            GameObject mapObject = CreateUiObject("Minimap Map Root", rootObject.transform);
            mapRoot = mapObject.GetComponent<RectTransform>();
            mapRoot.sizeDelta = fallbackMapSize;
            AddLayout(mapObject, fallbackMapSize.x, fallbackMapSize.y);
        }

        private static TextMeshProUGUI CreateText(Transform parent, string objectName, string value, float size, FontStyles style)
        {
            GameObject textObject = CreateUiObject(objectName, parent);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.color = new Color(0.86f, 0.95f, 1f, 1f);
            return text;
        }

        private static void AddLayout(GameObject target, float preferredWidth, float preferredHeight)
        {
            LayoutElement layout = target.AddComponent<LayoutElement>();
            layout.preferredWidth = preferredWidth;
            layout.preferredHeight = preferredHeight;
        }

        private static GameObject CreateUiObject(string objectName, Transform parent)
        {
            GameObject uiObject = new GameObject(objectName);
            uiObject.transform.SetParent(parent, false);
            uiObject.AddComponent<RectTransform>();
            return uiObject;
        }

        private readonly struct MinimapNode
        {
            public readonly RectTransform Rect;
            public readonly Image Image;
            public readonly Image TypeMarker;
            public readonly TextMeshProUGUI Glyph;

            public MinimapNode(RectTransform rect, Image image, Image typeMarker, TextMeshProUGUI glyph)
            {
                Rect = rect;
                Image = image;
                TypeMarker = typeMarker;
                Glyph = glyph;
            }
        }

        private readonly struct MinimapConnection
        {
            private readonly Image first;
            private readonly Image second;

            public MinimapConnection(Image first)
            {
                this.first = first;
                second = null;
            }

            public MinimapConnection(Image first, Image second)
            {
                this.first = first;
                this.second = second;
            }

            public void SetVisible(bool visible)
            {
                if (first != null)
                {
                    first.gameObject.SetActive(visible);
                }

                if (second != null)
                {
                    second.gameObject.SetActive(visible);
                }
            }

            public void SetColor(Color color)
            {
                if (first != null)
                {
                    first.color = color;
                }

                if (second != null)
                {
                    second.color = color;
                }
            }
        }
    }
}
