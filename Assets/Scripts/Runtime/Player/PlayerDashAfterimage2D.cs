using System.Collections;
using UnityEngine;

namespace NeonBreaker.Player
{
    [RequireComponent(typeof(PlayerDash2D))]
    public sealed class PlayerDashAfterimage2D : MonoBehaviour
    {
        [SerializeField] private PlayerDash2D dash;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private SpriteRenderer[] sourceRenderers;
        [SerializeField] private bool autoCollectRenderers = true;
        [SerializeField] private bool refreshRenderersOnDash = true;
        [SerializeField, Min(0.005f)] private float spawnInterval = 0.035f;
        [SerializeField, Min(0.01f)] private float lifetime = 0.18f;
        [SerializeField] private Color startColor = new Color(0.25f, 0.95f, 1f, 0.42f);
        [SerializeField] private Color endColor = new Color(0.7f, 0.15f, 1f, 0f);
        [SerializeField] private bool inheritSourceRgb;
        [SerializeField] private Material ghostMaterial;
        [SerializeField] private int sortingOrderOffset = -1;
        [SerializeField] private int minimumGhostSortingOrder = 1;
        [SerializeField] private Vector3 scaleMultiplier = Vector3.one;
        [SerializeField] private bool spawnOnDashStart = true;
        [SerializeField] private bool spawnOnDashEnd;

        private Coroutine spawnRoutine;

        private void Awake()
        {
            if (dash == null)
            {
                dash = GetComponent<PlayerDash2D>();
            }

            ResolveVisualRoot();

            if (autoCollectRenderers)
            {
                CollectSourceRenderers();
            }
        }

        private void OnEnable()
        {
            if (dash == null)
            {
                return;
            }

            dash.DashStarted += HandleDashStarted;
            dash.DashEnded += HandleDashEnded;
        }

        private void OnDisable()
        {
            if (dash != null)
            {
                dash.DashStarted -= HandleDashStarted;
                dash.DashEnded -= HandleDashEnded;
            }

            StopSpawnRoutine();
        }

        private void HandleDashStarted()
        {
            StopSpawnRoutine();

            if (autoCollectRenderers && refreshRenderersOnDash)
            {
                CollectSourceRenderers();
            }

            if (spawnOnDashStart)
            {
                SpawnAfterimage();
            }

            spawnRoutine = StartCoroutine(SpawnRoutine());
        }

        private void HandleDashEnded()
        {
            StopSpawnRoutine();

            if (spawnOnDashEnd)
            {
                SpawnAfterimage();
            }
        }

        private IEnumerator SpawnRoutine()
        {
            WaitForSeconds wait = new WaitForSeconds(spawnInterval);

            while (dash != null && dash.IsDashing)
            {
                yield return wait;
                SpawnAfterimage();
            }

            spawnRoutine = null;
        }

        private void StopSpawnRoutine()
        {
            if (spawnRoutine == null)
            {
                return;
            }

            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }

        private void SpawnAfterimage()
        {
            if (sourceRenderers == null || sourceRenderers.Length == 0)
            {
                return;
            }

            GameObject root = new GameObject("Dash Afterimage");
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            SpriteRenderer[] ghosts = new SpriteRenderer[sourceRenderers.Length];
            int ghostCount = 0;

            for (int i = 0; i < sourceRenderers.Length; i++)
            {
                SpriteRenderer source = sourceRenderers[i];
                if (!CanSnapshot(source))
                {
                    continue;
                }

                SpriteRenderer ghost = CreateGhostRenderer(root.transform, source);
                ghosts[ghostCount] = ghost;
                ghostCount++;
            }

            if (ghostCount <= 0)
            {
                Destroy(root);
                return;
            }

            StartCoroutine(FadeAndDestroy(root, ghosts, ghostCount));
        }

        private SpriteRenderer CreateGhostRenderer(Transform root, SpriteRenderer source)
        {
            GameObject ghostObject = new GameObject($"{source.name} Afterimage");
            Transform ghostTransform = ghostObject.transform;
            ghostTransform.SetParent(root, false);
            ghostTransform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            ghostTransform.localScale = Vector3.Scale(source.transform.lossyScale, scaleMultiplier);

            SpriteRenderer ghost = ghostObject.AddComponent<SpriteRenderer>();
            ghost.sprite = source.sprite;
            ghost.flipX = source.flipX;
            ghost.flipY = source.flipY;
            ghost.drawMode = source.drawMode;
            ghost.size = source.size;
            ghost.maskInteraction = source.maskInteraction;
            ghost.sortingLayerID = source.sortingLayerID;
            ghost.sortingOrder = Mathf.Max(
                minimumGhostSortingOrder,
                source.sortingOrder + sortingOrderOffset);
            ghost.sharedMaterial = ghostMaterial != null ? ghostMaterial : source.sharedMaterial;
            ghost.color = GetGhostColor(source, startColor);
            return ghost;
        }

        private IEnumerator FadeAndDestroy(GameObject root, SpriteRenderer[] ghosts, int ghostCount)
        {
            float timer = 0f;
            Color[] startColors = new Color[ghostCount];

            for (int i = 0; i < ghostCount; i++)
            {
                startColors[i] = ghosts[i] != null ? ghosts[i].color : startColor;
            }

            while (timer < lifetime)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / lifetime);
                float easeOut = 1f - (1f - t) * (1f - t);

                for (int i = 0; i < ghostCount; i++)
                {
                    SpriteRenderer ghost = ghosts[i];
                    if (ghost == null)
                    {
                        continue;
                    }

                    ghost.color = Color.Lerp(startColors[i], endColor, easeOut);
                }

                yield return null;
            }

            Destroy(root);
        }

        private Color GetGhostColor(SpriteRenderer source, Color alphaSource)
        {
            if (!inheritSourceRgb)
            {
                return alphaSource;
            }

            Color color = source.color;
            color.a = alphaSource.a;
            return color;
        }

        public void CollectSourceRenderers()
        {
            ResolveVisualRoot();
            Transform searchRoot = visualRoot != null ? visualRoot : transform;
            sourceRenderers = searchRoot.GetComponentsInChildren<SpriteRenderer>(true);
        }

        private void ResolveVisualRoot()
        {
            if (visualRoot != null)
            {
                return;
            }

            Animator childAnimator = GetComponentInChildren<Animator>(true);
            if (childAnimator != null)
            {
                visualRoot = childAnimator.transform;
                return;
            }

            Transform namedVisualRoot = transform.Find("VisualRoot");
            visualRoot = namedVisualRoot != null ? namedVisualRoot : transform;
        }

        private static bool CanSnapshot(SpriteRenderer source)
        {
            return source != null
                && source.enabled
                && source.gameObject.activeInHierarchy
                && source.sprite != null;
        }
    }
}
