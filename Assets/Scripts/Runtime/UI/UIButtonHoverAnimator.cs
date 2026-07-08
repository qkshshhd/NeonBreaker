using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine.UI;

namespace NeonBreaker.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UIButtonHoverAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        [Header("Motion")]
        [SerializeField] private Vector3 hoverScale = new Vector3(1.025f, 1.025f, 1f);
        [SerializeField] private Vector3 pressedScale = new Vector3(0.985f, 0.985f, 1f);
        [SerializeField] private Vector2 hoverOffset = new Vector2(3f, 0f);
        [SerializeField, Min(1f)] private float enterSpeed = 18f;
        [SerializeField, Min(1f)] private float exitSpeed = 14f;

        [Header("Color")]
        [SerializeField] private Graphic targetGraphic;
        [SerializeField] private Color hoverTint = new Color(0.18f, 0.78f, 1f, 1f);
        [SerializeField] private Color pressedTint = new Color(0.08f, 0.42f, 0.68f, 1f);
        [SerializeField, Range(0f, 1f)] private float colorBlend = 0.22f;

        [Header("Fallback Input")]
        [SerializeField] private bool useMousePositionFallback = true;
        [SerializeField] private bool animateWhenKeyboardSelected;

        [Header("Neon Accent")]
        [SerializeField] private bool autoCreateNeonAccent = true;
        [SerializeField] private Image accentLine;
        [SerializeField] private Image topLine;
        [SerializeField] private Image bottomLine;
        [SerializeField] private Image rightLine;
        [SerializeField] private Image innerGlow;
        [SerializeField] private bool useSweepLine;
        [SerializeField] private Image sweepLine;
        [SerializeField] private Color accentColor = new Color(0.03f, 0.95f, 1f, 0.95f);
        [SerializeField] private Color sweepColor = new Color(0.6f, 1f, 1f, 0.12f);
        [SerializeField, Min(1f)] private float outlineThickness = 2f;
        [SerializeField, Min(1f)] private float selectedLineWidth = 4f;
        [SerializeField, Min(0f)] private float sweepWidth = 42f;
        [SerializeField, Min(0.01f)] private float accentLerpSpeed = 16f;
        [SerializeField, HideInInspector] private int visualTuningVersion;

        private RectTransform rectTransform;
        private RectTransform accentLineRect;
        private RectTransform topLineRect;
        private RectTransform bottomLineRect;
        private RectTransform rightLineRect;
        private RectTransform innerGlowRect;
        private RectTransform sweepLineRect;
        private Vector3 baseScale;
        private Vector2 baseAnchoredPosition;
        private Color baseColor = Color.white;
        private bool hasGraphic;
        private bool hovering;
        private bool pressing;
        private bool selected;
        private float hoverAmount;
        private const int CurrentVisualTuningVersion = 1;

        private void Awake()
        {
            UpgradeVisualDefaults();

            rectTransform = GetComponent<RectTransform>();
            baseScale = rectTransform.localScale;
            baseAnchoredPosition = rectTransform.anchoredPosition;

            if (targetGraphic == null)
            {
                targetGraphic = GetComponent<Graphic>();
            }

            hasGraphic = targetGraphic != null;
            if (hasGraphic)
            {
                baseColor = targetGraphic.color;
            }

            EnsureNeonAccent();
        }

        private void OnEnable()
        {
            hovering = false;
            pressing = false;
            selected = false;
            hoverAmount = 0f;
            CacheBaseIfNeeded();
            EnsureNeonAccent();
            ApplyImmediate();
        }

        private void OnValidate()
        {
            UpgradeVisualDefaults();

            outlineThickness = Mathf.Max(1f, outlineThickness);
            selectedLineWidth = Mathf.Max(1f, selectedLineWidth);
            sweepWidth = Mathf.Max(0f, sweepWidth);
        }

        private void OnDisable()
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.localScale = baseScale;
            rectTransform.anchoredPosition = baseAnchoredPosition;
            if (hasGraphic && targetGraphic != null)
            {
                targetGraphic.color = baseColor;
            }
        }

        private void Update()
        {
            CacheBaseIfNeeded();
            UpdateMouseFallback();

            float speed = hovering || pressing ? enterSpeed : exitSpeed;
            float t = 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime);
            float accentT = 1f - Mathf.Exp(-accentLerpSpeed * Time.unscaledDeltaTime);

            rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, GetTargetScale(), t);
            rectTransform.anchoredPosition = Vector2.Lerp(rectTransform.anchoredPosition, GetTargetPosition(), t);
            hoverAmount = Mathf.Lerp(hoverAmount, hovering ? 1f : 0f, accentT);

            if (hasGraphic && targetGraphic != null)
            {
                targetGraphic.color = Color.Lerp(targetGraphic.color, GetTargetColor(), t);
            }

            ApplyNeonAccent();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovering = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovering = false;
            pressing = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pressing = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pressing = false;
        }

        public void OnSelect(BaseEventData eventData)
        {
            selected = true;
        }

        public void OnDeselect(BaseEventData eventData)
        {
            selected = false;
            hovering = false;
            pressing = false;
        }

        private void UpdateMouseFallback()
        {
            if (!useMousePositionFallback || rectTransform == null || !gameObject.activeInHierarchy)
            {
                return;
            }

            bool pointerInside = TryGetPointerPosition(out Vector2 pointerPosition)
                && RectTransformUtility.RectangleContainsScreenPoint(
                    rectTransform,
                    pointerPosition,
                    GetUiCamera());

            bool interactable = IsInteractable();
            bool shouldHover = interactable && (pointerInside || (selected && animateWhenKeyboardSelected));

            hovering = shouldHover;
            pressing = interactable && pointerInside && IsPrimaryMousePressed();

            if (!shouldHover)
            {
                pressing = false;
            }
        }

        private Vector3 GetTargetScale()
        {
            if (pressing)
            {
                return Vector3.Scale(baseScale, pressedScale);
            }

            return hovering ? Vector3.Scale(baseScale, hoverScale) : baseScale;
        }

        private Vector2 GetTargetPosition()
        {
            return hovering && !pressing ? baseAnchoredPosition + hoverOffset : baseAnchoredPosition;
        }

        private Color GetTargetColor()
        {
            if (pressing)
            {
                return Color.Lerp(baseColor, pressedTint, colorBlend);
            }

            return hovering ? Color.Lerp(baseColor, hoverTint, colorBlend) : baseColor;
        }

        private void ApplyImmediate()
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.localScale = baseScale;
            rectTransform.anchoredPosition = baseAnchoredPosition;
            if (hasGraphic && targetGraphic != null)
            {
                targetGraphic.color = baseColor;
            }

            hoverAmount = 0f;
            ApplyNeonAccent();
        }

        private void CacheBaseIfNeeded()
        {
            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
            }

            if (targetGraphic == null)
            {
                targetGraphic = GetComponent<Graphic>();
                hasGraphic = targetGraphic != null;
            }
        }

        private void EnsureNeonAccent()
        {
            if (!autoCreateNeonAccent || rectTransform == null)
            {
                CacheAccentRects();
                return;
            }

            if (accentLine == null)
            {
                accentLine = CreateAccentImage("Hover Accent Line", transform, accentColor);
                accentLineRect = accentLine.rectTransform;
                accentLineRect.anchorMin = new Vector2(0f, 0f);
                accentLineRect.anchorMax = new Vector2(0f, 1f);
                accentLineRect.pivot = new Vector2(0f, 0.5f);
                accentLineRect.anchoredPosition = Vector2.zero;
                accentLineRect.sizeDelta = new Vector2(selectedLineWidth, 0f);
            }

            if (topLine == null)
            {
                topLine = CreateAccentImage("Hover Top Line", transform, accentColor);
                topLineRect = topLine.rectTransform;
                topLineRect.anchorMin = new Vector2(0f, 1f);
                topLineRect.anchorMax = new Vector2(1f, 1f);
                topLineRect.pivot = new Vector2(0.5f, 1f);
                topLineRect.anchoredPosition = Vector2.zero;
                topLineRect.sizeDelta = new Vector2(0f, outlineThickness);
            }

            if (bottomLine == null)
            {
                bottomLine = CreateAccentImage("Hover Bottom Line", transform, accentColor);
                bottomLineRect = bottomLine.rectTransform;
                bottomLineRect.anchorMin = new Vector2(0f, 0f);
                bottomLineRect.anchorMax = new Vector2(1f, 0f);
                bottomLineRect.pivot = new Vector2(0.5f, 0f);
                bottomLineRect.anchoredPosition = Vector2.zero;
                bottomLineRect.sizeDelta = new Vector2(0f, outlineThickness);
            }

            if (rightLine == null)
            {
                rightLine = CreateAccentImage("Hover Right Line", transform, accentColor);
                rightLineRect = rightLine.rectTransform;
                rightLineRect.anchorMin = new Vector2(1f, 0f);
                rightLineRect.anchorMax = new Vector2(1f, 1f);
                rightLineRect.pivot = new Vector2(1f, 0.5f);
                rightLineRect.anchoredPosition = Vector2.zero;
                rightLineRect.sizeDelta = new Vector2(outlineThickness, 0f);
            }

            if (innerGlow == null)
            {
                innerGlow = CreateAccentImage("Hover Inner Glow", transform, accentColor);
                innerGlowRect = innerGlow.rectTransform;
                Stretch(innerGlowRect);
            }

            if (useSweepLine && sweepLine == null)
            {
                sweepLine = CreateAccentImage("Hover Sweep Line", transform, sweepColor);
                sweepLineRect = sweepLine.rectTransform;
                sweepLineRect.anchorMin = new Vector2(0f, 0f);
                sweepLineRect.anchorMax = new Vector2(0f, 1f);
                sweepLineRect.pivot = new Vector2(0.5f, 0.5f);
                sweepLineRect.anchoredPosition = new Vector2(-sweepWidth, 0f);
                sweepLineRect.sizeDelta = new Vector2(sweepWidth, 0f);
            }

            CacheAccentRects();
            ApplyNeonAccent();
        }

        private void CacheAccentRects()
        {
            accentLineRect = accentLine != null ? accentLine.rectTransform : null;
            topLineRect = topLine != null ? topLine.rectTransform : null;
            bottomLineRect = bottomLine != null ? bottomLine.rectTransform : null;
            rightLineRect = rightLine != null ? rightLine.rectTransform : null;
            innerGlowRect = innerGlow != null ? innerGlow.rectTransform : null;
            sweepLineRect = sweepLine != null ? sweepLine.rectTransform : null;
        }

        private void ApplyNeonAccent()
        {
            ApplyLine(accentLine, hoverAmount, 1f);
            ApplyLine(topLine, hoverAmount, 0.72f);
            ApplyLine(bottomLine, hoverAmount, 0.72f);
            ApplyLine(rightLine, hoverAmount, 0.45f);

            if (accentLine != null)
            {
                if (accentLineRect != null)
                {
                    accentLineRect.sizeDelta = new Vector2(Mathf.Lerp(1f, selectedLineWidth, hoverAmount), 0f);
                    accentLineRect.localScale = Vector3.one;
                }
            }

            if (topLineRect != null)
            {
                topLineRect.sizeDelta = new Vector2(0f, outlineThickness);
            }

            if (bottomLineRect != null)
            {
                bottomLineRect.sizeDelta = new Vector2(0f, outlineThickness);
            }

            if (rightLineRect != null)
            {
                rightLineRect.sizeDelta = new Vector2(outlineThickness, 0f);
            }

            if (innerGlow != null)
            {
                Color color = accentColor;
                color.a = 0.08f * hoverAmount;
                innerGlow.color = color;

                if (innerGlowRect != null)
                {
                    Stretch(innerGlowRect);
                }
            }

            if (sweepLine != null)
            {
                sweepLine.enabled = useSweepLine;
                if (!useSweepLine)
                {
                    return;
                }

                Color color = sweepColor;
                float pulse = hovering ? 0.75f + Mathf.Sin(Time.unscaledTime * 18f) * 0.25f : 0f;
                color.a *= hoverAmount * pulse;
                sweepLine.color = color;

                if (sweepLineRect != null)
                {
                    float width = rectTransform != null ? rectTransform.rect.width : 280f;
                    float x = Mathf.Lerp(-sweepWidth, width + sweepWidth, hoverAmount);
                    sweepLineRect.anchoredPosition = new Vector2(x, 0f);
                    sweepLineRect.sizeDelta = new Vector2(sweepWidth, 0f);
                }
            }
        }

        private void ApplyLine(Image image, float amount, float alphaMultiplier)
        {
            if (image == null)
            {
                return;
            }

            Color color = accentColor;
            color.a *= amount * alphaMultiplier;
            image.color = color;
            image.enabled = amount > 0.001f;
        }

        private void UpgradeVisualDefaults()
        {
            if (visualTuningVersion >= CurrentVisualTuningVersion)
            {
                return;
            }

            if (hoverScale == new Vector3(1.06f, 1.06f, 1f))
            {
                hoverScale = new Vector3(1.025f, 1.025f, 1f);
            }

            if (pressedScale == new Vector3(0.98f, 0.98f, 1f))
            {
                pressedScale = new Vector3(0.985f, 0.985f, 1f);
            }

            if (hoverOffset == new Vector2(6f, 0f))
            {
                hoverOffset = new Vector2(3f, 0f);
            }

            if (Mathf.Approximately(colorBlend, 0.55f))
            {
                colorBlend = 0.22f;
            }

            useSweepLine = false;
            visualTuningVersion = CurrentVisualTuningVersion;
        }

        private static Image CreateAccentImage(string objectName, Transform parent, Color color)
        {
            GameObject accentObject = new GameObject(objectName);
            accentObject.transform.SetParent(parent, false);
            Image image = accentObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            accentObject.transform.SetAsFirstSibling();
            return image;
        }

        private static void Stretch(RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }

        private bool IsInteractable()
        {
            Button button = GetComponent<Button>();
            return button == null || button.interactable;
        }

        private Camera GetUiCamera()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            return canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
        }

        private static bool TryGetPointerPosition(out Vector2 pointerPosition)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                pointerPosition = Mouse.current.position.ReadValue();
                return true;
            }

            pointerPosition = default;
            return false;
#else
            pointerPosition = Input.mousePosition;
            return true;
#endif
        }

        private static bool IsPrimaryMousePressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.leftButton.isPressed;
            }

            return false;
#else
            return Input.GetMouseButton(0);
#endif
        }
    }
}
