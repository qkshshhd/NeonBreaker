using NeonBreaker.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeonBreaker.UI
{
    public sealed class RecoilCoreUI : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private PlayerRecoilCore recoilCore;

        [Header("Legacy Gauge")]
        [SerializeField] private Image fillImage;
        [SerializeField] private TextMeshProUGUI valueText;
        [SerializeField] private bool showGaugeBar;
        [SerializeField] private bool showValueText;

        [Header("Screen Feedback")]
        [SerializeField] private Image screenTintImage;
        [SerializeField] private bool useScreenTint;
        [SerializeField] private bool useEdgeTint = true;
        [SerializeField] private bool autoBuildEdgeTint = true;
        [SerializeField] private Image[] edgeTintImages;
        [SerializeField] private bool autoBuildEdgeGlow = true;
        [SerializeField] private Image[] edgeGlowImages;
        [SerializeField] private Material edgeGlowMaterial;
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField, Range(0f, 1f)] private float tintStartRatio = 0.12f;
        [SerializeField, Range(0f, 1f)] private float maxTintAlpha = 0.08f;
        [SerializeField, Range(0f, 1f)] private float edgeMaxAlpha = 0.42f;
        [SerializeField, Min(1f)] private float edgeThickness = 18f;
        [SerializeField, Min(1f)] private float edgeGlowThicknessMultiplier = 2.35f;
        [SerializeField, Range(0f, 1f)] private float edgeGlowAlphaMultiplier = 0.38f;
        [SerializeField, Range(0f, 1f)] private float dangerousPulseStartRatio = 0.72f;
        [SerializeField, Range(0f, 1f)] private float dangerousPulseAlpha = 0.1f;
        [SerializeField, Min(0f)] private float pulseSpeed = 7f;
        [SerializeField, Min(0.01f)] private float visualLerpSpeed = 12f;

        [Header("Discharge Flash")]
        [SerializeField] private bool flashOnDischarge = true;
        [SerializeField, Range(0f, 1f)] private float dischargeFlashAlpha = 0.22f;
        [SerializeField, Min(0.01f)] private float dischargeFlashDuration = 0.18f;

        [Header("Colors")]
        [SerializeField] private Color stableColor = new Color(0.2f, 0.9f, 1f, 1f);
        [SerializeField] private Color overheatedColor = new Color(1f, 0.82f, 0.28f, 1f);
        [SerializeField] private Color dangerousColor = new Color(1f, 0.25f, 0.45f, 1f);

        private float targetRatio;
        private float displayedRatio;
        private float dischargeFlashTimer;

        private void Awake()
        {
            if (recoilCore == null)
            {
                recoilCore = FindAnyObjectByType<PlayerRecoilCore>();
            }

            EnsureEdgeTintImages();
        }

        private void OnEnable()
        {
            if (recoilCore != null)
            {
                recoilCore.RecoilChanged += HandleRecoilChanged;
                recoilCore.RecoilDischarged += HandleRecoilDischarged;
                Refresh(recoilCore.CurrentRecoil, recoilCore.MaxRecoil);
            }
            else
            {
                Refresh(0f, 1f);
            }
        }

        private void OnDisable()
        {
            if (recoilCore != null)
            {
                recoilCore.RecoilChanged -= HandleRecoilChanged;
                recoilCore.RecoilDischarged -= HandleRecoilDischarged;
            }
        }

        private void Update()
        {
            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            displayedRatio = Mathf.MoveTowards(displayedRatio, targetRatio, visualLerpSpeed * deltaTime);

            if (dischargeFlashTimer > 0f)
            {
                dischargeFlashTimer = Mathf.Max(0f, dischargeFlashTimer - deltaTime);
            }

            ApplyVisuals(displayedRatio);
        }

        public void Bind(PlayerRecoilCore target)
        {
            if (recoilCore == target)
            {
                return;
            }

            if (isActiveAndEnabled && recoilCore != null)
            {
                recoilCore.RecoilChanged -= HandleRecoilChanged;
                recoilCore.RecoilDischarged -= HandleRecoilDischarged;
            }

            recoilCore = target;

            if (isActiveAndEnabled && recoilCore != null)
            {
                recoilCore.RecoilChanged += HandleRecoilChanged;
                recoilCore.RecoilDischarged += HandleRecoilDischarged;
                Refresh(recoilCore.CurrentRecoil, recoilCore.MaxRecoil);
            }
        }

        private void HandleRecoilChanged(float current, float max)
        {
            Refresh(current, max);
        }

        private void HandleRecoilDischarged(float ratio)
        {
            if (flashOnDischarge && ratio > 0.001f)
            {
                dischargeFlashTimer = dischargeFlashDuration;
            }
        }

        private void Refresh(float current, float max)
        {
            targetRatio = max <= 0f ? 0f : Mathf.Clamp01(current / max);
            ApplyVisuals(targetRatio);
        }

        private void ApplyVisuals(float ratio)
        {
            if (fillImage != null)
            {
                fillImage.gameObject.SetActive(showGaugeBar);
                fillImage.type = Image.Type.Filled;
                fillImage.fillMethod = Image.FillMethod.Horizontal;
                fillImage.fillAmount = ratio;
                fillImage.color = GetColor(ratio);
            }

            if (valueText != null)
            {
                valueText.gameObject.SetActive(showValueText);
                if (showValueText)
                {
                    valueText.text = $"{Mathf.RoundToInt(ratio * 100f)}%";
                    valueText.color = GetColor(ratio);
                }
            }

            UpdateScreenTint(ratio);
        }

        private void UpdateScreenTint(float ratio)
        {
            Color color = GetColor(ratio);
            float tintAlpha = GetTintAlpha(ratio);
            float flashAlpha = GetDischargeFlashAlpha();

            if (useEdgeTint)
            {
                SetFullScreenTintVisible(false);
                UpdateEdgeTint(color, Mathf.Clamp01(tintAlpha + flashAlpha));
                return;
            }

            HideEdgeTint();

            if (!useScreenTint || screenTintImage == null)
            {
                SetFullScreenTintVisible(false);
                return;
            }

            color.a = Mathf.Clamp01(tintAlpha + flashAlpha);

            screenTintImage.raycastTarget = false;
            screenTintImage.color = color;
            screenTintImage.enabled = color.a > 0.001f;
        }

        private float GetTintAlpha(float ratio)
        {
            if (ratio <= tintStartRatio)
            {
                return 0f;
            }

            float severity = Mathf.InverseLerp(tintStartRatio, 1f, ratio);
            float alpha = severity * maxTintAlpha;

            if (ratio >= dangerousPulseStartRatio && dangerousPulseAlpha > 0f)
            {
                float pulseSeverity = Mathf.InverseLerp(dangerousPulseStartRatio, 1f, ratio);
                float pulse = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;
                alpha += pulse * dangerousPulseAlpha * pulseSeverity;
            }

            return Mathf.Clamp01(alpha);
        }

        private void UpdateEdgeTint(Color color, float baseAlpha)
        {
            EnsureEdgeTintImages();

            if (edgeTintImages == null || edgeTintImages.Length == 0)
            {
                return;
            }

            float edgeAlpha = Mathf.Clamp01(baseAlpha / Mathf.Max(0.001f, maxTintAlpha)) * edgeMaxAlpha;
            color.a = edgeAlpha;

            ApplyEdgeImages(edgeTintImages, color, true);

            Color glowColor = color;
            glowColor.a *= edgeGlowAlphaMultiplier;
            ApplyEdgeImages(edgeGlowImages, glowColor, false);
        }

        private void HideEdgeTint()
        {
            SetEdgeImagesVisible(edgeTintImages, false);
            SetEdgeImagesVisible(edgeGlowImages, false);
        }

        private void SetFullScreenTintVisible(bool visible)
        {
            if (screenTintImage != null)
            {
                screenTintImage.enabled = visible;
            }
        }

        private void EnsureEdgeTintImages()
        {
            if (!autoBuildEdgeTint || !useEdgeTint)
            {
                return;
            }

            Transform parent = screenTintImage != null && screenTintImage.transform.parent != null
                ? screenTintImage.transform.parent
                : transform;

            if (!HasEdgeTintImages())
            {
                edgeTintImages = new Image[4];
                edgeTintImages[0] = CreateEdgeImage(parent, "Recoil Feedback Frame Top");
                edgeTintImages[1] = CreateEdgeImage(parent, "Recoil Feedback Frame Bottom");
                edgeTintImages[2] = CreateEdgeImage(parent, "Recoil Feedback Frame Left");
                edgeTintImages[3] = CreateEdgeImage(parent, "Recoil Feedback Frame Right");
            }

            if (autoBuildEdgeGlow && !HasAnyImages(edgeGlowImages))
            {
                edgeGlowImages = new Image[4];
                edgeGlowImages[0] = CreateEdgeImage(parent, "Recoil Feedback Glow Top");
                edgeGlowImages[1] = CreateEdgeImage(parent, "Recoil Feedback Glow Bottom");
                edgeGlowImages[2] = CreateEdgeImage(parent, "Recoil Feedback Glow Left");
                edgeGlowImages[3] = CreateEdgeImage(parent, "Recoil Feedback Glow Right");
            }

            ConfigureConnectedFrame(edgeGlowImages, edgeThickness * edgeGlowThicknessMultiplier, edgeGlowMaterial);
            ConfigureConnectedFrame(edgeTintImages, edgeThickness, null);
        }

        private bool HasEdgeTintImages()
        {
            return HasAnyImages(edgeTintImages);
        }

        private static bool HasAnyImages(Image[] images)
        {
            if (images == null || images.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static Image CreateEdgeImage(Transform parent, string name)
        {
            GameObject edgeObject = new GameObject(name);
            edgeObject.transform.SetParent(parent, false);

            edgeObject.AddComponent<RectTransform>();
            Image image = edgeObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.enabled = false;
            return image;
        }

        private static void ConfigureConnectedFrame(Image[] images, float thickness, Material material)
        {
            if (images == null || images.Length < 4)
            {
                return;
            }

            ConfigureFramePart(images[0], new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, thickness), material);
            ConfigureFramePart(images[1], new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, thickness), material);
            ConfigureFramePart(images[2], new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(thickness, 0f), material);
            ConfigureFramePart(images[3], new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(thickness, 0f), material);
        }

        private static void ConfigureFramePart(Image image, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta, Material material)
        {
            if (image == null)
            {
                return;
            }

            RectTransform rect = image.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = sizeDelta;

            image.material = material;
            image.raycastTarget = false;
        }

        private static void ApplyEdgeImages(Image[] images, Color color, bool placeInFront)
        {
            if (images == null)
            {
                return;
            }

            for (int i = 0; i < images.Length; i++)
            {
                Image edge = images[i];
                if (edge == null)
                {
                    continue;
                }

                edge.raycastTarget = false;
                edge.color = color;
                edge.enabled = color.a > 0.001f;

                if (placeInFront)
                {
                    edge.transform.SetAsLastSibling();
                }
            }
        }

        private static void SetEdgeImagesVisible(Image[] images, bool visible)
        {
            if (images == null)
            {
                return;
            }

            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null)
                {
                    images[i].enabled = visible;
                }
            }
        }

        private float GetDischargeFlashAlpha()
        {
            if (!flashOnDischarge || dischargeFlashDuration <= 0f || dischargeFlashTimer <= 0f)
            {
                return 0f;
            }

            float normalized = Mathf.Clamp01(dischargeFlashTimer / dischargeFlashDuration);
            return normalized * dischargeFlashAlpha;
        }

        private Color GetColor(float ratio)
        {
            if (ratio >= 0.7f)
            {
                return dangerousColor;
            }

            if (ratio >= 0.3f)
            {
                return overheatedColor;
            }

            return stableColor;
        }
    }
}
