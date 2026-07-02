using System.Collections;
using NeonBreaker.Combat;
using UnityEngine;

namespace NeonBreaker.Player
{
    [RequireComponent(typeof(Health))]
    public sealed class PlayerDamageFeedback2D : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private SpriteRenderer[] renderers;

        [Header("Hit Feedback")]
        [SerializeField] private Color hitFlashColor = new Color(1f, 0.22f, 0.22f, 1f);
        [SerializeField, Min(0f)] private float hitFlashDuration = 0.08f;
        [SerializeField] private bool playHitStop = true;
        [SerializeField, Min(0f)] private float hitStopDuration = 0.035f;
        [SerializeField, Range(0.01f, 1f)] private float hitStopTimeScale = 0.08f;
        [SerializeField] private bool playCameraShake = true;
        [SerializeField, Min(0f)] private float cameraShakeDuration = 0.08f;
        [SerializeField, Min(0f)] private float cameraShakeStrength = 0.09f;

        [Header("Invulnerability Blink")]
        [SerializeField] private bool blinkWhileInvulnerable = true;
        [SerializeField] private Color invulnerableBlinkColor = new Color(1f, 1f, 1f, 0.42f);
        [SerializeField, Min(0.01f)] private float blinkInterval = 0.06f;

        [Header("Death")]
        [SerializeField] private bool tintOnDeath = true;
        [SerializeField] private Color deathColor = new Color(0.22f, 0.22f, 0.28f, 1f);

        private Health health;
        private Color[] originalColors;
        private Coroutine feedbackRoutine;

        private void Awake()
        {
            health = GetComponent<Health>();

            if (renderers == null || renderers.Length == 0)
            {
                renderers = GetComponentsInChildren<SpriteRenderer>(true);
            }

            CacheOriginalColors();
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.Damaged += HandleDamaged;
                health.Died += HandleDied;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Damaged -= HandleDamaged;
                health.Died -= HandleDied;
            }

            StopFeedbackRoutine();
            RestoreColors();
        }

        private void HandleDamaged(DamageInfo damage)
        {
            if (playHitStop)
            {
                HitStop2D.Play(hitStopDuration, hitStopTimeScale);
            }

            if (playCameraShake)
            {
                CameraShake2D.Shake(cameraShakeDuration, cameraShakeStrength);
            }

            StopFeedbackRoutine();
            feedbackRoutine = StartCoroutine(DamageRoutine());
        }

        private void HandleDied()
        {
            StopFeedbackRoutine();

            if (tintOnDeath)
            {
                SetColor(deathColor);
            }
        }

        private IEnumerator DamageRoutine()
        {
            SetColor(hitFlashColor);

            if (hitFlashDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(hitFlashDuration);
            }

            if (blinkWhileInvulnerable && health != null)
            {
                bool showBlinkColor = false;
                while (health.IsInvulnerable && !health.IsDead)
                {
                    SetColor(showBlinkColor ? invulnerableBlinkColor : Color.white);
                    showBlinkColor = !showBlinkColor;
                    yield return new WaitForSecondsRealtime(blinkInterval);
                }
            }

            RestoreColors();
            feedbackRoutine = null;
        }

        private void CacheOriginalColors()
        {
            originalColors = new Color[renderers != null ? renderers.Length : 0];
            for (int i = 0; i < originalColors.Length; i++)
            {
                originalColors[i] = renderers[i] != null ? renderers[i].color : Color.white;
            }
        }

        private void StopFeedbackRoutine()
        {
            if (feedbackRoutine == null)
            {
                return;
            }

            StopCoroutine(feedbackRoutine);
            feedbackRoutine = null;
        }

        private void SetColor(Color color)
        {
            if (renderers == null)
            {
                return;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].color = color;
                }
            }
        }

        private void RestoreColors()
        {
            if (renderers == null || originalColors == null)
            {
                return;
            }

            for (int i = 0; i < renderers.Length && i < originalColors.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].color = originalColors[i];
                }
            }
        }
    }
}
