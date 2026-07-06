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
        [SerializeField] private Vector3 hoverScale = new Vector3(1.06f, 1.06f, 1f);
        [SerializeField] private Vector3 pressedScale = new Vector3(0.98f, 0.98f, 1f);
        [SerializeField] private Vector2 hoverOffset = new Vector2(6f, 0f);
        [SerializeField, Min(1f)] private float enterSpeed = 18f;
        [SerializeField, Min(1f)] private float exitSpeed = 14f;

        [Header("Color")]
        [SerializeField] private Graphic targetGraphic;
        [SerializeField] private Color hoverTint = new Color(0.18f, 0.78f, 1f, 1f);
        [SerializeField] private Color pressedTint = new Color(0.08f, 0.42f, 0.68f, 1f);
        [SerializeField, Range(0f, 1f)] private float colorBlend = 0.55f;

        [Header("Fallback Input")]
        [SerializeField] private bool useMousePositionFallback = true;
        [SerializeField] private bool animateWhenKeyboardSelected;

        private RectTransform rectTransform;
        private Vector3 baseScale;
        private Vector2 baseAnchoredPosition;
        private Color baseColor = Color.white;
        private bool hasGraphic;
        private bool hovering;
        private bool pressing;
        private bool selected;

        private void Awake()
        {
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
        }

        private void OnEnable()
        {
            hovering = false;
            pressing = false;
            selected = false;
            CacheBaseIfNeeded();
            ApplyImmediate();
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

            rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, GetTargetScale(), t);
            rectTransform.anchoredPosition = Vector2.Lerp(rectTransform.anchoredPosition, GetTargetPosition(), t);

            if (hasGraphic && targetGraphic != null)
            {
                targetGraphic.color = Color.Lerp(targetGraphic.color, GetTargetColor(), t);
            }
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
