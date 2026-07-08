using TMPro;
using UnityEngine;

namespace NeonBreaker.Combat
{
    public sealed class DamageNumberPopup2D : MonoBehaviour
    {
        [SerializeField] private TextMeshPro text;
        [SerializeField] private float lifetime = 0.82f;
        [SerializeField] private float riseDistance = 1.05f;
        [SerializeField] private float horizontalDrift = 0.28f;
        [SerializeField] private AnimationCurve alphaCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 1.22f, 1f, 0.9f);

        private Vector3 startPosition;
        private Vector3 drift;
        private Color baseColor = Color.white;
        private float timer;
        private float scaleMultiplier = 1f;

        private void Awake()
        {
            if (text == null)
            {
                text = GetComponentInChildren<TextMeshPro>(true);
            }
        }

        private void OnEnable()
        {
            timer = 0f;
            startPosition = transform.position;
        }

        private void Update()
        {
            float safeLifetime = Mathf.Max(0.01f, lifetime);
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / safeLifetime);

            transform.position = startPosition + Vector3.up * (riseDistance * t) + drift * t;

            float scale = scaleCurve != null ? scaleCurve.Evaluate(t) : 1f;
            transform.localScale = Vector3.one * Mathf.Max(0.01f, scaleMultiplier) * scale;

            if (text != null)
            {
                Color color = baseColor;
                color.a *= alphaCurve != null ? alphaCurve.Evaluate(t) : 1f - t;
                text.color = color;
            }

            if (timer >= safeLifetime)
            {
                Destroy(gameObject);
            }
        }

        public void Play(
            string value,
            Color color,
            float scaleMultiplier,
            string sortingLayerName,
            int sortingOrder,
            FontStyles fontStyle)
        {
            Play(
                value,
                color,
                scaleMultiplier,
                sortingLayerName,
                sortingOrder,
                fontStyle,
                text != null ? text.fontSize : 4.2f,
                lifetime,
                riseDistance,
                horizontalDrift,
                false,
                0f,
                Color.black,
                alphaCurve,
                scaleCurve);
        }

        public void Play(
            string value,
            Color color,
            float popupScaleMultiplier,
            string sortingLayerName,
            int sortingOrder,
            FontStyles fontStyle,
            float fontSize,
            float popupLifetime,
            float popupRiseDistance,
            float popupHorizontalDrift,
            bool useOutline,
            float outlineWidth,
            Color outlineColor,
            AnimationCurve popupAlphaCurve,
            AnimationCurve popupScaleCurve)
        {
            if (text == null)
            {
                text = GetComponentInChildren<TextMeshPro>(true);
            }

            baseColor = color;
            scaleMultiplier = Mathf.Max(0.01f, popupScaleMultiplier);
            lifetime = Mathf.Max(0.05f, popupLifetime);
            riseDistance = Mathf.Max(0f, popupRiseDistance);
            horizontalDrift = Mathf.Max(0f, popupHorizontalDrift);
            alphaCurve = popupAlphaCurve != null ? popupAlphaCurve : alphaCurve;
            scaleCurve = popupScaleCurve != null ? popupScaleCurve : scaleCurve;
            drift = Vector3.right * Random.Range(-horizontalDrift, horizontalDrift);
            timer = 0f;
            startPosition = transform.position;
            transform.localScale = Vector3.one * scaleMultiplier;

            if (text == null)
            {
                return;
            }

            text.text = value;
            text.color = baseColor;
            text.fontStyle = fontStyle;
            text.fontSize = Mathf.Max(0.1f, fontSize);
            text.outlineWidth = useOutline ? Mathf.Clamp01(outlineWidth) : 0f;
            text.outlineColor = outlineColor;
            text.sortingLayerID = SortingLayer.NameToID(sortingLayerName);
            text.sortingOrder = sortingOrder;
        }
    }
}
