using System.Collections;
using NeonBreaker.Pooling;
using UnityEngine;

namespace NeonBreaker.Combat
{
    public sealed class TransientVfx2D : MonoBehaviour
    {
        private enum CompleteAction
        {
            ReturnToPool,
            Disable
        }

        [SerializeField, Min(0.01f)] private float lifetime = 1.2f;
        [SerializeField] private bool useUnscaledTime;
        [SerializeField] private CompleteAction completeAction = CompleteAction.ReturnToPool;
        [SerializeField] private bool destroyAfterAnimatorCompletes = true;
        [SerializeField, Min(0f)] private float animatorCompletionPadding;
        [SerializeField, Min(0.01f)] private float animatorFallbackLifetime = 1.2f;
        [SerializeField, Min(0.01f)] private float maxAnimatorLifetime = 10f;
        [SerializeField, Range(0.8f, 1f)] private float animatorHideNormalizedTime = 0.98f;
        [SerializeField] private bool hideRenderersBeforeDestroy = true;
        [SerializeField] private bool restartAnimatorOnPlay = true;
        [SerializeField] private Animator animator;

        private Coroutine routine;
        private Renderer[] renderers;
        private PoolableGameObject poolableObject;

        public void Play(float duration)
        {
            lifetime = Mathf.Max(0.01f, duration);

            if (routine != null)
            {
                StopCoroutine(routine);
            }

            routine = StartCoroutine(LifetimeRoutine());
        }

        public void PlayFromAnimator(float fallbackDuration)
        {
            animatorFallbackLifetime = Mathf.Max(0.01f, fallbackDuration);

            if (routine != null)
            {
                StopCoroutine(routine);
            }

            routine = StartCoroutine(AnimatorLifetimeRoutine());
        }

        private void OnEnable()
        {
            if (routine == null)
            {
                routine = destroyAfterAnimatorCompletes
                    ? StartCoroutine(AnimatorLifetimeRoutine())
                    : StartCoroutine(LifetimeRoutine());
            }
        }

        private void OnDisable()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }
        }

        private IEnumerator LifetimeRoutine()
        {
            float timer = 0f;
            while (timer < lifetime)
            {
                timer += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                yield return null;
            }

            Complete();
        }

        private IEnumerator AnimatorLifetimeRoutine()
        {
            ResolveAnimator();
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                yield return LifetimeRoutine();
                yield break;
            }

            ResolveRenderers();
            SetRenderersVisible(true);

            if (restartAnimatorOnPlay)
            {
                animator.Rebind();
            }

            yield return null;
            animator.Update(0f);

            AnimatorStateInfo initialState = animator.GetCurrentAnimatorStateInfo(0);
            int initialStateHash = initialState.fullPathHash;
            bool hasInitialState = initialStateHash != 0;

            float timer = 0f;
            while (animator != null && animator.isActiveAndEnabled && timer < maxAnimatorLifetime)
            {
                if (!animator.IsInTransition(0))
                {
                    AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                    if (hasInitialState && stateInfo.fullPathHash != initialStateHash)
                    {
                        break;
                    }

                    if (!stateInfo.loop && stateInfo.normalizedTime >= animatorHideNormalizedTime)
                    {
                        break;
                    }

                    if (stateInfo.loop && timer >= animatorFallbackLifetime)
                    {
                        break;
                    }
                }

                timer += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                yield return null;
            }

            HideRenderersIfNeeded();

            if (animatorCompletionPadding > 0f)
            {
                float paddingTimer = 0f;
                while (paddingTimer < animatorCompletionPadding)
                {
                    paddingTimer += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                    yield return null;
                }
            }

            Complete();
        }

        private void ResolveAnimator()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }
        }

        private void ResolvePoolable()
        {
            if (poolableObject == null)
            {
                poolableObject = GetComponentInParent<PoolableGameObject>();
            }
        }

        private void Complete()
        {
            HideRenderersIfNeeded();

            if (completeAction == CompleteAction.ReturnToPool)
            {
                ResolvePoolable();
                if (poolableObject != null)
                {
                    poolableObject.ReturnToPool();
                    return;
                }
            }

            gameObject.SetActive(false);
        }

        private void ResolveRenderers()
        {
            if (renderers == null || renderers.Length == 0)
            {
                renderers = GetComponentsInChildren<Renderer>(true);
            }
        }

        private void HideRenderersIfNeeded()
        {
            if (hideRenderersBeforeDestroy)
            {
                SetRenderersVisible(false);
            }
        }

        private void SetRenderersVisible(bool visible)
        {
            ResolveRenderers();
            if (renderers == null)
            {
                return;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer targetRenderer = renderers[i];
                if (targetRenderer != null)
                {
                    targetRenderer.enabled = visible;
                }
            }
        }
    }
}
