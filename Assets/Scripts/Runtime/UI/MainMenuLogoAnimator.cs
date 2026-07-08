using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeonBreaker.UI
{
    [DisallowMultipleComponent]
    public sealed class MainMenuLogoAnimator : MonoBehaviour
    {
        [Header("Targets")]
        [SerializeField] private RectTransform logoRoot;
        [SerializeField] private Graphic logoGraphic;
        [SerializeField] private TextMeshProUGUI logoText;

        [Header("Pulse")]
        [SerializeField] private Color baseColor = new Color(0.82f, 0.98f, 1f, 1f);
        [SerializeField] private Color pulseColor = new Color(0.05f, 0.95f, 1f, 1f);
        [SerializeField, Range(0f, 1f)] private float colorPulseAmount = 0.22f;
        [SerializeField, Min(0.01f)] private float pulseFrequency = 0.7f;
        [SerializeField] private Vector3 pulseScale = new Vector3(1.018f, 1.018f, 1f);

        [Header("Glitch")]
        [SerializeField, Min(0.5f)] private float glitchInterval = 4.5f;
        [SerializeField, Min(0.02f)] private float glitchDuration = 0.14f;
        [SerializeField] private Vector2 glitchOffset = new Vector2(5f, 1f);
        [SerializeField, Range(0f, 1f)] private float glitchChance = 0.7f;

        private Vector2 baseAnchoredPosition;
        private Vector3 baseScale;
        private Color cachedBaseColor;
        private float nextGlitchTime;
        private float glitchEndTime;
        private bool glitching;

        private void Awake()
        {
            CacheTargets();
            ScheduleNextGlitch();
        }

        private void OnEnable()
        {
            CacheTargets();
            ScheduleNextGlitch();
        }

        private void OnDisable()
        {
            if (logoRoot != null)
            {
                logoRoot.anchoredPosition = baseAnchoredPosition;
                logoRoot.localScale = baseScale;
            }

            if (logoGraphic != null)
            {
                logoGraphic.color = cachedBaseColor;
            }
        }

        private void Update()
        {
            if (logoRoot == null)
            {
                return;
            }

            float time = Time.unscaledTime;
            float pulse = Mathf.Sin(time * pulseFrequency * Mathf.PI * 2f) * 0.5f + 0.5f;
            logoRoot.localScale = Vector3.Lerp(baseScale, Vector3.Scale(baseScale, pulseScale), pulse);

            if (logoGraphic != null)
            {
                Color target = Color.Lerp(baseColor, pulseColor, pulse * colorPulseAmount);
                target.a = cachedBaseColor.a;
                logoGraphic.color = target;
            }

            UpdateGlitch(time);
        }

        private void CacheTargets()
        {
            if (logoRoot == null)
            {
                logoRoot = transform as RectTransform;
            }

            if (logoGraphic == null)
            {
                logoGraphic = GetComponent<Graphic>();
            }

            if (logoText == null)
            {
                logoText = GetComponent<TextMeshProUGUI>();
            }

            if (logoGraphic == null && logoText != null)
            {
                logoGraphic = logoText;
            }

            if (logoRoot != null)
            {
                baseAnchoredPosition = logoRoot.anchoredPosition;
                baseScale = logoRoot.localScale;
            }

            if (logoGraphic != null)
            {
                cachedBaseColor = logoGraphic.color;
                if (baseColor == default)
                {
                    baseColor = cachedBaseColor;
                }
            }
        }

        private void UpdateGlitch(float time)
        {
            if (!glitching && time >= nextGlitchTime)
            {
                glitching = Random.value <= glitchChance;
                glitchEndTime = time + glitchDuration;
                if (!glitching)
                {
                    ScheduleNextGlitch();
                    return;
                }
            }

            if (!glitching)
            {
                logoRoot.anchoredPosition = Vector2.Lerp(logoRoot.anchoredPosition, baseAnchoredPosition, 0.35f);
                return;
            }

            if (time >= glitchEndTime)
            {
                glitching = false;
                logoRoot.anchoredPosition = baseAnchoredPosition;
                ScheduleNextGlitch();
                return;
            }

            float direction = Random.value > 0.5f ? 1f : -1f;
            logoRoot.anchoredPosition = baseAnchoredPosition + new Vector2(glitchOffset.x * direction, Random.Range(-glitchOffset.y, glitchOffset.y));
        }

        private void ScheduleNextGlitch()
        {
            nextGlitchTime = Time.unscaledTime + glitchInterval + Random.Range(-glitchInterval * 0.25f, glitchInterval * 0.25f);
        }
    }
}
