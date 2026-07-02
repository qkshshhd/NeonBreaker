using System.Collections;
using UnityEngine;

namespace NeonBreaker.Rooms
{
    public sealed class RestRoomFeedback2D : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private RoomRunManager runManager;
        [SerializeField] private Transform player;

        [Header("Visual")]
        [SerializeField] private Color ringColor = new Color(0.28f, 1f, 0.72f, 0.72f);
        [SerializeField] private Color glowColor = new Color(0.45f, 1f, 0.85f, 0.26f);
        [SerializeField, Min(0.05f)] private float duration = 0.85f;
        [SerializeField, Min(0.01f)] private float startScale = 0.35f;
        [SerializeField, Min(0.01f)] private float endScale = 3.25f;
        [SerializeField] private int sortingOrder = 25;
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.05f, 0f);
        [SerializeField] private bool playOnlyWhenRoomGrantsHeal = true;

        private Sprite ringSprite;
        private Sprite glowSprite;
        private Coroutine routine;

        private void Awake()
        {
            if (runManager == null)
            {
                runManager = FindAnyObjectByType<RoomRunManager>();
            }

            if (player == null)
            {
                GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
                if (playerObject != null)
                {
                    player = playerObject.transform;
                }
            }
        }

        private void OnEnable()
        {
            if (runManager != null)
            {
                runManager.RunRoomStarted += HandleRunRoomStarted;
            }
        }

        private void OnDisable()
        {
            if (runManager != null)
            {
                runManager.RunRoomStarted -= HandleRunRoomStarted;
            }

            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }
        }

        private void HandleRunRoomStarted(int roomIndex, RoomDefinition room)
        {
            if (room == null || room.RoomType != RoomType.Rest)
            {
                return;
            }

            if (playOnlyWhenRoomGrantsHeal && !room.GrantsHealReward)
            {
                return;
            }

            Play();
        }

        public void Play()
        {
            if (player == null)
            {
                return;
            }

            if (routine != null)
            {
                StopCoroutine(routine);
            }

            routine = StartCoroutine(PlayRoutine());
        }

        private IEnumerator PlayRoutine()
        {
            Vector3 position = player.position + worldOffset;
            GameObject root = new GameObject("Rest Room Feedback");
            root.transform.position = position;

            SpriteRenderer glow = CreateRenderer(root.transform, "Rest Glow", GetGlowSprite(), glowColor, sortingOrder - 1);
            SpriteRenderer ring = CreateRenderer(root.transform, "Rest Ring", GetRingSprite(), ringColor, sortingOrder);

            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / duration);
                float easeOut = 1f - (1f - t) * (1f - t);
                float ringScale = Mathf.Lerp(startScale, endScale, easeOut);
                float glowScale = Mathf.Lerp(startScale * 1.4f, endScale * 1.2f, easeOut);
                float alpha = 1f - easeOut;

                root.transform.position = player != null ? player.position + worldOffset : position;
                ring.transform.localScale = Vector3.one * ringScale;
                glow.transform.localScale = Vector3.one * glowScale;

                Color currentRingColor = ringColor;
                currentRingColor.a *= alpha;
                ring.color = currentRingColor;

                Color currentGlowColor = glowColor;
                currentGlowColor.a *= alpha;
                glow.color = currentGlowColor;

                yield return null;
            }

            Destroy(root);
            routine = null;
        }

        private static SpriteRenderer CreateRenderer(Transform parent, string objectName, Sprite sprite, Color color, int order)
        {
            GameObject target = new GameObject(objectName);
            target.transform.SetParent(parent, false);

            SpriteRenderer renderer = target.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        private Sprite GetRingSprite()
        {
            if (ringSprite == null)
            {
                ringSprite = CreateRingSprite(128, 54f, 43f, "Rest Room Ring Sprite");
            }

            return ringSprite;
        }

        private Sprite GetGlowSprite()
        {
            if (glowSprite == null)
            {
                glowSprite = CreateDiscSprite(128, 54f, "Rest Room Glow Sprite");
            }

            return glowSprite;
        }

        private static Sprite CreateRingSprite(int size, float outerRadius, float innerRadius, string spriteName)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = spriteName;
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

        private static Sprite CreateDiscSprite(int size, float radius, string spriteName)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = spriteName;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Clamp01(Mathf.InverseLerp(radius, radius * 0.25f, distance));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * 0.85f));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 64f);
        }
    }
}
