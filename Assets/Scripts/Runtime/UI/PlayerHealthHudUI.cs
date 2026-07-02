using NeonBreaker.Combat;
using NeonBreaker.Player;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace NeonBreaker.UI
{
    public sealed class PlayerHealthHudUI : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private PlayerController player;
        [SerializeField] private Health health;

        [Header("Bindings")]
        [SerializeField] private CanvasGroup root;
        [SerializeField] private Image healthFill;
        [SerializeField] private Image delayedFill;
        [SerializeField] private Image damageFlash;
        [FormerlySerializedAs("healthPulseRoot")]
        [SerializeField] private RectTransform healthPulseParent;
        [SerializeField] private TextMeshProUGUI valueText;
        [SerializeField] private TextMeshProUGUI labelText;

        [Header("Shield Bindings")]
        [SerializeField] private CanvasGroup shieldRoot;
        [SerializeField] private Image shieldFill;
        [SerializeField] private TextMeshProUGUI shieldValueText;

        [Header("Options")]
        [SerializeField] private bool findBindingsInChildren = true;
        [SerializeField] private bool buildFallbackUiIfMissing = true;
        [SerializeField] private bool hideWhenDead;
        [SerializeField] private bool showValueText = true;
        [SerializeField] private bool showShieldBesideHealthValue = true;
        [SerializeField] private bool hideShieldWhenEmpty = true;
        [SerializeField] private bool showShieldMaxValue = true;
        [SerializeField] private bool forceFilledBarImages;
        [SerializeField] private bool autoArrangeBarLayers;
        [SerializeField] private bool useScriptedColors;
        [SerializeField] private string label = "HP";
        [SerializeField] private string shieldLabel = "방어막";

        [Header("Animation")]
        [SerializeField, Min(0.01f)] private float fillLerpSpeed = 18f;
        [SerializeField, Min(0.01f)] private float shieldFillLerpSpeed = 22f;
        [SerializeField, Min(0f)] private float delayedFillHoldTime = 0.18f;
        [SerializeField, Min(0.01f)] private float delayedFillLerpSpeed = 1.8f;
        [SerializeField, Range(0f, 1f)] private float lowHealthThreshold = 0.3f;
        [SerializeField, Min(0.05f)] private float lowHealthPulseDuration = 0.72f;
        [SerializeField, Min(0f)] private float lowHealthPulseGap = 0.08f;
        [SerializeField, Range(0.01f, 0.5f)] private float lowHealthPulsePunchRatio = 0.12f;
        [SerializeField, Min(0f)] private float lowHealthPulsePunchExpandPixels = 4f;
        [FormerlySerializedAs("lowHealthPulseExpandPixels")]
        [SerializeField, Min(0f)] private float lowHealthPulseFinalExpandPixels = 18f;
        [SerializeField, Range(0f, 1f)] private float lowHealthPulseStartAlpha = 0.38f;
        [SerializeField, Min(0f)] private float damageFlashDuration = 0.16f;

        [Header("Colors")]
        [SerializeField] private Color healthyColor = new Color(0.24f, 0.95f, 1f, 1f);
        [SerializeField] private Color warningColor = new Color(1f, 0.78f, 0.16f, 1f);
        [SerializeField] private Color dangerColor = new Color(1f, 0.12f, 0.28f, 1f);
        [SerializeField] private Color delayedFillColor = new Color(1f, 1f, 1f, 0.45f);
        [SerializeField] private Color flashColor = new Color(1f, 0.08f, 0.18f, 0.45f);
        [SerializeField] private Color shieldTextColor = new Color(0.25f, 0.9f, 1f, 1f);

        private float displayedRatio = 1f;
        private float delayedRatio = 1f;
        private float targetRatio = 1f;
        private float displayedShieldRatio;
        private float targetShieldRatio;
        private float currentHealthValue;
        private float maxHealthValue;
        private float currentShieldValue;
        private float maxShieldValue;
        private float delayedHoldTimer;
        private float damageFlashTimer;
        private RectTransform healthFillRect;
        private RectTransform delayedFillRect;
        private RectTransform shieldFillRect;
        private Vector2 healthFullAnchorMax;
        private Vector2 healthFullSizeDelta;
        private Vector2 delayedFullAnchorMax;
        private Vector2 delayedFullSizeDelta;
        private Vector2 shieldFullAnchorMax;
        private Vector2 shieldFullSizeDelta;
        private bool healthFillInitialized;
        private bool delayedFillInitialized;
        private bool builtFallbackUi;
        private Color damageFlashBaseColor;
        private Image healthPulseImage;
        private RectTransform healthPulseRect;
        private float lowHealthPulseTimer;
        private float lowHealthPulseCycleDuration;

        private void Awake()
        {
            ResolveSources();

            if (root == null)
            {
                root = GetComponentInChildren<CanvasGroup>(true);
            }

            if (findBindingsInChildren)
            {
                AutoBind();
            }

            bool hasCustomUiChildren = HasUiChildren();
            if (buildFallbackUiIfMissing && !hasCustomUiChildren && (root == null || healthFill == null))
            {
                BuildFallbackUi();
            }

            ArrangeBarLayers();
            InitializeFillRects();
            CacheCustomColors();
            BuildHealthPulseImage();
            ApplyStaticVisuals();
            SetVisible(true);
        }

        private void OnEnable()
        {
            BindHealth();
        }

        private void OnDisable()
        {
            UnbindHealth();
        }

        private void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;

            displayedRatio = Mathf.MoveTowards(
                displayedRatio,
                targetRatio,
                fillLerpSpeed * deltaTime);

            displayedShieldRatio = Mathf.MoveTowards(
                displayedShieldRatio,
                targetShieldRatio,
                shieldFillLerpSpeed * deltaTime);

            if (delayedHoldTimer > 0f)
            {
                delayedHoldTimer -= deltaTime;
            }
            else
            {
                delayedRatio = Mathf.MoveTowards(
                    delayedRatio,
                    targetRatio,
                    delayedFillLerpSpeed * deltaTime);
            }

            if (damageFlashTimer > 0f)
            {
                damageFlashTimer = Mathf.Max(0f, damageFlashTimer - deltaTime);
            }

            RefreshVisuals();
        }

        public void SetPlayer(PlayerController newPlayer)
        {
            if (player == newPlayer)
            {
                return;
            }

            UnbindHealth();
            player = newPlayer;
            health = player != null ? player.Health : null;
            BindHealth();
        }

        public void SetHealth(Health newHealth)
        {
            if (health == newHealth)
            {
                return;
            }

            UnbindHealth();
            health = newHealth;
            BindHealth();
        }

        private void ResolveSources()
        {
            if (player == null)
            {
                player = FindAnyObjectByType<PlayerController>();
            }

            if (health == null)
            {
                health = player != null ? player.Health : null;
            }
        }

        private void BindHealth()
        {
            ResolveSources();

            if (health == null)
            {
                SetVisible(false);
                return;
            }

            health.HealthChanged += HandleHealthChanged;
            health.ShieldChanged += HandleShieldChanged;
            health.Damaged += HandleDamaged;
            HandleHealthChanged(health.CurrentHealth, health.MaxHealth, true);
            HandleShieldChanged(health.CurrentShield, health.MaxShield, true);
        }

        private void UnbindHealth()
        {
            if (health == null)
            {
                return;
            }

            health.HealthChanged -= HandleHealthChanged;
            health.ShieldChanged -= HandleShieldChanged;
            health.Damaged -= HandleDamaged;
        }

        private void HandleHealthChanged(float current, float max)
        {
            HandleHealthChanged(current, max, false);
        }

        private void HandleHealthChanged(float current, float max, bool immediate)
        {
            currentHealthValue = current;
            maxHealthValue = max;
            targetRatio = max <= 0f ? 0f : Mathf.Clamp01(current / max);

            if (immediate)
            {
                displayedRatio = targetRatio;
                delayedRatio = targetRatio;
                delayedHoldTimer = 0f;
            }
            else if (targetRatio < delayedRatio)
            {
                delayedHoldTimer = delayedFillHoldTime;
            }
            else
            {
                delayedRatio = targetRatio;
                delayedHoldTimer = 0f;
            }

            SetText(labelText, label);
            RefreshValueText();

            SetVisible(!hideWhenDead || current > 0f);
            RefreshVisuals();
        }

        private void HandleShieldChanged(float current, float max)
        {
            HandleShieldChanged(current, max, false);
        }

        private void HandleShieldChanged(float current, float max, bool immediate)
        {
            currentShieldValue = current;
            maxShieldValue = max;
            targetShieldRatio = max <= 0f ? 0f : Mathf.Clamp01(current / max);
            if (immediate)
            {
                displayedShieldRatio = targetShieldRatio;
            }

            if (shieldValueText != null && !showShieldBesideHealthValue)
            {
                int currentValue = Mathf.CeilToInt(current);
                int maxValue = Mathf.CeilToInt(max);
                shieldValueText.text = showShieldMaxValue
                    ? $"{shieldLabel} {currentValue} / {maxValue}"
                    : $"{shieldLabel} {currentValue}";
            }
            else if (shieldValueText != null)
            {
                shieldValueText.text = string.Empty;
                shieldValueText.enabled = false;
            }

            RefreshValueText();
            SetShieldVisible(!hideShieldWhenEmpty || current > 0.001f);
            RefreshVisuals();
        }

        private void RefreshValueText()
        {
            if (valueText == null)
            {
                return;
            }

            if (!showValueText)
            {
                valueText.text = string.Empty;
                return;
            }

            string healthText = $"{Mathf.CeilToInt(currentHealthValue)} / {Mathf.CeilToInt(maxHealthValue)}";
            if (!showShieldBesideHealthValue || currentShieldValue <= 0.001f)
            {
                valueText.text = healthText;
                return;
            }

            string colorHex = ColorUtility.ToHtmlStringRGBA(shieldTextColor);
            valueText.text = $"{healthText} <color=#{colorHex}>(+{Mathf.CeilToInt(currentShieldValue)})</color>";
        }

        private void HandleDamaged(DamageInfo damage)
        {
            if (damage.Amount <= 0f)
            {
                return;
            }

            damageFlashTimer = damageFlashDuration;
        }

        private void RefreshVisuals()
        {
            UpdateFill(healthFill, healthFillRect, healthFullAnchorMax, healthFullSizeDelta, displayedRatio);
            UpdateFill(delayedFill, delayedFillRect, delayedFullAnchorMax, delayedFullSizeDelta, delayedRatio);
            UpdateFill(shieldFill, shieldFillRect, shieldFullAnchorMax, shieldFullSizeDelta, displayedShieldRatio);

            if (healthFill != null && ShouldUseScriptedColors())
            {
                healthFill.color = GetHealthColor(displayedRatio);
            }

            if (delayedFill != null && ShouldUseScriptedColors())
            {
                delayedFill.color = delayedFillColor;
            }

            UpdateDamageFlash();
            UpdateLowHealthPulse();
        }

        private void UpdateDamageFlash()
        {
            if (damageFlash == null)
            {
                return;
            }

            float alpha = damageFlashDuration <= 0f
                ? 0f
                : Mathf.Clamp01(damageFlashTimer / damageFlashDuration) * damageFlashBaseColor.a;

            Color color = damageFlashBaseColor;
            color.a = alpha;
            damageFlash.color = color;
            damageFlash.enabled = alpha > 0.001f;
        }

        private void UpdateLowHealthPulse()
        {
            if (healthPulseImage == null || healthFill == null)
            {
                return;
            }

            if (targetRatio <= 0f || targetRatio > lowHealthThreshold)
            {
                HideHealthPulseImage(true);
                return;
            }

            lowHealthPulseCycleDuration = lowHealthPulseDuration + lowHealthPulseGap;
            lowHealthPulseTimer += Time.unscaledDeltaTime;
            if (lowHealthPulseTimer >= lowHealthPulseCycleDuration)
            {
                lowHealthPulseTimer %= lowHealthPulseCycleDuration;
            }

            if (lowHealthPulseTimer > lowHealthPulseDuration)
            {
                HideHealthPulseImage(false);
                return;
            }

            float normalizedTime = Mathf.Clamp01(lowHealthPulseTimer / lowHealthPulseDuration);
            ApplyHealthPulseImage(normalizedTime);
        }

        private Color GetHealthColor(float ratio)
        {
            if (ratio <= lowHealthThreshold)
            {
                return Color.Lerp(dangerColor, warningColor, Mathf.InverseLerp(0f, lowHealthThreshold, ratio));
            }

            return Color.Lerp(warningColor, healthyColor, Mathf.InverseLerp(lowHealthThreshold, 1f, ratio));
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

        private void SetShieldVisible(bool visible)
        {
            if (shieldRoot != null)
            {
                shieldRoot.alpha = visible ? 1f : 0f;
                shieldRoot.interactable = false;
                shieldRoot.blocksRaycasts = false;
                return;
            }

            if (shieldFill != null)
            {
                shieldFill.enabled = visible;
            }

            if (shieldValueText != null)
            {
                shieldValueText.enabled = visible && !showShieldBesideHealthValue;
            }
        }

        private void AutoBind()
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                string lowerName = images[i].name.ToLowerInvariant();
                if (shieldFill == null && IsShieldFillName(lowerName))
                {
                    shieldFill = images[i];
                }
            }

            for (int i = 0; i < images.Length; i++)
            {
                string lowerName = images[i].name.ToLowerInvariant();
                if (damageFlash == null && IsDamageFlashName(lowerName))
                {
                    damageFlash = images[i];
                }
            }

            for (int i = 0; i < images.Length; i++)
            {
                string lowerName = images[i].name.ToLowerInvariant();
                if (delayedFill == null && IsDelayedFillName(lowerName))
                {
                    delayedFill = images[i];
                }
            }

            for (int i = 0; i < images.Length; i++)
            {
                string lowerName = images[i].name.ToLowerInvariant();
                if (healthFill == null && IsHealthFillName(lowerName))
                {
                    healthFill = images[i];
                }
            }

            TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                string lowerName = texts[i].name.ToLowerInvariant();
                if (shieldValueText == null && IsShieldName(lowerName))
                {
                    shieldValueText = texts[i];
                }
                else if (valueText == null && (lowerName.Contains("value") || lowerName.Contains("hp") || lowerName.Contains("health")))
                {
                    valueText = texts[i];
                }
                else if (labelText == null && lowerName.Contains("label"))
                {
                    labelText = texts[i];
                }
            }
        }

        private void BuildFallbackUi()
        {
            builtFallbackUi = true;

            GameObject canvasObject = new GameObject("Player Health Canvas");
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 320;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject rootObject = CreateUiObject("Player Health Root", canvasObject.transform);
            root = rootObject.AddComponent<CanvasGroup>();

            RectTransform rootRect = rootObject.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(0f, 1f);
            rootRect.pivot = new Vector2(0f, 1f);
            rootRect.anchoredPosition = new Vector2(34f, -28f);
            rootRect.sizeDelta = new Vector2(430f, 86f);

            HorizontalLayoutGroup row = rootObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 14f;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = false;
            row.childControlHeight = false;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;

            GameObject portrait = CreateUiObject("Player Portrait Frame", rootObject.transform);
            AddLayout(portrait, 68f, 68f);
            Image portraitImage = portrait.AddComponent<Image>();
            portraitImage.color = new Color(0.06f, 0.09f, 0.12f, 0.92f);

            GameObject column = CreateUiObject("Player Health Column", rootObject.transform);
            AddLayout(column, 340f, 68f);

            VerticalLayoutGroup columnLayout = column.AddComponent<VerticalLayoutGroup>();
            columnLayout.spacing = 7f;
            columnLayout.childAlignment = TextAnchor.MiddleLeft;
            columnLayout.childControlWidth = true;
            columnLayout.childControlHeight = false;
            columnLayout.childForceExpandWidth = true;
            columnLayout.childForceExpandHeight = false;

            GameObject textRow = CreateUiObject("Player Health Text Row", column.transform);
            AddLayout(textRow, 340f, 24f);

            HorizontalLayoutGroup textLayout = textRow.AddComponent<HorizontalLayoutGroup>();
            textLayout.childAlignment = TextAnchor.MiddleLeft;
            textLayout.childControlWidth = false;
            textLayout.childControlHeight = true;
            textLayout.childForceExpandWidth = false;
            textLayout.childForceExpandHeight = true;

            labelText = CreateText(textRow.transform, "Health Label", label, 18f, FontStyles.Bold, TextAlignmentOptions.Left);
            AddLayout(labelText.gameObject, 54f, 24f);

            valueText = CreateText(textRow.transform, "Health Value", "100 / 100", 19f, FontStyles.Bold, TextAlignmentOptions.Left);
            AddLayout(valueText.gameObject, 220f, 24f);

            GameObject barObject = CreateUiObject("Player Health Bar", column.transform);
            AddLayout(barObject, 340f, 28f);

            Image background = barObject.AddComponent<Image>();
            background.color = new Color(0.025f, 0.035f, 0.052f, 0.94f);

            delayedFill = CreateBarImage("Delayed Health Fill", barObject.transform, delayedFillColor);
            healthFill = CreateBarImage("Health Fill", barObject.transform, healthyColor);
            damageFlash = CreateBarImage("Damage Flash", barObject.transform, flashColor);
            healthPulseParent = barObject.GetComponent<RectTransform>();

            InitializeFillRects();
        }

        private void InitializeFillRects()
        {
            InitializeFillRect(healthFill, out healthFillRect, out healthFullAnchorMax, out healthFullSizeDelta, out healthFillInitialized);
            InitializeFillRect(delayedFill, out delayedFillRect, out delayedFullAnchorMax, out delayedFullSizeDelta, out delayedFillInitialized);
            InitializeFillRect(shieldFill, out shieldFillRect, out shieldFullAnchorMax, out shieldFullSizeDelta, out _);
        }

        private void InitializeFillRect(
            Image image,
            out RectTransform rect,
            out Vector2 fullAnchorMax,
            out Vector2 fullSizeDelta,
            out bool initialized)
        {
            rect = image != null ? image.rectTransform : null;
            fullAnchorMax = rect != null ? rect.anchorMax : Vector2.one;
            fullSizeDelta = rect != null ? rect.sizeDelta : Vector2.zero;
            initialized = rect != null;

            if (image != null && ShouldForceFilledBarImages())
            {
                ConfigureFilledImage(image);
            }
            else if (image != null && image.type != Image.Type.Filled)
            {
                image.type = Image.Type.Simple;
            }
        }

        private void UpdateFill(Image image, RectTransform rect, Vector2 fullAnchorMax, Vector2 fullSizeDelta, float ratio)
        {
            if (image == null)
            {
                return;
            }

            ratio = Mathf.Clamp01(ratio);
            if (image.type == Image.Type.Filled)
            {
                image.fillAmount = ratio;
                return;
            }

            if (rect == null)
            {
                InitializeFillRects();
                if (image == healthFill)
                {
                    rect = healthFillRect;
                    fullAnchorMax = healthFullAnchorMax;
                    fullSizeDelta = healthFullSizeDelta;
                }
                else if (image == shieldFill)
                {
                    rect = shieldFillRect;
                    fullAnchorMax = shieldFullAnchorMax;
                    fullSizeDelta = shieldFullSizeDelta;
                }
                else
                {
                    rect = delayedFillRect;
                    fullAnchorMax = delayedFullAnchorMax;
                    fullSizeDelta = delayedFullSizeDelta;
                }
            }

            if (rect == null)
            {
                return;
            }

            if (Mathf.Abs(fullAnchorMax.x - rect.anchorMin.x) > 0.0001f)
            {
                rect.anchorMax = new Vector2(
                    Mathf.Lerp(rect.anchorMin.x, fullAnchorMax.x, ratio),
                    fullAnchorMax.y);
                return;
            }

            rect.sizeDelta = new Vector2(fullSizeDelta.x * ratio, fullSizeDelta.y);
        }

        private void ApplyStaticVisuals()
        {
            if (ShouldForceFilledBarImages())
            {
                ConfigureFilledImage(healthFill);
                ConfigureFilledImage(delayedFill);
                ConfigureFilledImage(shieldFill);
            }

            if (ShouldUseScriptedColors())
            {
                ApplyFallbackColors();
            }

            if (damageFlash != null)
            {
                damageFlash.enabled = false;
            }

            SetText(labelText, label);

            if (valueText != null && showShieldBesideHealthValue)
            {
                valueText.richText = true;
            }
        }

        private void CacheCustomColors()
        {
            damageFlashBaseColor = damageFlash != null ? damageFlash.color : flashColor;

            if (builtFallbackUi || useScriptedColors)
            {
                damageFlashBaseColor = flashColor;
            }
        }

        private void BuildHealthPulseImage()
        {
            if (healthFill == null)
            {
                return;
            }

            if (healthPulseParent == null)
            {
                healthPulseParent = healthFill.transform.parent as RectTransform;
            }

            if (healthPulseParent == null)
            {
                return;
            }

            GameObject pulseObject = new GameObject("Health Fill Pulse Clone");
            pulseObject.transform.SetParent(healthPulseParent, false);
            healthPulseRect = pulseObject.AddComponent<RectTransform>();
            healthPulseImage = pulseObject.AddComponent<Image>();
            CopyImageShape(healthFill, healthPulseImage);
            healthPulseImage.raycastTarget = false;
            healthPulseImage.enabled = false;
            healthPulseImage.transform.SetSiblingIndex(healthFill.transform.GetSiblingIndex());
        }

        private void ApplyHealthPulseImage(float normalizedTime)
        {
            if (healthPulseImage == null || healthPulseRect == null || healthFill == null)
            {
                return;
            }

            CopyImageShape(healthFill, healthPulseImage);
            CopyRectTransform(healthFill.rectTransform, healthPulseRect);
            UpdateFill(healthPulseImage, healthPulseRect, healthFill.rectTransform.anchorMax, healthFill.rectTransform.sizeDelta, displayedRatio);

            float punchRatio = Mathf.Max(0.01f, lowHealthPulsePunchRatio);
            float expand;
            float alpha;
            if (normalizedTime <= punchRatio)
            {
                float punchTime = normalizedTime / punchRatio;
                expand = Mathf.Lerp(0f, lowHealthPulsePunchExpandPixels, EaseOutCubic(punchTime));
                alpha = lowHealthPulseStartAlpha;
            }
            else
            {
                float driftTime = (normalizedTime - punchRatio) / (1f - punchRatio);
                expand = Mathf.Lerp(lowHealthPulsePunchExpandPixels, lowHealthPulseFinalExpandPixels, EaseOutQuad(driftTime));
                alpha = Mathf.Lerp(lowHealthPulseStartAlpha, 0f, EaseInQuad(driftTime));
            }

            ExpandRect(healthPulseRect, expand);
            Color color = healthFill.color;
            color.a *= alpha;
            healthPulseImage.color = color;
            healthPulseImage.enabled = alpha > 0.001f;
        }

        private void HideHealthPulseImage(bool resetTimer)
        {
            if (resetTimer)
            {
                lowHealthPulseTimer = 0f;
            }

            if (healthPulseImage != null)
            {
                healthPulseImage.enabled = false;
            }
        }

        private static void CopyImageShape(Image source, Image target)
        {
            if (source == null || target == null)
            {
                return;
            }

            target.sprite = source.sprite;
            target.overrideSprite = source.overrideSprite;
            target.type = source.type;
            target.fillMethod = source.fillMethod;
            target.fillOrigin = source.fillOrigin;
            target.fillClockwise = source.fillClockwise;
            target.fillCenter = source.fillCenter;
            target.preserveAspect = source.preserveAspect;
            target.material = source.material;
            target.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        }

        private static void CopyRectTransform(RectTransform source, RectTransform target)
        {
            if (source == null || target == null)
            {
                return;
            }

            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.anchoredPosition = source.anchoredPosition;
            target.sizeDelta = source.sizeDelta;
            target.offsetMin = source.offsetMin;
            target.offsetMax = source.offsetMax;
            target.localRotation = source.localRotation;
            target.localScale = source.localScale;
        }

        private static void ExpandRect(RectTransform rect, float expand)
        {
            if (rect == null || expand <= 0f)
            {
                return;
            }

            if (HasStretchAnchor(rect))
            {
                rect.offsetMin -= new Vector2(expand, expand);
                rect.offsetMax += new Vector2(expand, expand);
                return;
            }

            rect.sizeDelta += new Vector2(expand * 2f, expand * 2f);
        }

        private static bool HasStretchAnchor(RectTransform rect)
        {
            return Mathf.Abs(rect.anchorMin.x - rect.anchorMax.x) > 0.0001f
                || Mathf.Abs(rect.anchorMin.y - rect.anchorMax.y) > 0.0001f;
        }

        private static float EaseOutCubic(float value)
        {
            value = Mathf.Clamp01(value);
            return 1f - Mathf.Pow(1f - value, 3f);
        }

        private static float EaseOutQuad(float value)
        {
            value = Mathf.Clamp01(value);
            return 1f - (1f - value) * (1f - value);
        }

        private static float EaseInQuad(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value;
        }

        private void ApplyFallbackColors()
        {
            if (delayedFill != null)
            {
                delayedFill.color = delayedFillColor;
            }

            if (damageFlash != null)
            {
                damageFlash.color = flashColor;
                damageFlashBaseColor = flashColor;
            }
        }

        private void ArrangeBarLayers()
        {
            if (!autoArrangeBarLayers && !builtFallbackUi)
            {
                return;
            }

            Transform commonParent = GetCommonBarParent();
            if (commonParent == null)
            {
                return;
            }

            SetAsLastSiblingIfChildOf(delayedFill, commonParent);
            SetAsLastSiblingIfChildOf(healthFill, commonParent);
            SetAsLastSiblingIfChildOf(damageFlash, commonParent);
        }

        private Transform GetCommonBarParent()
        {
            Transform parent = healthFill != null ? healthFill.transform.parent : null;
            if (parent == null)
            {
                return null;
            }

            if (delayedFill != null && delayedFill.transform.parent != parent)
            {
                return null;
            }

            if (damageFlash != null && damageFlash.transform.parent != parent)
            {
                return null;
            }

            return parent;
        }

        private static void SetAsLastSiblingIfChildOf(Image image, Transform parent)
        {
            if (image != null && image.transform.parent == parent)
            {
                image.transform.SetAsLastSibling();
            }
        }

        private static void ConfigureFilledImage(Image image)
        {
            if (image == null)
            {
                return;
            }

            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            image.fillClockwise = true;
            image.preserveAspect = false;
        }

        private bool ShouldForceFilledBarImages()
        {
            return forceFilledBarImages || builtFallbackUi;
        }

        private bool ShouldUseScriptedColors()
        {
            return useScriptedColors || builtFallbackUi;
        }

        private bool HasUiChildren()
        {
            Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i].gameObject != gameObject)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsHealthFillName(string lowerName)
        {
            return lowerName.Contains("fill")
                && (lowerName.Contains("health") || lowerName.Contains("hp"))
                && !IsDelayedFillName(lowerName)
                && !IsDamageFlashName(lowerName)
                && !IsNonBarImageName(lowerName);
        }

        private static bool IsShieldFillName(string lowerName)
        {
            return lowerName.Contains("fill")
                && IsShieldName(lowerName)
                && !IsNonBarImageName(lowerName);
        }

        private static bool IsShieldName(string lowerName)
        {
            return lowerName.Contains("shield")
                || lowerName.Contains("barrier")
                || lowerName.Contains("방어막");
        }

        private static bool IsDelayedFillName(string lowerName)
        {
            return (lowerName.Contains("delayed") || lowerName.Contains("delay") || lowerName.Contains("lag") || lowerName.Contains("trail"))
                && lowerName.Contains("fill")
                && !IsNonBarImageName(lowerName);
        }

        private static bool IsDamageFlashName(string lowerName)
        {
            return lowerName.Contains("flash") && !IsNonBarImageName(lowerName);
        }

        private static bool IsNonBarImageName(string lowerName)
        {
            return lowerName.Contains("background")
                || lowerName.Contains("bg")
                || lowerName.Contains("frame")
                || lowerName.Contains("border")
                || lowerName.Contains("panel")
                || lowerName.Contains("root")
                || lowerName.Contains("portrait")
                || lowerName.Contains("icon");
        }

        private static Image CreateBarImage(string objectName, Transform parent, Color color)
        {
            GameObject imageObject = CreateUiObject(objectName, parent);
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(3f, 3f);
            rect.offsetMax = new Vector2(-3f, -3f);

            Image image = imageObject.AddComponent<Image>();
            image.color = color;
            return image;
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
            text.color = new Color(0.88f, 0.96f, 1f, 1f);
            text.raycastTarget = false;
            return text;
        }

        private static void AddLayout(GameObject target, float preferredWidth, float preferredHeight)
        {
            LayoutElement layout = target.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = target.AddComponent<LayoutElement>();
            }

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
