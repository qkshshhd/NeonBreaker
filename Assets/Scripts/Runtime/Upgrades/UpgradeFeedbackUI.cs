using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeonBreaker.Upgrades
{
    public sealed class UpgradeFeedbackUI : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private UpgradeManager upgradeManager;

        [Header("Toast Bindings")]
        [SerializeField] private CanvasGroup toastRoot;
        [SerializeField] private TextMeshProUGUI toastTitleText;
        [SerializeField] private TextMeshProUGUI toastDescriptionText;

        [Header("List Bindings")]
        [SerializeField] private RectTransform listRoot;
        [SerializeField] private TextMeshProUGUI listItemPrefab;

        [Header("Options")]
        [SerializeField] private bool buildFallbackUiIfMissing = true;
        [SerializeField] private bool showToast = true;
        [SerializeField] private bool showAcquiredList = true;
        [SerializeField] private int maxVisibleListItems = 6;

        [Header("Timing")]
        [SerializeField] private float fadeInDuration = 0.12f;
        [SerializeField] private float holdDuration = 1.15f;
        [SerializeField] private float fadeOutDuration = 0.35f;

        private readonly Dictionary<UpgradeDefinition, TextMeshProUGUI> acquiredItems = new Dictionary<UpgradeDefinition, TextMeshProUGUI>();
        private readonly List<UpgradeDefinition> acquiredOrder = new List<UpgradeDefinition>();
        private Coroutine toastRoutine;

        private void Awake()
        {
            if (upgradeManager == null)
            {
                upgradeManager = FindAnyObjectByType<UpgradeManager>();
            }

            if (buildFallbackUiIfMissing && (toastRoot == null || listRoot == null))
            {
                BuildFallbackUi();
            }

            SetToastAlpha(0f);
            RefreshListVisibility();
        }

        private void OnEnable()
        {
            if (upgradeManager != null)
            {
                upgradeManager.UpgradeSelected += HandleUpgradeSelected;
                upgradeManager.UpgradeLevelChanged += HandleUpgradeLevelChanged;
            }
        }

        private void OnDisable()
        {
            if (upgradeManager != null)
            {
                upgradeManager.UpgradeSelected -= HandleUpgradeSelected;
                upgradeManager.UpgradeLevelChanged -= HandleUpgradeLevelChanged;
            }

            if (toastRoutine != null)
            {
                StopCoroutine(toastRoutine);
                toastRoutine = null;
            }
        }

        private void HandleUpgradeSelected(UpgradeDefinition upgrade)
        {
            if (upgrade == null)
            {
                return;
            }

            int level = upgradeManager != null ? upgradeManager.GetUpgradeLevel(upgrade) : 0;
            UpdateAcquiredList(upgrade, level);

            if (showToast)
            {
                ShowToast(upgrade, level);
            }
        }

        private void HandleUpgradeLevelChanged(UpgradeDefinition upgrade, int level)
        {
            if (upgrade == null)
            {
                return;
            }

            UpdateAcquiredList(upgrade, level);
        }

        private void ShowToast(UpgradeDefinition upgrade, int level)
        {
            SetText(toastTitleText, $"{upgrade.DisplayName} Lv.{Mathf.Max(1, level)}");
            SetText(toastDescriptionText, upgrade.Description);

            if (toastRoutine != null)
            {
                StopCoroutine(toastRoutine);
            }

            toastRoutine = StartCoroutine(ToastRoutine());
        }

        private IEnumerator ToastRoutine()
        {
            yield return FadeToast(0f, 1f, fadeInDuration);

            if (holdDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(holdDuration);
            }

            yield return FadeToast(1f, 0f, fadeOutDuration);
            toastRoutine = null;
        }

        private IEnumerator FadeToast(float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                SetToastAlpha(to);
                yield break;
            }

            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / duration);
                SetToastAlpha(Mathf.Lerp(from, to, t));
                yield return null;
            }

            SetToastAlpha(to);
        }

        private void UpdateAcquiredList(UpgradeDefinition upgrade, int level)
        {
            if (!showAcquiredList || listRoot == null || listItemPrefab == null)
            {
                return;
            }

            if (!acquiredItems.TryGetValue(upgrade, out TextMeshProUGUI item) || item == null)
            {
                item = Instantiate(listItemPrefab, listRoot);
                item.gameObject.SetActive(true);
                acquiredItems[upgrade] = item;
                acquiredOrder.Add(upgrade);
            }

            item.text = $"{upgrade.DisplayName}  Lv.{Mathf.Max(1, level)}";
            RefreshListVisibility();
        }

        private void RefreshListVisibility()
        {
            if (listRoot == null)
            {
                return;
            }

            bool visible = showAcquiredList && acquiredOrder.Count > 0;
            listRoot.gameObject.SetActive(visible);

            if (!visible)
            {
                return;
            }

            int firstVisible = Mathf.Max(0, acquiredOrder.Count - Mathf.Max(1, maxVisibleListItems));
            for (int i = 0; i < acquiredOrder.Count; i++)
            {
                UpgradeDefinition upgrade = acquiredOrder[i];
                if (upgrade == null || !acquiredItems.TryGetValue(upgrade, out TextMeshProUGUI item) || item == null)
                {
                    continue;
                }

                item.gameObject.SetActive(i >= firstVisible);
            }
        }

        private void BuildFallbackUi()
        {
            GameObject canvasObject = new GameObject("Upgrade Feedback Canvas");
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 420;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject toastObject = CreateUiObject("Upgrade Toast Root", canvasObject.transform);
            toastRoot = toastObject.AddComponent<CanvasGroup>();

            RectTransform toastRect = toastObject.GetComponent<RectTransform>();
            toastRect.anchorMin = new Vector2(0.5f, 0.12f);
            toastRect.anchorMax = new Vector2(0.5f, 0.12f);
            toastRect.pivot = new Vector2(0.5f, 0.5f);
            toastRect.anchoredPosition = Vector2.zero;
            toastRect.sizeDelta = new Vector2(620f, 112f);

            Image toastBackground = toastObject.AddComponent<Image>();
            toastBackground.raycastTarget = false;
            toastBackground.color = new Color(0.025f, 0.035f, 0.055f, 0.86f);

            VerticalLayoutGroup toastLayout = toastObject.AddComponent<VerticalLayoutGroup>();
            toastLayout.padding = new RectOffset(22, 22, 16, 14);
            toastLayout.spacing = 6f;
            toastLayout.childControlWidth = true;
            toastLayout.childControlHeight = false;
            toastLayout.childForceExpandWidth = true;
            toastLayout.childForceExpandHeight = false;

            toastTitleText = CreateText(toastObject.transform, "Upgrade Toast Title", "Upgrade", 26f, FontStyles.Bold);
            toastTitleText.color = new Color(0.42f, 0.96f, 1f, 1f);
            AddLayout(toastTitleText.gameObject, 576f, 34f);

            toastDescriptionText = CreateText(toastObject.transform, "Upgrade Toast Description", "Description", 18f, FontStyles.Normal);
            toastDescriptionText.color = new Color(0.86f, 0.94f, 1f, 0.92f);
            AddLayout(toastDescriptionText.gameObject, 576f, 42f);

            GameObject listObject = CreateUiObject("Acquired Upgrade List", canvasObject.transform);
            listRoot = listObject.GetComponent<RectTransform>();
            listRoot.anchorMin = new Vector2(0f, 0.5f);
            listRoot.anchorMax = new Vector2(0f, 0.5f);
            listRoot.pivot = new Vector2(0f, 0.5f);
            listRoot.anchoredPosition = new Vector2(24f, -48f);
            listRoot.sizeDelta = new Vector2(300f, 280f);

            VerticalLayoutGroup listLayout = listObject.AddComponent<VerticalLayoutGroup>();
            listLayout.spacing = 5f;
            listLayout.childControlWidth = true;
            listLayout.childControlHeight = false;
            listLayout.childForceExpandWidth = true;
            listLayout.childForceExpandHeight = false;

            listItemPrefab = CreateText(listObject.transform, "Upgrade List Item Template", "Upgrade Lv.1", 17f, FontStyles.Normal);
            listItemPrefab.color = new Color(0.82f, 0.94f, 1f, 0.88f);
            AddLayout(listItemPrefab.gameObject, 300f, 24f);
            listItemPrefab.gameObject.SetActive(false);
        }

        private void SetToastAlpha(float alpha)
        {
            if (toastRoot == null)
            {
                return;
            }

            toastRoot.alpha = alpha;
            toastRoot.interactable = false;
            toastRoot.blocksRaycasts = false;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string objectName, string value, float size, FontStyles style)
        {
            GameObject textObject = CreateUiObject(objectName, parent);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Left;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private static GameObject CreateUiObject(string objectName, Transform parent)
        {
            GameObject uiObject = new GameObject(objectName);
            uiObject.transform.SetParent(parent, false);
            uiObject.AddComponent<RectTransform>();
            return uiObject;
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
