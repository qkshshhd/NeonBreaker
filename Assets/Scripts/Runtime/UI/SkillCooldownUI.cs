using NeonBreaker.Skills;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeonBreaker.UI
{
    public sealed class SkillCooldownUI : MonoBehaviour
    {
        [SerializeField] private PlayerSkillController skillController;
        [SerializeField] private CanvasGroup root;
        [SerializeField] private Image cooldownFill;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI keyText;
        [SerializeField] private TextMeshProUGUI cooldownText;
        [SerializeField] private bool buildFallbackUiIfMissing = true;
        [SerializeField] private bool showSecondsText = true;
        [SerializeField] private bool invertFill = true;
        [SerializeField] private Color readyColor = new Color(1f, 0.35f, 0.95f, 1f);
        [SerializeField] private Color cooldownColor = new Color(0.45f, 0.28f, 0.62f, 0.85f);
        [SerializeField] private string keyLabel = "RMB";

        private void Awake()
        {
            if (skillController == null)
            {
                skillController = FindAnyObjectByType<PlayerSkillController>();
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

            if (skillController == null || !skillController.HasSkill)
            {
                root.alpha = 0f;
                return;
            }

            root.alpha = 1f;
            root.blocksRaycasts = false;
            root.interactable = false;

            bool ready = skillController.IsReady;
            float normalized = Mathf.Clamp01(skillController.CooldownNormalized);
            float fillAmount = invertFill ? 1f - normalized : normalized;

            if (cooldownFill != null)
            {
                cooldownFill.fillAmount = ready ? 1f : fillAmount;
                cooldownFill.color = ready ? readyColor : cooldownColor;
            }

            if (iconImage != null)
            {
                iconImage.color = ready ? Color.white : new Color(0.65f, 0.65f, 0.72f, 0.8f);
            }

            if (keyText != null)
            {
                keyText.text = keyLabel;
                keyText.color = ready ? Color.white : new Color(0.78f, 0.78f, 0.86f, 0.75f);
            }

            if (cooldownText != null)
            {
                cooldownText.text = ready
                    ? "READY"
                    : showSecondsText
                        ? skillController.CooldownRemaining.ToString("0.0")
                        : string.Empty;
            }
        }

        private void BuildFallbackUi()
        {
            GameObject canvasObject = new GameObject("Skill Cooldown Canvas");
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 221;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject rootObject = CreateUiObject("Skill Cooldown Root", canvasObject.transform);
            root = rootObject.AddComponent<CanvasGroup>();

            RectTransform rootRect = rootObject.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0f);
            rootRect.anchorMax = new Vector2(0.5f, 0f);
            rootRect.pivot = new Vector2(0.5f, 0f);
            rootRect.anchoredPosition = new Vector2(130f, 36f);
            rootRect.sizeDelta = new Vector2(104f, 86f);

            Image background = rootObject.AddComponent<Image>();
            background.color = new Color(0.03f, 0.025f, 0.045f, 0.78f);

            GameObject fillObject = CreateUiObject("Skill Fill", rootObject.transform);
            RectTransform fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0.5f, 0.5f);
            fillRect.anchorMax = new Vector2(0.5f, 0.5f);
            fillRect.pivot = new Vector2(0.5f, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = new Vector2(90f, 68f);

            cooldownFill = fillObject.AddComponent<Image>();
            cooldownFill.type = Image.Type.Filled;
            cooldownFill.fillMethod = Image.FillMethod.Radial360;
            cooldownFill.fillOrigin = 2;
            cooldownFill.fillAmount = 1f;
            cooldownFill.color = readyColor;

            keyText = CreateText(rootObject.transform, "Skill Key Text", keyLabel, 18f, FontStyles.Bold);
            RectTransform keyRect = keyText.rectTransform;
            keyRect.anchorMin = new Vector2(0.5f, 0.5f);
            keyRect.anchorMax = new Vector2(0.5f, 0.5f);
            keyRect.pivot = new Vector2(0.5f, 0.5f);
            keyRect.anchoredPosition = new Vector2(0f, 11f);
            keyRect.sizeDelta = new Vector2(92f, 26f);

            cooldownText = CreateText(rootObject.transform, "Skill Cooldown Text", "READY", 16f, FontStyles.Normal);
            RectTransform cooldownRect = cooldownText.rectTransform;
            cooldownRect.anchorMin = new Vector2(0.5f, 0.5f);
            cooldownRect.anchorMax = new Vector2(0.5f, 0.5f);
            cooldownRect.pivot = new Vector2(0.5f, 0.5f);
            cooldownRect.anchoredPosition = new Vector2(0f, -15f);
            cooldownRect.sizeDelta = new Vector2(92f, 24f);
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
