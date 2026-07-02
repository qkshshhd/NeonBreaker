using NeonBreaker.Combat;
using NeonBreaker.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeonBreaker.UI
{
    public sealed class PlayerStatsPanelUI : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private PlayerController player;
        [SerializeField] private PlayerStats stats;
        [SerializeField] private Health health;
        [SerializeField] private PlayerDefinition definition;

        [Header("Bindings")]
        [SerializeField] private CanvasGroup root;
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private TextMeshProUGUI damageText;
        [SerializeField] private TextMeshProUGUI attackText;
        [SerializeField] private TextMeshProUGUI moveText;
        [SerializeField] private TextMeshProUGUI dashText;
        [SerializeField] private TextMeshProUGUI critText;
        [SerializeField] private TextMeshProUGUI skillText;
        [SerializeField] private TextMeshProUGUI shockwaveText;

        [Header("Options")]
        [SerializeField] private bool buildFallbackUiIfMissing = true;
        [SerializeField] private bool refreshEveryFrame = false;
        [SerializeField] private KeyCode toggleKey = KeyCode.Tab;
        [SerializeField] private bool startVisible = true;

        private bool visible;

        private void Awake()
        {
            ResolveSources();

            if (buildFallbackUiIfMissing && root == null)
            {
                BuildFallbackUi();
            }

            SetVisible(startVisible);
            Refresh();
        }

        private void OnEnable()
        {
            if (stats != null)
            {
                stats.StatsChanged += Refresh;
            }

            if (health != null)
            {
                health.HealthChanged += HandleHealthChanged;
            }
        }

        private void OnDisable()
        {
            if (stats != null)
            {
                stats.StatsChanged -= Refresh;
            }

            if (health != null)
            {
                health.HealthChanged -= HandleHealthChanged;
            }
        }

        private void Update()
        {
            if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
            {
                SetVisible(!visible);
            }

            if (refreshEveryFrame)
            {
                Refresh();
            }
        }

        public void Refresh()
        {
            ResolveSources();

            SetText(healthText, BuildHealthText());
            SetText(damageText, $"DMG {FormatMultiplier(stats != null ? stats.DamageMultiplier : 1f)}");
            SetText(attackText, BuildAttackText());
            SetText(moveText, BuildMoveText());
            SetText(dashText, BuildDashText());
            SetText(critText, BuildCritText());
            SetText(skillText, $"SKILL CD {FormatCooldownMultiplier(stats != null ? stats.SkillCooldownMultiplier : 1f)}");
            SetText(shockwaveText, BuildShockwaveText());
        }

        private void HandleHealthChanged(float current, float max)
        {
            SetText(healthText, BuildHealthText());
        }

        private string BuildHealthText()
        {
            if (health == null)
            {
                return "HP -";
            }

            return $"HP {Mathf.CeilToInt(health.CurrentHealth)} / {Mathf.CeilToInt(health.MaxHealth)}";
        }

        private string BuildAttackText()
        {
            if (stats == null)
            {
                return "ATK -";
            }

            return $"ATK CD {FormatCooldownMultiplier(stats.AttackCooldownMultiplier)}  RNG {FormatMultiplier(stats.AttackRangeMultiplier)}";
        }

        private string BuildMoveText()
        {
            if (stats == null)
            {
                return "MOVE -";
            }

            if (definition != null)
            {
                return $"MOVE {stats.GetMoveSpeed(definition.MoveSpeed):0.0}";
            }

            return $"MOVE {FormatMultiplier(stats.MoveSpeedMultiplier)}";
        }

        private string BuildDashText()
        {
            if (stats == null)
            {
                return "DASH -";
            }

            if (definition != null)
            {
                return $"DASH {stats.GetDashDistance(definition.DashDistance):0.0}  CD {stats.GetDashCooldown(definition.DashCooldown):0.00}s";
            }

            return $"DASH {FormatMultiplier(stats.DashDistanceMultiplier)}  CD {FormatCooldownMultiplier(stats.DashCooldownMultiplier)}";
        }

        private string BuildCritText()
        {
            if (stats == null)
            {
                return "CRIT -";
            }

            return $"CRIT {stats.CriticalChance * 100f:0}%  DMG x{stats.CriticalDamageMultiplier:0.00}";
        }

        private string BuildShockwaveText()
        {
            int level = stats != null ? stats.DashShockwaveLevel : 0;
            return level > 0 ? $"SHOCKWAVE Lv.{level}" : "SHOCKWAVE -";
        }

        private void ResolveSources()
        {
            if (player == null)
            {
                player = FindAnyObjectByType<PlayerController>();
            }

            if (stats == null)
            {
                stats = player != null ? player.Stats : FindAnyObjectByType<PlayerStats>();
            }

            if (health == null)
            {
                health = player != null ? player.Health : FindAnyObjectByType<Health>();
            }

            if (definition == null && player != null)
            {
                definition = player.Definition;
            }
        }

        private void SetVisible(bool isVisible)
        {
            visible = isVisible;

            if (root == null)
            {
                return;
            }

            root.alpha = visible ? 1f : 0f;
            root.interactable = false;
            root.blocksRaycasts = false;
        }

        private void BuildFallbackUi()
        {
            GameObject canvasObject = new GameObject("Player Stats Canvas");
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 230;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject rootObject = CreateUiObject("Player Stats Root", canvasObject.transform);
            root = rootObject.AddComponent<CanvasGroup>();

            RectTransform rootRect = rootObject.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(1f, 0f);
            rootRect.anchorMax = new Vector2(1f, 0f);
            rootRect.pivot = new Vector2(1f, 0f);
            rootRect.anchoredPosition = new Vector2(-24f, 24f);
            rootRect.sizeDelta = new Vector2(360f, 240f);

            Image panel = rootObject.AddComponent<Image>();
            panel.raycastTarget = false;
            panel.color = new Color(0.025f, 0.035f, 0.055f, 0.72f);

            VerticalLayoutGroup layout = rootObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 14, 14);
            layout.spacing = 5f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            healthText = CreateLine(rootObject.transform, "HP -", 21f, FontStyles.Bold, new Color(0.58f, 1f, 0.78f, 1f));
            damageText = CreateLine(rootObject.transform, "DMG x1.00", 18f, FontStyles.Normal);
            attackText = CreateLine(rootObject.transform, "ATK CD -", 18f, FontStyles.Normal);
            moveText = CreateLine(rootObject.transform, "MOVE -", 18f, FontStyles.Normal);
            dashText = CreateLine(rootObject.transform, "DASH -", 18f, FontStyles.Normal);
            critText = CreateLine(rootObject.transform, "CRIT -", 18f, FontStyles.Normal);
            skillText = CreateLine(rootObject.transform, "SKILL CD -", 18f, FontStyles.Normal);
            shockwaveText = CreateLine(rootObject.transform, "SHOCKWAVE -", 18f, FontStyles.Normal);
        }

        private static TextMeshProUGUI CreateLine(
            Transform parent,
            string value,
            float size,
            FontStyles style,
            Color? color = null)
        {
            GameObject textObject = CreateUiObject(value.Split(' ')[0], parent);
            LayoutElement layout = textObject.AddComponent<LayoutElement>();
            layout.preferredHeight = size + 8f;

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Left;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.color = color ?? new Color(0.86f, 0.94f, 1f, 0.92f);
            return text;
        }

        private static GameObject CreateUiObject(string objectName, Transform parent)
        {
            GameObject uiObject = new GameObject(objectName);
            uiObject.transform.SetParent(parent, false);
            uiObject.AddComponent<RectTransform>();
            return uiObject;
        }

        private static string FormatMultiplier(float value)
        {
            return $"x{value:0.00}";
        }

        private static string FormatCooldownMultiplier(float value)
        {
            float reduction = Mathf.Max(0f, 1f - value);
            return reduction > 0f ? $"-{reduction * 100f:0}%" : "x1.00";
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
