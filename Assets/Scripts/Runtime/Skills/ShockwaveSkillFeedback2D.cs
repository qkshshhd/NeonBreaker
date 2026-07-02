using System.Collections;
using NeonBreaker.Player;
using UnityEngine;

namespace NeonBreaker.Skills
{
    [RequireComponent(typeof(PlayerSkillController))]
    public sealed class ShockwaveSkillFeedback2D : MonoBehaviour
    {
        [SerializeField] private PlayerSkillController skillController;
        [SerializeField] private PlayerStats stats;
        [SerializeField] private SpriteRenderer ringRenderer;
        [SerializeField] private Color shockwaveColor = new Color(0.85f, 0.25f, 1f, 1f);
        [SerializeField] private float duration = 0.28f;
        [SerializeField] private float startScale = 0.2f;
        [SerializeField] private int sortingOrderOffset = 2;
        [SerializeField] private bool createRingIfMissing = true;

        private Coroutine routine;

        private void Awake()
        {
            if (skillController == null)
            {
                skillController = GetComponent<PlayerSkillController>();
            }

            if (stats == null)
            {
                stats = GetComponent<PlayerStats>();
            }

            if (ringRenderer == null && createRingIfMissing)
            {
                ringRenderer = CreateFallbackRing();
            }

            if (ringRenderer != null)
            {
                ringRenderer.enabled = false;
            }
        }

        private void OnEnable()
        {
            if (skillController != null)
            {
                skillController.SkillPerformed += HandleSkillPerformed;
            }
        }

        private void OnDisable()
        {
            if (skillController != null)
            {
                skillController.SkillPerformed -= HandleSkillPerformed;
            }
        }

        private void HandleSkillPerformed(SkillDefinition skill)
        {
            if (skill == null || skill.SkillType != SkillType.Shockwave)
            {
                return;
            }

            float effectiveRadius = stats != null ? stats.GetSkillRadius(skill.Radius) : skill.Radius;
            Play(effectiveRadius);
        }

        public void Play(float radius)
        {
            if (ringRenderer == null)
            {
                return;
            }

            if (routine != null)
            {
                StopCoroutine(routine);
            }

            routine = StartCoroutine(PlayRoutine(Mathf.Max(0.05f, radius)));
        }

        private IEnumerator PlayRoutine(float radius)
        {
            ringRenderer.enabled = true;

            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / duration);
                float easeOut = 1f - (1f - t) * (1f - t);
                float alpha = 1f - easeOut;
                float scale = Mathf.Lerp(startScale, radius * 2f, easeOut);

                ringRenderer.transform.localScale = Vector3.one * scale;
                ringRenderer.color = new Color(shockwaveColor.r, shockwaveColor.g, shockwaveColor.b, alpha);

                yield return null;
            }

            ringRenderer.enabled = false;
            routine = null;
        }

        private SpriteRenderer CreateFallbackRing()
        {
            GameObject ringObject = new GameObject("Shockwave Ring");
            ringObject.transform.SetParent(transform, false);
            ringObject.transform.localPosition = Vector3.zero;

            SpriteRenderer renderer = ringObject.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateRingSprite();
            renderer.sortingOrder = GetBaseSortingOrder() + sortingOrderOffset;
            renderer.color = new Color(shockwaveColor.r, shockwaveColor.g, shockwaveColor.b, 0f);
            return renderer;
        }

        private int GetBaseSortingOrder()
        {
            SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            return spriteRenderer != null ? spriteRenderer.sortingOrder : 10;
        }

        private static Sprite CreateRingSprite()
        {
            const int size = 128;
            const float outerRadius = 56f;
            const float innerRadius = 46f;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "Shockwave Ring Texture";
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    float outer = Mathf.InverseLerp(outerRadius + 3f, outerRadius - 3f, distance);
                    float inner = Mathf.InverseLerp(innerRadius - 3f, innerRadius + 3f, distance);
                    float alpha = Mathf.Clamp01(Mathf.Min(outer, inner));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 64f);
        }
    }
}
