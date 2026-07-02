using System.Collections;
using NeonBreaker.Pooling;
using UnityEngine;

namespace NeonBreaker.Combat
{
    [RequireComponent(typeof(PoolableGameObject))]
    public sealed class PooledVfx2D : MonoBehaviour, IPoolLifecycle
    {
        [SerializeField, Min(0.01f)] private float fallbackLifetime = 0.35f;
        [SerializeField] private bool useFallbackTimer = true;
        [SerializeField] private bool returnAfterAnimatorClipLength = true;
        [SerializeField] private bool returnBeforeAnimatorLoops = true;
        [SerializeField, Min(0f)] private float animationReturnLeadTime = 0.02f;
        [SerializeField, Min(0f)] private float animationReturnPadding;
        [SerializeField, Range(0.8f, 1f)] private float animatorHideNormalizedTime = 0.98f;
        [SerializeField, Min(0.01f)] private float maxAnimatorLifetime = 10f;
        [SerializeField] private bool hideRenderersBeforeReturn = true;
        [SerializeField, Min(0f)] private float rendererHideLeadTime = 0.06f;
        [SerializeField] private bool restartAnimatorOnSpawn = true;
        [SerializeField] private bool restartParticlesOnSpawn = true;
        [SerializeField] private bool autoPlayWhenUnpooled = true;
        [SerializeField] private bool lockWorldTransformDuringLifetime = true;
        [SerializeField] private bool multiplyLockedScaleByPrefabScale = true;
        [SerializeField] private Animator animator;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Vector3 visualRootBaseScale = Vector3.one;
        [SerializeField] private bool overrideVisualRootBaseScale;
        [SerializeField] private bool useSpriteRendererFlipForVisualFlip = true;

        private PoolableGameObject poolableObject;
        private ParticleSystem[] particleSystems;
        private Renderer[] renderers;
        private SpriteRenderer[] spriteRenderers;
        private bool[] baseSpriteFlipX;
        private bool[] baseSpriteFlipY;
        private Coroutine returnRoutine;
        private Vector3 prefabRootScale = Vector3.one;
        private bool hasLockedTransform;
        private Vector3 lockedPosition;
        private Quaternion lockedRotation;
        private Vector3 lockedScale;
        private bool hasLockedVisualFlip;
        private bool lockedVisualFlipX;
        private bool lockedVisualFlipY;
        private Vector3 lockedVisualScaleMultiplier = Vector3.one;

        private void Start()
        {
            if (!autoPlayWhenUnpooled || !IsUnpooledInstance() || returnRoutine != null)
            {
                return;
            }

            PlayDefault();
        }

        private void Awake()
        {
            poolableObject = GetComponent<PoolableGameObject>();
            prefabRootScale = transform.localScale;

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }

            if (visualRoot == null && animator != null && animator.transform != transform)
            {
                visualRoot = animator.transform;
            }

            if (visualRoot == null)
            {
                Renderer childRenderer = GetComponentInChildren<Renderer>(true);
                if (childRenderer != null && childRenderer.transform != transform)
                {
                    visualRoot = childRenderer.transform;
                }
            }

            if (visualRoot != null)
            {
                if (!overrideVisualRootBaseScale)
                {
                    visualRootBaseScale = visualRoot.localScale;
                }
            }

            particleSystems = GetComponentsInChildren<ParticleSystem>(true);
            renderers = GetComponentsInChildren<Renderer>(true);
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            CacheSpriteRendererFlipState();
        }

        public void OnSpawned()
        {
            PlayDefault();
        }

        public void PlayDefault()
        {
            SetRenderersVisible(true);
            RestartVisuals();
            ApplyVisualFlip(lockedVisualFlipX, lockedVisualFlipY);
            StartReturnTimer(GetReturnDelay());
        }

        public void OnDespawned()
        {
            StopFallbackTimer();
            lockedVisualScaleMultiplier = Vector3.one;
            ApplyVisualFlip(false, false);

            hasLockedTransform = false;
            hasLockedVisualFlip = false;
            lockedVisualFlipX = false;
            lockedVisualFlipY = false;
        }

        public void Play(float duration)
        {
            StartReturnTimer(duration);
        }

        public void ReturnToPool()
        {
            StopFallbackTimer();
            hasLockedTransform = false;
            hasLockedVisualFlip = false;
            lockedVisualScaleMultiplier = Vector3.one;

            if (hideRenderersBeforeReturn)
            {
                SetRenderersVisible(false);
            }

            if (poolableObject != null && poolableObject.HasOwnerPool)
            {
                poolableObject.ReturnToPool();
                return;
            }

            gameObject.SetActive(false);
        }

        public void LockWorldTransform(Vector3 position, Quaternion rotation)
        {
            LockWorldTransform(position, rotation, Vector3.one);
        }

        public void LockWorldTransform(Vector3 position, Quaternion rotation, Vector3 scaleMultiplier)
        {
            Vector3 localScale = GetLockedLocalScale(scaleMultiplier);

            transform.SetPositionAndRotation(position, rotation);
            transform.localScale = localScale;

            if (!lockWorldTransformDuringLifetime)
            {
                return;
            }

            lockedPosition = position;
            lockedRotation = rotation;
            lockedScale = localScale;
            hasLockedTransform = true;
        }

        public void SetVisualVerticalFlip(bool flipY)
        {
            SetVisualFlip(lockedVisualFlipX, flipY);
        }

        public void SetVisualHorizontalFlip(bool flipX)
        {
            SetVisualFlip(flipX, lockedVisualFlipY);
        }

        public void SetVisualFlip(bool flipX, bool flipY)
        {
            if (visualRoot == null && (spriteRenderers == null || spriteRenderers.Length == 0))
            {
                return;
            }

            ApplyVisualFlip(flipX, flipY);
            lockedVisualFlipX = flipX;
            lockedVisualFlipY = flipY;
            hasLockedVisualFlip = true;
        }

        public bool SetVisualScaleMultiplier(Vector3 scaleMultiplier)
        {
            if (visualRoot == null)
            {
                return false;
            }

            lockedVisualScaleMultiplier = SanitizeScaleMultiplier(scaleMultiplier);
            ApplyVisualFlip(lockedVisualFlipX, lockedVisualFlipY);
            return true;
        }

        private void LateUpdate()
        {
            if (!hasLockedTransform)
            {
                if (hasLockedVisualFlip)
                {
                    ApplyVisualFlip(lockedVisualFlipX, lockedVisualFlipY);
                }

                return;
            }

            transform.SetPositionAndRotation(lockedPosition, lockedRotation);
            transform.localScale = lockedScale;

            if (hasLockedVisualFlip)
            {
                ApplyVisualFlip(lockedVisualFlipX, lockedVisualFlipY);
            }
        }

        private void RestartVisuals()
        {
            if (restartAnimatorOnSpawn && animator != null)
            {
                animator.Rebind();
                animator.Update(0f);
            }

            if (!restartParticlesOnSpawn || particleSystems == null)
            {
                return;
            }

            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                {
                    continue;
                }

                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particleSystem.Play(true);
            }
        }

        private float GetReturnDelay()
        {
            if (!returnAfterAnimatorClipLength || animator == null || animator.runtimeAnimatorController == null)
            {
                return fallbackLifetime;
            }

            float duration = GetCurrentAnimatorClipDuration();
            if (duration <= 0.01f)
            {
                return fallbackLifetime;
            }

            if (returnBeforeAnimatorLoops)
            {
                return Mathf.Max(0.01f, duration - animationReturnLeadTime);
            }

            return duration + animationReturnPadding;
        }

        private float GetCurrentAnimatorClipDuration()
        {
            AnimatorClipInfo[] clipInfos = animator.GetCurrentAnimatorClipInfo(0);
            if (clipInfos == null || clipInfos.Length == 0)
            {
                return 0f;
            }

            float duration = 0f;
            for (int i = 0; i < clipInfos.Length; i++)
            {
                AnimationClip clip = clipInfos[i].clip;
                if (clip != null)
                {
                    duration = Mathf.Max(duration, clip.length);
                }
            }

            float speed = Mathf.Abs(animator.speed);
            return speed > 0.0001f ? duration / speed : duration;
        }

        private void StartReturnTimer(float duration)
        {
            StopFallbackTimer();

            if (!useFallbackTimer)
            {
                return;
            }

            if (returnAfterAnimatorClipLength && animator != null && animator.runtimeAnimatorController != null)
            {
                returnRoutine = StartCoroutine(AnimatorReturnRoutine(Mathf.Max(0.01f, duration)));
                return;
            }

            returnRoutine = StartCoroutine(TimerReturnRoutine(Mathf.Max(0.01f, duration)));
        }

        private void StopFallbackTimer()
        {
            if (returnRoutine == null)
            {
                return;
            }

            StopCoroutine(returnRoutine);
            returnRoutine = null;
        }

        private bool IsUnpooledInstance()
        {
            return poolableObject == null || !poolableObject.HasOwnerPool;
        }

        private Vector3 GetLockedLocalScale(Vector3 scaleMultiplier)
        {
            return multiplyLockedScaleByPrefabScale
                ? Vector3.Scale(prefabRootScale, scaleMultiplier)
                : scaleMultiplier;
        }

        private static Vector3 SanitizeScaleMultiplier(Vector3 scaleMultiplier)
        {
            if (Mathf.Approximately(scaleMultiplier.x, 0f))
            {
                scaleMultiplier.x = 1f;
            }

            if (Mathf.Approximately(scaleMultiplier.y, 0f))
            {
                scaleMultiplier.y = 1f;
            }

            if (Mathf.Approximately(scaleMultiplier.z, 0f))
            {
                scaleMultiplier.z = 1f;
            }

            return scaleMultiplier;
        }

        private IEnumerator TimerReturnRoutine(float duration)
        {
            if (hideRenderersBeforeReturn && rendererHideLeadTime > 0f)
            {
                float visibleDuration = Mathf.Max(0f, duration - rendererHideLeadTime);
                if (visibleDuration > 0f)
                {
                    yield return new WaitForSeconds(visibleDuration);
                }

                SetRenderersVisible(false);

                float hiddenDuration = duration - visibleDuration;
                if (hiddenDuration > 0f)
                {
                    yield return new WaitForSeconds(hiddenDuration);
                }
            }
            else
            {
                yield return new WaitForSeconds(duration);
            }

            returnRoutine = null;
            ReturnToPool();
        }

        private IEnumerator AnimatorReturnRoutine(float fallbackDuration)
        {
            yield return null;
            if (animator != null)
            {
                animator.Update(0f);
            }

            int initialStateHash = 0;
            bool hasInitialState = false;
            if (animator != null && animator.isActiveAndEnabled)
            {
                AnimatorStateInfo initialState = animator.GetCurrentAnimatorStateInfo(0);
                initialStateHash = initialState.fullPathHash;
                hasInitialState = initialStateHash != 0;
            }

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

                    if (stateInfo.loop && timer >= fallbackDuration)
                    {
                        break;
                    }
                }

                timer += Time.deltaTime;
                yield return null;
            }

            if (hideRenderersBeforeReturn)
            {
                SetRenderersVisible(false);
            }

            if (animationReturnPadding > 0f)
            {
                yield return new WaitForSeconds(animationReturnPadding);
            }

            returnRoutine = null;
            ReturnToPool();
        }

        private void SetRenderersVisible(bool visible)
        {
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

        private void ApplyVisualFlip(bool flipX, bool flipY)
        {
            bool usedSpriteRendererFlip = useSpriteRendererFlipForVisualFlip
                && spriteRenderers != null
                && spriteRenderers.Length > 0;

            if (usedSpriteRendererFlip)
            {
                ApplySpriteRendererFlip(flipX, flipY);
            }

            ApplyVisualRootScale(
                usedSpriteRendererFlip ? false : flipX,
                usedSpriteRendererFlip ? false : flipY);
        }

        private void ApplyVisualRootScale(bool flipX, bool flipY)
        {
            if (visualRoot == null)
            {
                return;
            }

            Vector3 scale = Vector3.Scale(visualRootBaseScale, lockedVisualScaleMultiplier);
            scale.x *= flipX ? -1f : 1f;
            scale.y *= flipY ? -1f : 1f;
            visualRoot.localScale = scale;
        }

        private void CacheSpriteRendererFlipState()
        {
            if (spriteRenderers == null)
            {
                baseSpriteFlipX = null;
                baseSpriteFlipY = null;
                return;
            }

            baseSpriteFlipX = new bool[spriteRenderers.Length];
            baseSpriteFlipY = new bool[spriteRenderers.Length];
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                SpriteRenderer spriteRenderer = spriteRenderers[i];
                baseSpriteFlipX[i] = spriteRenderer != null && spriteRenderer.flipX;
                baseSpriteFlipY[i] = spriteRenderer != null && spriteRenderer.flipY;
            }
        }

        private void ApplySpriteRendererFlip(bool flipX, bool flipY)
        {
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                SpriteRenderer spriteRenderer = spriteRenderers[i];
                if (spriteRenderer == null)
                {
                    continue;
                }

                bool baseFlipX = baseSpriteFlipX != null && i < baseSpriteFlipX.Length && baseSpriteFlipX[i];
                bool baseFlipY = baseSpriteFlipY != null && i < baseSpriteFlipY.Length && baseSpriteFlipY[i];
                spriteRenderer.flipX = baseFlipX != flipX;
                spriteRenderer.flipY = baseFlipY != flipY;
            }
        }
    }
}
