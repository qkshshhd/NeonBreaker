using NeonBreaker.Combat;
using NeonBreaker.Player;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NeonBreaker.UI
{
    public sealed class LowHealthVolumeFeedback2D : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private PlayerController player;
        [SerializeField] private Health health;
        [SerializeField] private bool retryBindingUntilFound = true;

        [Header("Volume")]
        [SerializeField] private Volume targetVolume;
        [SerializeField] private bool createRuntimeVolumeIfMissing = true;
        [SerializeField] private bool cloneProfileAtRuntime = true;
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField, Min(0f)] private float volumePriority = 100f;

        [Header("Low Health")]
        [SerializeField, Range(0f, 1f)] private float lowHealthThreshold = 0.32f;
        [SerializeField, Min(0.01f)] private float enterSpeed = 8f;
        [SerializeField, Min(0.01f)] private float exitSpeed = 4f;
        [SerializeField, Range(0.25f, 3f)] private float pressureCurve = 1.35f;
        [SerializeField, Range(0.1f, 1f)] private float pressureOutputScale = 0.62f;
        [SerializeField, Range(0.1f, 1f)] private float maxDisplayedPressure = 0.72f;

        [Header("Low Health Pulse")]
        [SerializeField, Min(0f)] private float pulseSpeed = 4.4f;
        [SerializeField, Range(0f, 1f)] private float pulseAmount = 0.24f;
        [SerializeField, Range(0f, 1f)] private float vignettePulseBoost = 0.055f;
        [SerializeField, Range(0f, 4f)] private float bloomPulseBoost = 0.42f;
        [SerializeField, Range(0f, 1f)] private float colorPulseBoost = 0.045f;

        [Header("Damage Kick")]
        [SerializeField, Range(0f, 1f)] private float damageKickAmount = 0.16f;
        [SerializeField, Min(0.01f)] private float damageKickDuration = 0.16f;

        [Header("Vignette")]
        [SerializeField] private Color hurtColor = new Color(1f, 0.02f, 0.08f, 1f);
        [SerializeField, Range(0f, 1f)] private float maxVignetteIntensity = 0.26f;
        [SerializeField, Range(0f, 1f)] private float maxVignetteSmoothness = 0.48f;

        [Header("Bloom")]
        [SerializeField, Range(0f, 8f)] private float bloomIntensityBoost = 0.32f;
        [SerializeField, Range(0f, 1f)] private float bloomScatter = 0.42f;
        [SerializeField, Range(0f, 1f)] private float bloomThreshold = 0.82f;

        [Header("Color Adjustments")]
        [SerializeField, Range(-100f, 100f)] private float maxSaturationDrop = -10f;
        [SerializeField, Range(-100f, 100f)] private float maxContrastBoost = 5f;
        [SerializeField, Range(-2f, 2f)] private float maxPostExposureDrop = -0.06f;

        [Header("Runtime Debug")]
        [SerializeField] private bool logBindingProblems;
        [SerializeField] private float debugHealthRatio = 1f;
        [SerializeField] private float debugTargetPressure;
        [SerializeField] private float debugDisplayedPressure;
        [SerializeField] private float debugPulse;
        [SerializeField] private float debugBloomIntensity;
        [SerializeField] private string debugBoundHealth;
        [SerializeField] private string debugTargetVolume;

        private float healthRatio = 1f;
        private float displayedPressure;
        private float damageKickTimer;
        private VolumeProfile runtimeProfile;
        private Vignette vignette;
        private Bloom bloom;
        private ColorAdjustments colorAdjustments;
        private bool bound;

        private float baseVignetteIntensity;
        private float baseVignetteSmoothness;
        private Color baseVignetteColor = Color.black;
        private float baseBloomIntensity;
        private float baseBloomScatter;
        private float baseBloomThreshold;
        private Color baseBloomTint = Color.white;
        private float baseSaturation;
        private float baseContrast;
        private float basePostExposure;
        private Color baseColorFilter = Color.white;

        private void Awake()
        {
            ResolveSources();
            SetupVolume();
        }

        private void OnEnable()
        {
            BindHealth();
            RefreshImmediate();
        }

        private void OnDisable()
        {
            UnbindHealth();
            RestoreVolume();
        }

        private void Update()
        {
            if (!bound && retryBindingUntilFound)
            {
                BindHealth();
            }

            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (damageKickTimer > 0f)
            {
                damageKickTimer = Mathf.Max(0f, damageKickTimer - deltaTime);
            }

            float targetPressure = GetTargetPressure();
            float speed = targetPressure > displayedPressure ? enterSpeed : exitSpeed;
            displayedPressure = Mathf.MoveTowards(displayedPressure, targetPressure, speed * deltaTime);
            ApplyVolume(displayedPressure);
            RefreshDebugValues(targetPressure);
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
                if (health == null)
                {
                    player.TryGetComponent(out health);
                }
            }
        }

        private void BindHealth()
        {
            ResolveSources();
            if (health == null || bound)
            {
                if (health == null && logBindingProblems)
                {
                    Debug.LogWarning(
                        "[LowHealthVolumeFeedback2D] Player Health is not ready yet. " +
                        "The component will retry binding during Update.",
                        this);
                }

                return;
            }

            health.HealthChanged += HandleHealthChanged;
            health.Damaged += HandleDamaged;
            bound = true;
            debugBoundHealth = health.name;
            HandleHealthChanged(health.CurrentHealth, health.MaxHealth);
        }

        private void UnbindHealth()
        {
            if (health == null || !bound)
            {
                return;
            }

            health.HealthChanged -= HandleHealthChanged;
            health.Damaged -= HandleDamaged;
            bound = false;
            debugBoundHealth = string.Empty;
        }

        private void HandleHealthChanged(float current, float max)
        {
            healthRatio = max <= 0f ? 0f : Mathf.Clamp01(current / max);
            debugHealthRatio = healthRatio;
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

            displayedPressure = GetTargetPressure();
            ApplyVolume(displayedPressure);
        }

        private void SetupVolume()
        {
            if (targetVolume == null)
            {
                targetVolume = FindAnyObjectByType<Volume>();
            }

            if (targetVolume == null && createRuntimeVolumeIfMissing)
            {
                GameObject volumeObject = new GameObject("Low Health Runtime Volume");
                volumeObject.transform.SetParent(transform, false);
                targetVolume = volumeObject.AddComponent<Volume>();
                targetVolume.isGlobal = true;
                targetVolume.priority = volumePriority;
            }

            if (targetVolume == null)
            {
                if (logBindingProblems)
                {
                    Debug.LogWarning(
                        "[LowHealthVolumeFeedback2D] Target Volume is missing and runtime volume creation is disabled.",
                        this);
                }

                return;
            }

            targetVolume.isGlobal = true;
            targetVolume.priority = Mathf.Max(targetVolume.priority, volumePriority);
            debugTargetVolume = targetVolume.name;

            VolumeProfile sourceProfile = targetVolume.profile != null
                ? targetVolume.profile
                : ScriptableObject.CreateInstance<VolumeProfile>();

            runtimeProfile = cloneProfileAtRuntime ? Instantiate(sourceProfile) : sourceProfile;
            targetVolume.profile = runtimeProfile;

            EnsureVolumeComponent(out vignette);
            EnsureVolumeComponent(out bloom);
            EnsureVolumeComponent(out colorAdjustments);
            CacheBaseValues();
        }

        private void EnsureVolumeComponent<T>(out T component) where T : VolumeComponent
        {
            component = null;
            if (runtimeProfile == null)
            {
                RefreshDebugValues(0f);
                return;
            }

            if (!runtimeProfile.TryGet(out component) || component == null)
            {
                component = runtimeProfile.Add<T>(true);
            }

            component.active = true;
        }

        private void CacheBaseValues()
        {
            if (vignette != null)
            {
                baseVignetteIntensity = vignette.intensity.value;
                baseVignetteSmoothness = vignette.smoothness.value;
                baseVignetteColor = vignette.color.value;
                vignette.intensity.overrideState = true;
                vignette.smoothness.overrideState = true;
                vignette.color.overrideState = true;
            }

            if (bloom != null)
            {
                baseBloomIntensity = bloom.intensity.value;
                baseBloomScatter = bloom.scatter.value;
                baseBloomThreshold = bloom.threshold.value;
                baseBloomTint = bloom.tint.value;
                bloom.intensity.overrideState = true;
                bloom.scatter.overrideState = true;
                bloom.threshold.overrideState = true;
                bloom.tint.overrideState = true;
            }

            if (colorAdjustments != null)
            {
                baseSaturation = colorAdjustments.saturation.value;
                baseContrast = colorAdjustments.contrast.value;
                basePostExposure = colorAdjustments.postExposure.value;
                baseColorFilter = colorAdjustments.colorFilter.value;
                colorAdjustments.saturation.overrideState = true;
                colorAdjustments.contrast.overrideState = true;
                colorAdjustments.postExposure.overrideState = true;
                colorAdjustments.colorFilter.overrideState = true;
            }
        }

        private float GetTargetPressure()
        {
            float lowHealthPressure = 0f;
            if (healthRatio <= 0f)
            {
                lowHealthPressure = 1f;
            }
            else if (healthRatio < lowHealthThreshold)
            {
                lowHealthPressure = Mathf.InverseLerp(lowHealthThreshold, 0f, healthRatio);
                lowHealthPressure = Mathf.Pow(lowHealthPressure, Mathf.Max(0.25f, pressureCurve));
            }

            float kick = damageKickDuration <= 0f || damageKickTimer <= 0f
                ? 0f
                : Mathf.Clamp01(damageKickTimer / damageKickDuration) * damageKickAmount;

            float scaledPressure = (lowHealthPressure + kick) * pressureOutputScale;
            return Mathf.Min(maxDisplayedPressure, Mathf.Clamp01(scaledPressure));
        }

        private void ApplyVolume(float pressure)
        {
            if (runtimeProfile == null)
            {
                return;
            }

            float basePressure = Mathf.Clamp01(pressure);
            float pulse = GetPulseAmount(basePressure);
            float pulsedPressure = Mathf.Clamp01(basePressure + pulse * pulseAmount);

            if (vignette != null)
            {
                vignette.color.value = Color.Lerp(baseVignetteColor, hurtColor, pulsedPressure);
                vignette.intensity.value = Mathf.Clamp01(
                    Mathf.Lerp(baseVignetteIntensity, maxVignetteIntensity, basePressure)
                    + vignettePulseBoost * pulse);
                vignette.smoothness.value = Mathf.Clamp01(
                    Mathf.Lerp(baseVignetteSmoothness, maxVignetteSmoothness, basePressure)
                    + vignettePulseBoost * 0.45f * pulse);
            }

            if (bloom != null)
            {
                bloom.tint.value = Color.Lerp(baseBloomTint, hurtColor, pulsedPressure);
                bloom.intensity.value = baseBloomIntensity
                    + bloomIntensityBoost * basePressure
                    + bloomPulseBoost * pulse;
                bloom.scatter.value = Mathf.Lerp(baseBloomScatter, bloomScatter, pulsedPressure);
                bloom.threshold.value = Mathf.Lerp(baseBloomThreshold, bloomThreshold, basePressure);
                debugBloomIntensity = bloom.intensity.value;
            }

            if (colorAdjustments != null)
            {
                float colorPressure = Mathf.Clamp01(basePressure * 0.38f + colorPulseBoost * pulse);
                colorAdjustments.colorFilter.value = Color.Lerp(baseColorFilter, hurtColor, colorPressure);
                colorAdjustments.saturation.value = baseSaturation + maxSaturationDrop * pulsedPressure;
                colorAdjustments.contrast.value = baseContrast + maxContrastBoost * pulsedPressure;
                colorAdjustments.postExposure.value = basePostExposure + maxPostExposureDrop * pulsedPressure;
            }
        }

        private float GetPulseAmount(float pressure)
        {
            if (pressure <= 0.001f || pulseSpeed <= 0f || pulseAmount <= 0f)
            {
                debugPulse = 0f;
                return 0f;
            }

            float time = useUnscaledTime ? Time.unscaledTime : Time.time;
            float wave = (Mathf.Sin(time * pulseSpeed) + 1f) * 0.5f;
            float pulse = wave * pressure;
            debugPulse = pulse;
            return pulse;
        }

        private void RestoreVolume()
        {
            ApplyVolume(0f);
            RefreshDebugValues(0f);
        }

        private void RefreshDebugValues(float targetPressure)
        {
            debugHealthRatio = healthRatio;
            debugTargetPressure = targetPressure;
            debugDisplayedPressure = displayedPressure;
            debugPulse = GetPulseAmount(Mathf.Clamp01(displayedPressure));
            debugBloomIntensity = bloom != null ? bloom.intensity.value : 0f;
        }
    }
}
