using System.Collections;
using UnityEngine;

namespace NeonBreaker.Skills
{
    [RequireComponent(typeof(PlayerSkillController))]
    public sealed class SkillReadyFeedback2D : MonoBehaviour
    {
        [SerializeField] private PlayerSkillController skillController;
        [SerializeField] private SpriteRenderer characterRenderer;
        [SerializeField] private SpriteRenderer ringRenderer;
        [SerializeField] private Color readyColor = new Color(1f, 0.35f, 0.95f, 1f);
        [SerializeField] private float duration = 0.32f;
        [SerializeField] private float characterGlowAlpha = 0.55f;
        [SerializeField] private float ringStartScale = 0.75f;
        [SerializeField] private float ringEndScale = 1.35f;
        [SerializeField] private bool createRingIfMissing = true;

        private MaterialPropertyBlock propertyBlock;
        private bool wasCoolingDown;
        private Coroutine routine;

        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void Awake()
        {
            if (skillController == null)
            {
                skillController = GetComponent<PlayerSkillController>();
            }

            if (characterRenderer == null)
            {
                characterRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (ringRenderer == null && createRingIfMissing)
            {
                ringRenderer = CreateFallbackRing();
            }

            if (ringRenderer != null)
            {
                ringRenderer.enabled = false;
            }

            propertyBlock = new MaterialPropertyBlock();
        }

        private void Update()
        {
            if (skillController == null || !skillController.HasSkill)
            {
                return;
            }

            bool isCoolingDown = skillController.CooldownRemaining > 0f;
            if (wasCoolingDown && skillController.IsReady)
            {
                PlayFeedback();
            }

            wasCoolingDown = isCoolingDown;
        }

        public void PlayFeedback()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
            }

            routine = StartCoroutine(FeedbackRoutine());
        }

        private IEnumerator FeedbackRoutine()
        {
            float timer = 0f;

            if (ringRenderer != null)
            {
                ringRenderer.enabled = true;
            }

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / duration);
                float easeOut = 1f - (1f - t) * (1f - t);
                float alpha = 1f - easeOut;

                ApplyCharacterFlash(alpha);
                ApplyRingFlash(easeOut, alpha);

                yield return null;
            }

            ClearCharacterFlash();
            if (ringRenderer != null)
            {
                ringRenderer.enabled = false;
            }

            routine = null;
        }

        private void ApplyCharacterFlash(float alpha)
        {
            if (characterRenderer == null)
            {
                return;
            }

            characterRenderer.GetPropertyBlock(propertyBlock);
            Color color = new Color(readyColor.r, readyColor.g, readyColor.b, Mathf.Clamp01(alpha * characterGlowAlpha));
            propertyBlock.SetColor(ColorId, Color.Lerp(Color.white, color, color.a));
            characterRenderer.SetPropertyBlock(propertyBlock);
        }

        private void ClearCharacterFlash()
        {
            if (characterRenderer != null)
            {
                characterRenderer.SetPropertyBlock(null);
            }
        }

        private void ApplyRingFlash(float scaleT, float alpha)
        {
            if (ringRenderer == null)
            {
                return;
            }

            float scale = Mathf.Lerp(ringStartScale, ringEndScale, scaleT);
            ringRenderer.transform.localScale = Vector3.one * scale;
            ringRenderer.color = new Color(readyColor.r, readyColor.g, readyColor.b, alpha);
        }

        private SpriteRenderer CreateFallbackRing()
        {
            GameObject ringObject = new GameObject("Skill Ready Ring");
            ringObject.transform.SetParent(transform, false);
            ringObject.transform.localPosition = Vector3.zero;

            SpriteRenderer renderer = ringObject.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateRingSprite();
            renderer.sortingOrder = characterRenderer != null ? characterRenderer.sortingOrder + 2 : 12;
            renderer.color = new Color(readyColor.r, readyColor.g, readyColor.b, 0f);
            return renderer;
        }

        private static Sprite CreateRingSprite()
        {
            const int size = 96;
            const float outerRadius = 42f;
            const float innerRadius = 34f;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "Skill Ready Ring Texture";
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    float outer = Mathf.InverseLerp(outerRadius + 2f, outerRadius - 2f, distance);
                    float inner = Mathf.InverseLerp(innerRadius - 2f, innerRadius + 2f, distance);
                    float alpha = Mathf.Clamp01(Mathf.Min(outer, inner));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 64f);
        }
    }
}
