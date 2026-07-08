using NeonBreaker.Combat;
using NeonBreaker.Player;
using UnityEngine;
using UnityEngine.UI;

namespace NeonBreaker.UI
{
    public sealed class LowHealthScreenPressureUI : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private PlayerController player;
        [SerializeField] private Health health;

        [Header("Binding")]
        [SerializeField] private bool disableVisualOutput = true;
        [SerializeField] private Image pressureImage;
        [SerializeField] private bool useEdgePressure = true;
        [SerializeField] private bool autoBuildEdgePressure = true;
        [SerializeField] private Image[] edgePressureImages;
        [SerializeField] private bool autoBuildEdgeGlow = true;
        [SerializeField] private Image[] edgeGlowImages;
        [SerializeField] private Material edgeGlowMaterial;
        [SerializeField] private bool useUnscaledTime = true;

        [Header("Pressure")]
        [SerializeField, Range(0f, 1f)] private float lowHealthThreshold = 0.3f;
        [SerializeField, Range(0f, 1f)] private float maxPressureAlpha = 0.08f;
        [SerializeField, Range(0f, 1f)] private float edgeMaxPressureAlpha = 0.48f;
        [SerializeField, Range(0f, 1f)] private float pulseAlpha = 0.08f;
        [SerializeField, Min(1f)] private float edgeThickness = 22f;
        [SerializeField, Min(1f)] private float edgeGlowThicknessMultiplier = 2.35f;
        [SerializeField, Range(0f, 1f)] private float edgeGlowAlphaMultiplier = 0.38f;
        [SerializeField, Min(0f)] private float pulseSpeed = 5.5f;
        [SerializeField, Range(0.25f, 3f)] private float pressureCurve = 1.35f;

        [Header("Damage Kick")]
        [SerializeField, Range(0f, 1f)] private float damageKickAlpha = 0.12f;
        [SerializeField, Min(0.01f)] private float damageKickDuration = 0.18f;

        [Header("Color")]
        [SerializeField] private Color pressureColor = new Color(1f, 0.05f, 0.18f, 1f);

        private float healthRatio = 1f;
        private float damageKickTimer;

        private void Awake()
        {
            ResolveSources();
            ConfigureImage();
            EnsureEdgePressureImages();
            RefreshImmediate();
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
            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (damageKickTimer > 0f)
            {
                damageKickTimer = Mathf.Max(0f, damageKickTimer - deltaTime);
            }

            RefreshVisual();
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

            if (health == null && player != null)
            {
                health = player.Health;
            }

            if (pressureImage == null)
            {
                pressureImage = GetComponent<Image>();
            }
        }

        private void BindHealth()
        {
            ResolveSources();
            if (health == null)
            {
                SetFullScreenPressureVisible(false);
                HideEdgePressure();
                return;
            }

            health.HealthChanged += HandleHealthChanged;
            health.Damaged += HandleDamaged;
            HandleHealthChanged(health.CurrentHealth, health.MaxHealth);
        }

        private void UnbindHealth()
        {
            if (health == null)
            {
                return;
            }

            health.HealthChanged -= HandleHealthChanged;
            health.Damaged -= HandleDamaged;
        }

        private void HandleHealthChanged(float current, float max)
        {
            healthRatio = max <= 0f ? 0f : Mathf.Clamp01(current / max);
            RefreshVisual();
        }

        private void HandleDamaged(DamageInfo damage)
        {
            if (damage.Amount <= 0f)
            {
                return;
            }

            damageKickTimer = damageKickDuration;
        }

        private void RefreshImmediate()
        {
            if (health != null)
            {
                healthRatio = health.MaxHealth <= 0f ? 0f : Mathf.Clamp01(health.CurrentHealth / health.MaxHealth);
            }

            RefreshVisual();
        }

        private void RefreshVisual()
        {
            if (disableVisualOutput)
            {
                SetFullScreenPressureVisible(false);
                HideEdgePressure();
                return;
            }

            float pressure = GetPressureAmount();
            float alpha = pressure * maxPressureAlpha;
            alpha += GetPulseAlpha(pressure);
            alpha += GetDamageKickAlpha();

            Color color = pressureColor;
            color.a = Mathf.Clamp01(alpha);

            if (useEdgePressure)
            {
                SetFullScreenPressureVisible(false);
                UpdateEdgePressure(color);
                return;
            }

            HideEdgePressure();

            if (pressureImage == null)
            {
                return;
            }

            pressureImage.color = color;
            SetFullScreenPressureVisible(color.a > 0.001f);
        }

        private float GetPressureAmount()
        {
            if (healthRatio <= 0f)
            {
                return 1f;
            }

            if (healthRatio >= lowHealthThreshold)
            {
                return 0f;
            }

            float pressure = Mathf.InverseLerp(lowHealthThreshold, 0f, healthRatio);
            return Mathf.Pow(pressure, Mathf.Max(0.25f, pressureCurve));
        }

        private float GetPulseAlpha(float pressure)
        {
            if (pressure <= 0f || pulseAlpha <= 0f)
            {
                return 0f;
            }

            float pulse = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;
            return pulse * pulseAlpha * pressure;
        }

        private float GetDamageKickAlpha()
        {
            if (damageKickDuration <= 0f || damageKickTimer <= 0f)
            {
                return 0f;
            }

            float normalized = Mathf.Clamp01(damageKickTimer / damageKickDuration);
            return normalized * damageKickAlpha;
        }

        private void ConfigureImage()
        {
            if (pressureImage == null)
            {
                return;
            }

            pressureImage.raycastTarget = false;
        }

        private void UpdateEdgePressure(Color fullScreenColor)
        {
            EnsureEdgePressureImages();

            if (edgePressureImages == null || edgePressureImages.Length == 0)
            {
                return;
            }

            float fullMaxAlpha = Mathf.Max(0.001f, maxPressureAlpha + pulseAlpha + damageKickAlpha);
            fullScreenColor.a = Mathf.Clamp01(fullScreenColor.a / fullMaxAlpha) * edgeMaxPressureAlpha;

            ApplyEdgeImages(edgePressureImages, fullScreenColor, true);

            Color glowColor = fullScreenColor;
            glowColor.a *= edgeGlowAlphaMultiplier;
            ApplyEdgeImages(edgeGlowImages, glowColor, false);
        }

        private void HideEdgePressure()
        {
            SetEdgeImagesVisible(edgePressureImages, false);
            SetEdgeImagesVisible(edgeGlowImages, false);
        }

        private void SetFullScreenPressureVisible(bool visible)
        {
            if (pressureImage != null)
            {
                pressureImage.enabled = visible;
            }
        }

        private void EnsureEdgePressureImages()
        {
            if (!autoBuildEdgePressure || !useEdgePressure)
            {
                return;
            }

            Transform parent = pressureImage != null && pressureImage.transform.parent != null
                ? pressureImage.transform.parent
                : transform;

            if (!HasEdgePressureImages())
            {
                edgePressureImages = new Image[4];
                edgePressureImages[0] = CreateEdgeImage(parent, "Low Health Feedback Frame Top");
                edgePressureImages[1] = CreateEdgeImage(parent, "Low Health Feedback Frame Bottom");
                edgePressureImages[2] = CreateEdgeImage(parent, "Low Health Feedback Frame Left");
                edgePressureImages[3] = CreateEdgeImage(parent, "Low Health Feedback Frame Right");
            }

            if (autoBuildEdgeGlow && !HasAnyImages(edgeGlowImages))
            {
                edgeGlowImages = new Image[4];
                edgeGlowImages[0] = CreateEdgeImage(parent, "Low Health Feedback Glow Top");
                edgeGlowImages[1] = CreateEdgeImage(parent, "Low Health Feedback Glow Bottom");
                edgeGlowImages[2] = CreateEdgeImage(parent, "Low Health Feedback Glow Left");
                edgeGlowImages[3] = CreateEdgeImage(parent, "Low Health Feedback Glow Right");
            }

            ConfigureConnectedFrame(edgeGlowImages, edgeThickness * edgeGlowThicknessMultiplier, edgeGlowMaterial);
            ConfigureConnectedFrame(edgePressureImages, edgeThickness, null);
        }

        private bool HasEdgePressureImages()
        {
            if (edgePressureImages == null || edgePressureImages.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < edgePressureImages.Length; i++)
            {
                if (edgePressureImages[i] != null)
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
    }
}
