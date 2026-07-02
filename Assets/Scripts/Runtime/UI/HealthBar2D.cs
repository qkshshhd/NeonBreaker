using NeonBreaker.Combat;
using UnityEngine;

namespace NeonBreaker.UI
{
    public sealed class HealthBar2D : MonoBehaviour
    {
        private enum FillOrigin
        {
            Left,
            Center,
            Right
        }

        [SerializeField] private Health target;
        [SerializeField] private Transform fill;
        [SerializeField] private FillOrigin fillOrigin = FillOrigin.Left;
        [SerializeField] private bool hideWhenFull = true;
        [SerializeField] private GameObject root;

        private Vector3 fillFullScale;
        private Vector3 fillFullLocalPosition;
        private float fillFullLocalWidth;
        private Renderer[] renderersToToggle;

        private void Awake()
        {
            if (root == null)
            {
                root = gameObject;
            }

            renderersToToggle = root.GetComponentsInChildren<Renderer>(true);

            if (target == null)
            {
                target = GetComponentInParent<Health>();
            }

            if (fill != null)
            {
                fillFullScale = fill.localScale;
                fillFullLocalPosition = fill.localPosition;
                fillFullLocalWidth = CalculateFillLocalWidth();
            }
        }

        private void OnEnable()
        {
            if (target != null)
            {
                target.HealthChanged += HandleHealthChanged;
                HandleHealthChanged(target.CurrentHealth, target.MaxHealth);
            }
        }

        private void OnDisable()
        {
            if (target != null)
            {
                target.HealthChanged -= HandleHealthChanged;
            }
        }

        public void SetTarget(Health newTarget)
        {
            if (target != null)
            {
                target.HealthChanged -= HandleHealthChanged;
            }

            target = newTarget;

            if (isActiveAndEnabled && target != null)
            {
                target.HealthChanged += HandleHealthChanged;
                HandleHealthChanged(target.CurrentHealth, target.MaxHealth);
            }
        }

        private void HandleHealthChanged(float current, float max)
        {
            float ratio = max <= 0f ? 0f : Mathf.Clamp01(current / max);

            if (fill != null)
            {
                fill.localScale = new Vector3(
                    fillFullScale.x * ratio,
                    fillFullScale.y,
                    fillFullScale.z);

                fill.localPosition = GetFillLocalPosition(ratio);
            }

            SetVisible(!hideWhenFull || ratio > 0f && ratio < 1f);
        }

        private Vector3 GetFillLocalPosition(float ratio)
        {
            if (fillOrigin == FillOrigin.Center)
            {
                return fillFullLocalPosition;
            }

            float halfLostWidth = fillFullLocalWidth * (1f - ratio) * 0.5f;
            float direction = fillOrigin == FillOrigin.Left ? -1f : 1f;
            return fillFullLocalPosition + Vector3.right * halfLostWidth * direction;
        }

        private float CalculateFillLocalWidth()
        {
            if (fill == null)
            {
                return 1f;
            }

            if (fill.TryGetComponent(out SpriteRenderer spriteRenderer) && spriteRenderer.sprite != null)
            {
                return Mathf.Abs(spriteRenderer.sprite.bounds.size.x * fillFullScale.x);
            }

            if (fill is RectTransform rectTransform)
            {
                return Mathf.Abs(rectTransform.rect.width * fillFullScale.x);
            }

            return Mathf.Abs(fillFullScale.x);
        }

        private void SetVisible(bool isVisible)
        {
            if (root != null && root != gameObject)
            {
                root.SetActive(isVisible);
                return;
            }

            for (int i = 0; i < renderersToToggle.Length; i++)
            {
                if (renderersToToggle[i] != null)
                {
                    renderersToToggle[i].enabled = isVisible;
                }
            }
        }
    }
}
