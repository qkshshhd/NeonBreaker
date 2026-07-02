using NeonBreaker.Combat;
using NeonBreaker.Enemies;
using NeonBreaker.Rooms;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeonBreaker.UI
{
    public sealed class BossHealthBarUI : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private RoomRunManager runManager;
        [SerializeField] private RoomManager roomManager;

        [Header("Bindings")]
        [SerializeField] private CanvasGroup root;
        [SerializeField] private Image fill;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI valueText;

        [Header("Options")]
        [SerializeField] private bool findBindingsInChildren = true;
        [SerializeField] private bool buildFallbackUiIfMissing = true;
        [SerializeField] private bool hideWhenNotBossRoom = true;
        [SerializeField] private string fallbackBossName = "Neon Core";

        private RoomDefinition currentRoom;
        private Health bossHealth;
        private RectTransform fillRect;
        private Vector2 fillFullAnchorMax;
        private Vector2 fillFullSizeDelta;
        private bool fillRectInitialized;

        private bool IsBossRoom => currentRoom != null && currentRoom.RoomType == RoomType.Boss;

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

            if (findBindingsInChildren)
            {
                AutoBind();
            }

            if (buildFallbackUiIfMissing && (root == null || fill == null))
            {
                BuildFallbackUi();
            }

            InitializeFillRect();
            Hide();
        }

        private void OnEnable()
        {
            if (runManager != null)
            {
                runManager.RunRoomStarted += HandleRunRoomStarted;
                runManager.RunRoomCleared += HandleRunRoomCleared;
                runManager.RunCleared += HandleRunCleared;
            }

            if (roomManager != null)
            {
                roomManager.EnemySpawned += HandleEnemySpawned;
                roomManager.RoomCleared += HandleRoomCleared;
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

            if (roomManager != null)
            {
                roomManager.EnemySpawned -= HandleEnemySpawned;
                roomManager.RoomCleared -= HandleRoomCleared;
            }

            UnbindHealth();
        }

        private void HandleRunRoomStarted(int roomIndex, RoomDefinition room)
        {
            currentRoom = room;
            UnbindHealth();

            if (hideWhenNotBossRoom || IsBossRoom)
            {
                Hide();
            }
        }

        private void HandleEnemySpawned(IRoomEnemy enemy)
        {
            if (!IsBossRoom || bossHealth != null)
            {
                return;
            }

            Component enemyComponent = enemy as Component;
            if (enemyComponent == null)
            {
                return;
            }

            BossEnemyBehavior bossBehavior = enemyComponent.GetComponent<BossEnemyBehavior>();
            if (bossBehavior == null)
            {
                bossBehavior = enemyComponent.GetComponentInChildren<BossEnemyBehavior>(true);
            }

            if (bossBehavior == null)
            {
                return;
            }

            Health health = enemyComponent.GetComponent<Health>();
            if (health == null)
            {
                health = enemyComponent.GetComponentInChildren<Health>(true);
            }

            BindHealth(health, enemyComponent.gameObject.name);
        }

        private void HandleRunRoomCleared(int roomIndex, RoomDefinition room)
        {
            if (room != null && room.RoomType == RoomType.Boss)
            {
                Hide();
            }
        }

        private void HandleRoomCleared(RoomDefinition room)
        {
            if (room != null && room.RoomType == RoomType.Boss)
            {
                Hide();
            }
        }

        private void HandleRunCleared()
        {
            Hide();
        }

        private void BindHealth(Health health, string bossName)
        {
            UnbindHealth();

            bossHealth = health;
            if (bossHealth == null)
            {
                Hide();
                return;
            }

            InitializeFillRect();
            bossHealth.HealthChanged += HandleBossHealthChanged;
            SetText(nameText, string.IsNullOrWhiteSpace(bossName) ? fallbackBossName : bossName);
            HandleBossHealthChanged(bossHealth.CurrentHealth, bossHealth.MaxHealth);
            Show();
        }

        private void UnbindHealth()
        {
            if (bossHealth != null)
            {
                bossHealth.HealthChanged -= HandleBossHealthChanged;
                bossHealth = null;
            }
        }

        private void HandleBossHealthChanged(float current, float max)
        {
            float ratio = max <= 0f ? 0f : Mathf.Clamp01(current / max);
            if (fill != null)
            {
                fill.fillAmount = ratio;
                UpdateFillRect(ratio);
            }

            SetText(valueText, $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}");

            if (ratio <= 0f)
            {
                Hide();
            }
        }

        private void Show()
        {
            SetVisible(true);
        }

        private void Hide()
        {
            SetVisible(false);
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
            if (fill == null)
            {
            Image[] images = GetComponentsInChildren<Image>(true);
                for (int i = 0; i < images.Length; i++)
                {
                    string lowerName = images[i].name.ToLowerInvariant();
                    if (lowerName.Contains("fill"))
                    {
                        fill = images[i];
                        break;
                    }
                }

                if (fill == null)
                {
                    for (int i = 0; i < images.Length; i++)
                    {
                        string lowerName = images[i].name.ToLowerInvariant();
                        if (lowerName.Contains("bar"))
                        {
                            fill = images[i];
                            break;
                        }
                    }
                }
            }

            TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                string lowerName = texts[i].name.ToLowerInvariant();
                if (nameText == null && (lowerName.Contains("name") || lowerName.Contains("title")))
                {
                    nameText = texts[i];
                }
                else if (valueText == null && (lowerName.Contains("value") || lowerName.Contains("hp") || lowerName.Contains("health")))
                {
                    valueText = texts[i];
                }
            }
        }

        private void BuildFallbackUi()
        {
            GameObject canvasObject = new GameObject("Boss Health Canvas");
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 350;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject rootObject = CreateUiObject("Boss Health Root", canvasObject.transform);
            root = rootObject.AddComponent<CanvasGroup>();

            RectTransform rootRect = rootObject.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.anchoredPosition = new Vector2(0f, -34f);
            rootRect.sizeDelta = new Vector2(900f, 86f);

            VerticalLayoutGroup layout = rootObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            nameText = CreateText(rootObject.transform, "Boss Name", fallbackBossName, 26f, FontStyles.Bold, TextAlignmentOptions.Center);
            AddLayout(nameText.gameObject, 900f, 30f);

            GameObject barObject = CreateUiObject("Boss Health Bar", rootObject.transform);
            AddLayout(barObject, 900f, 24f);

            Image background = barObject.AddComponent<Image>();
            background.color = new Color(0.05f, 0.055f, 0.075f, 0.92f);

            GameObject fillObject = CreateUiObject("Boss Health Fill", barObject.transform);
            RectTransform fallbackFillRect = fillObject.GetComponent<RectTransform>();
            fallbackFillRect.anchorMin = Vector2.zero;
            fallbackFillRect.anchorMax = Vector2.one;
            fallbackFillRect.offsetMin = Vector2.zero;
            fallbackFillRect.offsetMax = Vector2.zero;

            fill = fillObject.AddComponent<Image>();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.color = new Color(1f, 0.18f, 0.28f, 1f);
            fillRect = fallbackFillRect;
            fillFullAnchorMax = fallbackFillRect.anchorMax;
            fillFullSizeDelta = fallbackFillRect.sizeDelta;
            fillRectInitialized = true;

            valueText = CreateText(rootObject.transform, "Boss Health Value", "0 / 0", 18f, FontStyles.Normal, TextAlignmentOptions.Center);
            AddLayout(valueText.gameObject, 900f, 22f);
        }

        private void InitializeFillRect()
        {
            if (fill == null)
            {
                return;
            }

            fillRect = fill.rectTransform;
            fillFullAnchorMax = fillRect.anchorMax;
            fillFullSizeDelta = fillRect.sizeDelta;
            fillRectInitialized = true;

            if (fill.type != Image.Type.Filled)
            {
                fill.type = Image.Type.Simple;
            }
        }

        private void UpdateFillRect(float ratio)
        {
            if (!fillRectInitialized || fillRect == null)
            {
                InitializeFillRect();
            }

            if (fillRect == null)
            {
                return;
            }

            if (fill.type == Image.Type.Filled)
            {
                return;
            }

            if (Mathf.Abs(fillFullAnchorMax.x - fillRect.anchorMin.x) > 0.0001f)
            {
                fillRect.anchorMax = new Vector2(
                    Mathf.Lerp(fillRect.anchorMin.x, fillFullAnchorMax.x, ratio),
                    fillFullAnchorMax.y);
                return;
            }

            fillRect.sizeDelta = new Vector2(fillFullSizeDelta.x * ratio, fillFullSizeDelta.y);
        }

        private static GameObject CreateUiObject(string objectName, Transform parent)
        {
            GameObject uiObject = new GameObject(objectName);
            uiObject.transform.SetParent(parent, false);
            uiObject.AddComponent<RectTransform>();
            return uiObject;
        }

        private static TextMeshProUGUI CreateText(
            Transform parent,
            string objectName,
            string value,
            float size,
            FontStyles style,
            TextAlignmentOptions alignment)
        {
            GameObject textObject = CreateUiObject(objectName, parent);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.color = new Color(0.95f, 0.98f, 1f, 1f);
            return text;
        }

        private static void AddLayout(GameObject target, float preferredWidth, float preferredHeight)
        {
            LayoutElement layout = target.AddComponent<LayoutElement>();
            layout.preferredWidth = preferredWidth;
            layout.preferredHeight = preferredHeight;
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
