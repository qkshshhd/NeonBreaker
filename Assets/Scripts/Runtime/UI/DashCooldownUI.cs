using NeonBreaker.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeonBreaker.UI
{
    public sealed class DashCooldownUI : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private PlayerDash2D dash;

        [Header("Bindings")]
        [SerializeField] private CanvasGroup root;
        [SerializeField] private Image cooldownFill;
        [SerializeField] private Image readyFrame;
        [SerializeField] private TextMeshProUGUI keyText;
        [SerializeField] private TextMeshProUGUI cooldownText;

        [Header("Display")]
        [SerializeField] private bool buildFallbackUiIfMissing = false;
        [SerializeField] private bool showSecondsText = true;
        [SerializeField] private bool invertFill = true;
        [SerializeField] private Color readyColor = new Color(0.4f, 1f, 0.95f, 1f);
        [SerializeField] private Color cooldownColor = new Color(0.35f, 0.5f, 0.65f, 0.85f);
        [SerializeField] private string keyLabel = "SPACE";

        private void Awake()
        {
            if (dash == null)
            {
                PlayerController player = FindAnyObjectByType<PlayerController>();
                dash = player != null ? player.Dash : FindAnyObjectByType<PlayerDash2D>();
            }

            if (root == null)
            {
                root = GetComponentInChildren<CanvasGroup>(true);
            }

            if (buildFallbackUiIfMissing && (root == null || cooldownFill == null))
            {
                BuildFallbackUi();
            }

            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (root == null)
            {
                return;
            }

            if (dash == null)
            {
                root.alpha = 0f;
                return;
            }

            root.alpha = 1f;
            root.blocksRaycasts = false;
            root.interactable = false;

            float remainingNormalized = Mathf.Clamp01(dash.CooldownNormalized);
            float fillAmount = invertFill ? 1f - remainingNormalized : remainingNormalized;
            bool ready = dash.IsReady;

            if (cooldownFill != null)
            {
                cooldownFill.fillAmount = ready ? 1f : fillAmount;
                cooldownFill.color = ready ? readyColor : cooldownColor;
            }

            if (readyFrame != null)
            {
                readyFrame.color = ready ? readyColor : cooldownColor;
            }

            if (keyText != null)
            {
                keyText.text = keyLabel;
                keyText.color = ready ? Color.white : new Color(0.75f, 0.82f, 0.9f, 0.7f);
            }

            if (cooldownText != null)
            {
                if (ready)
                {
                    cooldownText.text = "READY";
                }
                else if (showSecondsText)
                {
                    cooldownText.text = dash.CooldownRemaining.ToString("0.0");
                }
                else
                {
                    cooldownText.text = string.Empty;
                }
            }
        }

        private void BuildFallbackUi()
        {
            GameObject canvasObject = new GameObject("Dash Cooldown Canvas");
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 220;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject rootObject = CreateUiObject("Dash Cooldown Root", canvasObject.transform);
            root = rootObject.AddComponent<CanvasGroup>();

            RectTransform rootRect = rootObject.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0f);
            rootRect.anchorMax = new Vector2(0.5f, 0f);
            rootRect.pivot = new Vector2(0.5f, 0f);
            rootRect.anchoredPosition = new Vector2(0f, 36f);
            rootRect.sizeDelta = new Vector2(180f, 86f);

            Image background = rootObject.AddComponent<Image>();
            background.color = new Color(0.025f, 0.03f, 0.045f, 0.78f);

            GameObject fillObject = CreateUiObject("Dash Fill", rootObject.transform);
            RectTransform fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0.5f, 0.5f);
            fillRect.anchorMax = new Vector2(0.5f, 0.5f);
            fillRect.pivot = new Vector2(0.5f, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = new Vector2(162f, 68f);

            cooldownFill = fillObject.AddComponent<Image>();
            cooldownFill.color = readyColor;
            cooldownFill.type = Image.Type.Filled;
            cooldownFill.fillMethod = Image.FillMethod.Horizontal;
            cooldownFill.fillOrigin = 0;
            cooldownFill.fillAmount = 1f;

            readyFrame = rootObject.GetComponent<Image>();

            keyText = CreateText(rootObject.transform, "Dash Key Text", keyLabel, 22f, FontStyles.Bold);
            RectTransform keyRect = keyText.rectTransform;
            keyRect.anchorMin = new Vector2(0.5f, 0.5f);
            keyRect.anchorMax = new Vector2(0.5f, 0.5f);
            keyRect.pivot = new Vector2(0.5f, 0.5f);
            keyRect.anchoredPosition = new Vector2(0f, 12f);
            keyRect.sizeDelta = new Vector2(160f, 28f);

            cooldownText = CreateText(rootObject.transform, "Dash Cooldown Text", "READY", 18f, FontStyles.Normal);
            RectTransform cooldownRect = cooldownText.rectTransform;
            cooldownRect.anchorMin = new Vector2(0.5f, 0.5f);
            cooldownRect.anchorMax = new Vector2(0.5f, 0.5f);
            cooldownRect.pivot = new Vector2(0.5f, 0.5f);
            cooldownRect.anchoredPosition = new Vector2(0f, -16f);
            cooldownRect.sizeDelta = new Vector2(160f, 28f);
        }

        private static TextMeshProUGUI CreateText(Transform parent, string objectName, string value, float fontSize, FontStyles style)
        {
            GameObject textObject = CreateUiObject(objectName, parent);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.color = Color.white;
            return text;
        }

        private static GameObject CreateUiObject(string objectName, Transform parent)
        {
            GameObject uiObject = new GameObject(objectName);
            uiObject.transform.SetParent(parent, false);
            uiObject.AddComponent<RectTransform>();
            return uiObject;
        }
    }
}
