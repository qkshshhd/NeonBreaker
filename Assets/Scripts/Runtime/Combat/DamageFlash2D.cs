using System.Collections;
using UnityEngine;

namespace NeonBreaker.Combat
{
    [RequireComponent(typeof(Health))]
    public sealed class DamageFlash2D : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private SpriteRenderer[] renderers;
        [SerializeField] private bool autoFindRenderersOnEnable = true;
        [SerializeField] private bool includeInactiveRenderers = true;
        [SerializeField] private Color flashColor = Color.white;
        [SerializeField] private Color criticalFlashColor = new Color(1f, 0.85f, 0.25f, 1f);
        [SerializeField] private float flashDuration = 0.08f;
        [SerializeField] private float criticalFlashDuration = 0.12f;

        private Health health;
        private Color[] originalColors;
        private Coroutine flashRoutine;

        private void Awake()
        {
            health = GetComponent<Health>();
            ResolveVisualRoot();
            RefreshRenderers();
        }

        private void OnEnable()
        {
            if (autoFindRenderersOnEnable || renderers == null || renderers.Length == 0)
            {
                ResolveVisualRoot();
                RefreshRenderers();
            }

            if (health != null)
            {
                health.Damaged += HandleDamaged;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Damaged -= HandleDamaged;
            }

            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
                flashRoutine = null;
            }

            RestoreColors();
        }

        private void HandleDamaged(DamageInfo damage)
        {
            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
            }

            flashRoutine = StartCoroutine(FlashRoutine(damage.IsCritical));
        }

        private IEnumerator FlashRoutine(bool isCritical)
        {
            SetColor(isCritical ? criticalFlashColor : flashColor);

            float duration = isCritical ? criticalFlashDuration : flashDuration;
            if (duration > 0f)
            {
                yield return new WaitForSeconds(duration);
            }

            RestoreColors();
            flashRoutine = null;
        }

        private void SetColor(Color color)
        {
            EnsureRendererCache();

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

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && i < originalColors.Length)
                {
                    renderers[i].color = originalColors[i];
                }
            }
        }

        private void EnsureRendererCache()
        {
            if (renderers == null || renderers.Length == 0)
            {
                ResolveVisualRoot();
                RefreshRenderers();
            }
        }

        private void RefreshRenderers()
        {
            Transform searchRoot = visualRoot != null ? visualRoot : transform;
            renderers = searchRoot.GetComponentsInChildren<SpriteRenderer>(includeInactiveRenderers);

            originalColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                originalColors[i] = renderers[i] != null ? renderers[i].color : Color.white;
            }
        }

        private void ResolveVisualRoot()
        {
            if (visualRoot != null)
            {
                return;
            }

            visualRoot = FindVisualRoot(transform);
        }

        private static Transform FindVisualRoot(Transform root)
        {
            if (root == null)
            {
                return null;
            }

            Transform directMatch = root.Find("VisualRoot");
            if (directMatch != null)
            {
                return directMatch;
            }

            directMatch = root.Find("Visual Root");
            if (directMatch != null)
            {
                return directMatch;
            }

            return FindVisualRootRecursive(root);
        }

        private static Transform FindVisualRootRecursive(Transform current)
        {
            string normalizedName = current.name.Replace(" ", string.Empty).ToLowerInvariant();
            if (normalizedName == "visualroot")
            {
                return current;
            }

            for (int i = 0; i < current.childCount; i++)
            {
                Transform match = FindVisualRootRecursive(current.GetChild(i));
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }
    }
}
