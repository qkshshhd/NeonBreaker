using UnityEngine;
using UnityEngine.UI;

namespace NeonBreaker.UI
{
    [DisallowMultipleComponent]
    public sealed class MainMenuParticleLayer : MonoBehaviour
    {
        [Header("Bindings")]
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private RectTransform particleRoot;
        [SerializeField] private bool createDedicatedCanvas = true;
        [SerializeField] private int sortingOrder = 760;
        [SerializeField] private Vector2 fallbackScreenSize = new Vector2(1920f, 1080f);

        [Header("Particles")]
        [SerializeField, Range(8, 160)] private int particleCount = 64;
        [SerializeField] private Vector2 sizeRange = new Vector2(2.5f, 6f);
        [SerializeField] private Vector2 speedRange = new Vector2(6f, 18f);
        [SerializeField] private Vector2 horizontalDriftRange = new Vector2(-8f, 8f);
        [SerializeField] private Color dustColor = new Color(0.38f, 0.9f, 1f, 0.46f);
        [SerializeField] private Color emberColor = new Color(1f, 0.12f, 0.44f, 0.36f);
        [SerializeField, Range(0f, 1f)] private float emberChance = 0.28f;

        [Header("Motion")]
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField, Min(0.01f)] private float pulseFrequency = 0.8f;
        [SerializeField, Range(0f, 1f)] private float pulseAmount = 0.22f;
        [SerializeField, HideInInspector] private int tuningVersion;

        private Particle[] particles;
        private Vector2 boundsSize = new Vector2(1920f, 1080f);
        private Vector2 previousBoundsSize;
        private static Sprite particleSprite;
        private const int CurrentTuningVersion = 1;

        private struct Particle
        {
            public RectTransform Rect;
            public UnityEngine.UI.Image Image;
            public Vector2 Position;
            public float Speed;
            public float Drift;
            public float Size;
            public float Phase;
            public Color BaseColor;
        }

        private void Awake()
        {
            UpgradeDefaults();
            EnsureCanvas();
            RebuildParticles();
        }

        private void OnEnable()
        {
            UpgradeDefaults();
            EnsureCanvas();
            if (particles == null || particles.Length != particleCount)
            {
                RebuildParticles();
            }
        }

        private void OnValidate()
        {
            UpgradeDefaults();
            particleCount = Mathf.Clamp(particleCount, 8, 160);
            sizeRange.x = Mathf.Max(0.5f, sizeRange.x);
            sizeRange.y = Mathf.Max(sizeRange.x, sizeRange.y);
            speedRange.x = Mathf.Max(0f, speedRange.x);
            speedRange.y = Mathf.Max(speedRange.x, speedRange.y);
        }

        private void Update()
        {
            if (particleRoot == null || particles == null)
            {
                return;
            }

            RefreshBounds();

            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float time = useUnscaledTime ? Time.unscaledTime : Time.time;
            for (int i = 0; i < particles.Length; i++)
            {
                Particle particle = particles[i];
                particle.Position.y += particle.Speed * deltaTime;
                particle.Position.x += particle.Drift * deltaTime;

                if (particle.Position.y > boundsSize.y * 0.5f + 30f
                    || particle.Position.x < -boundsSize.x * 0.5f - 30f
                    || particle.Position.x > boundsSize.x * 0.5f + 30f)
                {
                    ResetParticle(ref particle, true);
                }

                float pulse = 1f - pulseAmount + (Mathf.Sin(time * pulseFrequency + particle.Phase) * 0.5f + 0.5f) * pulseAmount;
                Color color = particle.BaseColor;
                color.a *= pulse;
                particle.Image.color = color;
                particle.Rect.anchoredPosition = particle.Position;
                particle.Rect.sizeDelta = Vector2.one * particle.Size;

                particles[i] = particle;
            }
        }

        private void EnsureCanvas()
        {
            if (createDedicatedCanvas && (targetCanvas == null || targetCanvas.GetComponent<MainMenuParticleCanvasMarker>() == null))
            {
                GameObject canvasObject = new GameObject("Main Menu Particle Canvas");
                targetCanvas = canvasObject.AddComponent<Canvas>();
                targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                targetCanvas.overrideSorting = true;
                targetCanvas.sortingOrder = sortingOrder;
                CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                canvasObject.AddComponent<MainMenuParticleCanvasMarker>();
            }

            if (!createDedicatedCanvas && targetCanvas == null)
            {
                targetCanvas = GetComponentInParent<Canvas>();
            }

            if (targetCanvas == null)
            {
                GameObject canvasObject = new GameObject("Main Menu Particle Canvas");
                targetCanvas = canvasObject.AddComponent<Canvas>();
                targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                targetCanvas.overrideSorting = true;
                targetCanvas.sortingOrder = sortingOrder;
                CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                canvasObject.AddComponent<MainMenuParticleCanvasMarker>();
            }

            targetCanvas.sortingOrder = sortingOrder;
            if (createDedicatedCanvas)
            {
                targetCanvas.overrideSorting = true;
            }

            if (particleRoot == null)
            {
                GameObject rootObject = new GameObject("Main Menu Particle Layer");
                rootObject.transform.SetParent(targetCanvas.transform, false);
                particleRoot = rootObject.AddComponent<RectTransform>();
                particleRoot.anchorMin = Vector2.zero;
                particleRoot.anchorMax = Vector2.one;
                particleRoot.offsetMin = Vector2.zero;
                particleRoot.offsetMax = Vector2.zero;
            }

            particleRoot.anchorMin = Vector2.zero;
            particleRoot.anchorMax = Vector2.one;
            particleRoot.offsetMin = Vector2.zero;
            particleRoot.offsetMax = Vector2.zero;
            particleRoot.SetAsLastSibling();
        }

        private void RebuildParticles()
        {
            ClearParticles();
            RefreshBounds();

            particles = new Particle[particleCount];
            for (int i = 0; i < particles.Length; i++)
            {
                GameObject particleObject = new GameObject($"Menu Particle {i:00}");
                particleObject.transform.SetParent(particleRoot, false);
                UnityEngine.UI.Image image = particleObject.AddComponent<UnityEngine.UI.Image>();
                image.sprite = GetParticleSprite();
                image.raycastTarget = false;

                Particle particle = new Particle
                {
                    Rect = image.rectTransform,
                    Image = image
                };

                ResetParticle(ref particle, false);
                particles[i] = particle;
            }
        }

        private void ClearParticles()
        {
            if (particleRoot == null)
            {
                return;
            }

            for (int i = particleRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = particleRoot.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private void ResetParticle(ref Particle particle, bool fromBottom)
        {
            particle.Position = new Vector2(
                Random.Range(-boundsSize.x * 0.5f, boundsSize.x * 0.5f),
                fromBottom ? Random.Range(-boundsSize.y * 0.5f - 40f, -boundsSize.y * 0.5f) : Random.Range(-boundsSize.y * 0.5f, boundsSize.y * 0.5f));
            particle.Speed = Random.Range(speedRange.x, speedRange.y);
            particle.Drift = Random.Range(horizontalDriftRange.x, horizontalDriftRange.y);
            particle.Size = Random.Range(sizeRange.x, sizeRange.y);
            particle.Phase = Random.Range(0f, Mathf.PI * 2f);
            particle.BaseColor = Random.value < emberChance ? emberColor : dustColor;
        }

        private void RefreshBounds()
        {
            Vector2 newBounds = Vector2.zero;

            if (particleRoot == null)
            {
                newBounds = GetScreenBounds();
            }
            else
            {
                Rect rect = particleRoot.rect;
                if (rect.width > 1f && rect.height > 1f)
                {
                    newBounds = rect.size;
                }
            }

            Vector2 screenBounds = GetScreenBounds();
            if (newBounds.x < screenBounds.x * 0.5f || newBounds.y < screenBounds.y * 0.5f)
            {
                newBounds = screenBounds;
            }

            if (newBounds.x <= 1f || newBounds.y <= 1f)
            {
                newBounds = fallbackScreenSize;
            }

            bool expandedFromTinyBounds = previousBoundsSize.x > 1f
                && previousBoundsSize.y > 1f
                && (newBounds.x > previousBoundsSize.x * 1.5f || newBounds.y > previousBoundsSize.y * 1.5f);

            boundsSize = newBounds;
            if (expandedFromTinyBounds && particles != null)
            {
                RedistributeParticles();
            }

            previousBoundsSize = boundsSize;
        }

        private Vector2 GetScreenBounds()
        {
            if (targetCanvas != null)
            {
                Rect pixelRect = targetCanvas.pixelRect;
                if (pixelRect.width > 1f && pixelRect.height > 1f)
                {
                    return pixelRect.size;
                }
            }

            if (Screen.width > 1 && Screen.height > 1)
            {
                return new Vector2(Screen.width, Screen.height);
            }

            return fallbackScreenSize;
        }

        private void RedistributeParticles()
        {
            for (int i = 0; i < particles.Length; i++)
            {
                Particle particle = particles[i];
                ResetParticle(ref particle, false);
                particles[i] = particle;
            }
        }

        private void UpgradeDefaults()
        {
            if (tuningVersion >= CurrentTuningVersion)
            {
                return;
            }

            if (particleCount == 42)
            {
                particleCount = 64;
            }

            if (fallbackScreenSize == Vector2.zero)
            {
                fallbackScreenSize = new Vector2(1920f, 1080f);
            }

            if (sizeRange == new Vector2(1.5f, 4f))
            {
                sizeRange = new Vector2(2.5f, 6f);
            }

            if (dustColor == new Color(0.38f, 0.82f, 1f, 0.24f))
            {
                dustColor = new Color(0.38f, 0.9f, 1f, 0.46f);
            }

            if (emberColor == new Color(1f, 0.12f, 0.44f, 0.2f))
            {
                emberColor = new Color(1f, 0.12f, 0.44f, 0.36f);
            }

            if (Mathf.Approximately(emberChance, 0.22f))
            {
                emberChance = 0.28f;
            }

            if (Mathf.Approximately(pulseAmount, 0.35f))
            {
                pulseAmount = 0.22f;
            }

            createDedicatedCanvas = true;
            sortingOrder = Mathf.Max(sortingOrder, 760);
            tuningVersion = CurrentTuningVersion;
        }

        private sealed class MainMenuParticleCanvasMarker : MonoBehaviour
        {
        }

        private static Sprite GetParticleSprite()
        {
            if (particleSprite != null)
            {
                return particleSprite;
            }

            const int size = 8;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center) / (size * 0.5f);
                    float alpha = Mathf.Clamp01(1f - distance);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha));
                }
            }

            texture.Apply();
            particleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            particleSprite.name = "Runtime Main Menu Particle";
            return particleSprite;
        }
    }
}
