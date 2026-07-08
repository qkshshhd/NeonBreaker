using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NeonBreaker.Environment
{
    public sealed class RoomAccentLightFlicker2D : MonoBehaviour
    {
        [SerializeField] private Light2D targetLight;
        [SerializeField, Min(0f)] private float baseIntensity = 0.6f;
        [SerializeField, Range(0f, 1f)] private float amount = 0.12f;
        [SerializeField, Min(0f)] private float speed = 1.6f;

        private float phase;

        private void Awake()
        {
            if (targetLight == null)
            {
                targetLight = GetComponent<Light2D>();
            }

            phase = Random.Range(0f, 10f);
        }

        private void Update()
        {
            if (targetLight == null || speed <= 0f || amount <= 0f)
            {
                return;
            }

            float wave = Mathf.Sin(Time.time * speed + phase) * 0.5f + 0.5f;
            float noise = Mathf.PerlinNoise(phase, Time.time * speed * 0.37f);
            float flicker = Mathf.Lerp(wave, noise, 0.45f);
            targetLight.intensity = baseIntensity * (1f - amount + amount * flicker * 2f);
        }

        public void Configure(Light2D light, float intensity, float flickerAmount, float flickerSpeed)
        {
            targetLight = light;
            baseIntensity = Mathf.Max(0f, intensity);
            amount = Mathf.Clamp01(flickerAmount);
            speed = Mathf.Max(0f, flickerSpeed);
            phase = Random.Range(0f, 10f);

            if (targetLight != null)
            {
                targetLight.intensity = baseIntensity;
            }
        }
    }
}
