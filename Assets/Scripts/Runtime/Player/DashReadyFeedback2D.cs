using System.Collections;
using UnityEngine;

namespace NeonBreaker.Player
{
    [RequireComponent(typeof(PlayerDash2D))]
    public sealed class DashReadyFeedback2D : MonoBehaviour
    {
        [Header("Bindings")]
        [SerializeField] private PlayerDash2D dash;
        [SerializeField] private SpriteRenderer characterRenderer;
        [SerializeField] private SpriteRenderer ringRenderer;

        [Header("Visual")]
        [SerializeField] private Color flashColor = new Color(0.45f, 1f, 0.95f, 1f);
        [SerializeField] private float flashDuration = 0.28f;
        [SerializeField] private float characterGlowAlpha = 0.65f;
        [SerializeField] private float ringStartScale = 0.85f;
        [SerializeField] private float ringEndScale = 1.45f;
        [SerializeField] private bool createRingIfMissing = true;

        private MaterialPropertyBlock propertyBlock;
        private Color originalRingColor;
        private bool wasCoolingDown;
        private Coroutine feedbackRoutine;

        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void Awake()
        {
            if (dash == null)
            {
                dash = GetComponent<PlayerDash2D>();
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
                originalRingColor = ringRenderer.color;
                ringRenderer.enabled = false;
            }

            propertyBlock = new MaterialPropertyBlock();
        }

        private void Update()
        {
            if (dash == null)
            {
                return;
            }

            bool isCoolingDown = dash.CooldownRemaining > 0f || dash.IsDashing;
            if (wasCoolingDown && dash.IsReady)
            {
                PlayFeedback();
            }

            wasCoolingDown = isCoolingDown;
        }

        public void PlayFeedback()
        {
            if (feedbackRoutine != null)
            {
                StopCoroutine(feedbackRoutine);
            }

            feedbackRoutine = StartCoroutine(FeedbackRoutine());
        }

        private IEnumerator FeedbackRoutine()
        {
            float timer = 0f;

            if (ringRenderer != null)
            {
                ringRenderer.enabled = true;
                ringRenderer.transform.localScale = Vector3.one * ringStartScale;
            }

            while (timer < flashDuration)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / flashDuration);
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
                ringRenderer.color = originalRingColor;
            }

            feedbackRoutine = null;
        }

        private void ApplyCharacterFlash(float alpha)
        {
            if (characterRenderer == null)
            {
                return;
            }

            characterRenderer.GetPropertyBlock(propertyBlock);
            Color color = new Color(flashColor.r, flashColor.g, flashColor.b, Mathf.Clamp01(alpha * characterGlowAlpha));
            propertyBlock.SetColor(ColorId, Color.Lerp(Color.white, color, color.a));
            characterRenderer.SetPropertyBlock(propertyBlock);
        }

        private void ClearCharacterFlash()
        {
            if (characterRenderer == null)
            {
                return;
            }

            characterRenderer.SetPropertyBlock(null);
        }

        private void ApplyRingFlash(float scaleT, float alpha)
        {
            if (ringRenderer == null)
            {
                return;
            }

            float scale = Mathf.Lerp(ringStartScale, ringEndScale, scaleT);
            ringRenderer.transform.localScale = Vector3.one * scale;
            ringRenderer.color = new Color(flashColor.r, flashColor.g, flashColor.b, alpha);
        }

        private SpriteRenderer CreateFallbackRing()
        {
            GameObject ringObject = new GameObject("Dash Ready Ring");
            ringObject.transform.SetParent(transform, false);
            ringObject.transform.localPosition = Vector3.zero;

            SpriteRenderer renderer = ringObject.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateRingSprite();
            renderer.sortingOrder = characterRenderer != null ? characterRenderer.sortingOrder + 1 : 10;
            renderer.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
            return renderer;
        }

        private static Sprite CreateRingSprite()
        {
            const int size = 96;
            const float outerRadius = 42f;
            const float innerRadius = 34f;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "Dash Ready Ring Texture";
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
