using UnityEngine;
using UnityEngine.UI;

namespace NeonBreaker.UI
{
    [DefaultExecutionOrder(10000)]
    public sealed class CustomCursorUI : MonoBehaviour
    {
        private const string RuntimeObjectName = "Custom Cursor UI";

        [Header("Cursor")]
        [SerializeField] private Sprite cursorSprite;
        [SerializeField] private Vector2 cursorSize = new Vector2(32f, 32f);
        [Tooltip("On이면 스프라이트 Import Settings의 Pivot이 실제 마우스 클릭 지점이 됩니다.")]
        [SerializeField] private bool useSpritePivotAsHotSpot = true;
        [Tooltip("Pivot 기준으로도 미세하게 어긋날 때 쓰는 보정값입니다. 보통은 (0, 0)이 맞습니다.")]
        [SerializeField] private Vector2 hotSpot = Vector2.zero;
        [SerializeField] private Color color = Color.white;
        [SerializeField] private bool hideSystemCursor = true;
        [SerializeField] private bool hideWhenCursorLocked = true;
        [SerializeField] private bool hideOutsideScreen = true;
        [SerializeField, HideInInspector] private int tuningVersion;

        [Header("Canvas")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private Image cursorImage;
        [SerializeField] private int sortingOrder = 1200;

        private static CustomCursorUI instance;
        private static Sprite fallbackSprite;
        private const int CurrentTuningVersion = 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeInstance()
        {
            if (instance != null || FindAnyObjectByType<CustomCursorUI>() != null)
            {
                return;
            }

            GameObject cursorObject = new GameObject(RuntimeObjectName);
            DontDestroyOnLoad(cursorObject);
            cursorObject.AddComponent<CustomCursorUI>();
        }

        private void Awake()
        {
            UpgradeSerializedDefaults();

            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureCanvas();
            ApplyVisual();
            ApplySystemCursorState();
        }

        private void OnEnable()
        {
            ApplySystemCursorState();
        }

        private void OnDisable()
        {
            if (instance == this)
            {
                Cursor.visible = true;
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
                Cursor.visible = true;
            }
        }

        private void OnValidate()
        {
            UpgradeSerializedDefaults();

            cursorSize.x = Mathf.Max(1f, cursorSize.x);
            cursorSize.y = Mathf.Max(1f, cursorSize.y);

            if (canvas != null)
            {
                canvas.sortingOrder = sortingOrder;
            }

            ApplyVisual();
        }

        private void LateUpdate()
        {
            ApplySystemCursorState();

            if (cursorImage == null)
            {
                return;
            }

            bool shouldShow = ShouldShowCursor();
            cursorImage.enabled = shouldShow;
            if (!shouldShow)
            {
                return;
            }

            RectTransform rectTransform = cursorImage.rectTransform;
            RectTransform canvasRect = canvas != null ? canvas.transform as RectTransform : null;
            if (canvasRect != null
                && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, Input.mousePosition, null, out Vector2 localPoint))
            {
                rectTransform.anchoredPosition = localPoint + hotSpot;
            }
            else
            {
                rectTransform.position = (Vector2)Input.mousePosition + hotSpot;
            }
        }

        public void SetCursorSprite(Sprite sprite)
        {
            cursorSprite = sprite;
            ApplyVisual();
        }

        private bool ShouldShowCursor()
        {
            if (hideWhenCursorLocked && Cursor.lockState == CursorLockMode.Locked)
            {
                return false;
            }

            if (!hideOutsideScreen)
            {
                return true;
            }

            Vector3 mousePosition = Input.mousePosition;
            return mousePosition.x >= 0f
                && mousePosition.y >= 0f
                && mousePosition.x <= Screen.width
                && mousePosition.y <= Screen.height;
        }

        private void ApplySystemCursorState()
        {
            Cursor.visible = !hideSystemCursor || !ShouldShowCursor();
        }

        private void EnsureCanvas()
        {
            if (canvas == null)
            {
                GameObject canvasObject = new GameObject("Custom Cursor Canvas");
                canvasObject.transform.SetParent(transform, false);
                canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = sortingOrder;
                CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                canvasObject.AddComponent<GraphicRaycaster>();
            }

            if (cursorImage == null)
            {
                GameObject imageObject = new GameObject("Cursor Sprite");
                imageObject.transform.SetParent(canvas.transform, false);
                cursorImage = imageObject.AddComponent<Image>();
                cursorImage.raycastTarget = false;
                cursorImage.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                cursorImage.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            }
        }

        private void ApplyVisual()
        {
            if (cursorImage == null)
            {
                return;
            }

            cursorImage.sprite = cursorSprite != null ? cursorSprite : GetFallbackSprite();
            cursorImage.color = color;
            cursorImage.raycastTarget = false;
            cursorImage.rectTransform.sizeDelta = cursorSize;
            cursorImage.rectTransform.pivot = GetCursorPivot();

            if (canvas != null)
            {
                canvas.sortingOrder = sortingOrder;
            }
        }

        private void UpgradeSerializedDefaults()
        {
            if (tuningVersion >= CurrentTuningVersion)
            {
                return;
            }

            if (hotSpot == new Vector2(4f, -4f))
            {
                hotSpot = Vector2.zero;
            }

            useSpritePivotAsHotSpot = true;
            tuningVersion = CurrentTuningVersion;
        }

        private Vector2 GetCursorPivot()
        {
            if (!useSpritePivotAsHotSpot)
            {
                return new Vector2(0f, 1f);
            }

            Sprite sprite = cursorSprite != null ? cursorSprite : GetFallbackSprite();
            if (sprite == null || sprite.rect.width <= 0f || sprite.rect.height <= 0f)
            {
                return new Vector2(0f, 1f);
            }

            return new Vector2(
                Mathf.Clamp01(sprite.pivot.x / sprite.rect.width),
                Mathf.Clamp01(sprite.pivot.y / sprite.rect.height));
        }

        private static Sprite GetFallbackSprite()
        {
            if (fallbackSprite != null)
            {
                return fallbackSprite;
            }

            const int size = 16;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            Color clear = Color.clear;
            Color core = Color.white;
            Color glow = new Color(0.08f, 0.95f, 1f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    texture.SetPixel(x, y, clear);
                }
            }

            for (int y = 1; y < 14; y++)
            {
                int width = Mathf.Max(1, 10 - y / 2);
                for (int x = 1; x <= width; x++)
                {
                    bool edge = x == 1 || x == width || y == 1;
                    texture.SetPixel(x, size - 1 - y, edge ? glow : core);
                }
            }

            texture.SetPixel(0, size - 1, glow);
            texture.SetPixel(1, size - 2, core);
            texture.SetPixel(0, size - 2, glow);
            texture.SetPixel(1, size - 1, glow);

            for (int i = 0; i < 5; i++)
            {
                int x = 6 + i;
                int y = 5 - i;
                if (x >= 0 && x < size && y >= 0 && y < size)
                {
                    texture.SetPixel(x, y, glow);
                }
            }

            texture.Apply();
            fallbackSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0f, 1f), 16f);
            fallbackSprite.name = "Runtime Custom Cursor Sprite";
            return fallbackSprite;
        }
    }
}
