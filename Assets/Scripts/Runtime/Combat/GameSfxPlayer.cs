using System.Collections.Generic;
using UnityEngine;

namespace NeonBreaker.Combat
{
    public sealed class GameSfxPlayer : MonoBehaviour
    {
        private static GameSfxPlayer instance;

        [SerializeField, Min(1)] private int initialPoolSize = 12;
        [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 0f;

        private readonly List<AudioSource> sources = new List<AudioSource>();
        private int nextSourceIndex;

        public static void Play(AudioClip clip, Vector3 position, float volume = 1f, float pitchVariance = 0.04f)
        {
            if (clip == null)
            {
                return;
            }

            GameSfxPlayer player = EnsureInstance();
            if (player != null)
            {
                player.PlayInternal(clip, position, volume, pitchVariance);
            }
        }

        private static GameSfxPlayer EnsureInstance()
        {
            if (instance != null)
            {
                return instance;
            }

            instance = FindAnyObjectByType<GameSfxPlayer>();
            if (instance != null)
            {
                return instance;
            }

            GameObject audioObject = new GameObject("Game SFX Player");
            DontDestroyOnLoad(audioObject);
            instance = audioObject.AddComponent<GameSfxPlayer>();
            instance.BuildPool();
            return instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            BuildPool();
        }

        private void PlayInternal(AudioClip clip, Vector3 position, float volume, float pitchVariance)
        {
            AudioSource source = GetNextSource();
            source.transform.position = position;
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume) * masterVolume;
            source.pitch = Random.Range(1f - pitchVariance, 1f + pitchVariance);
            source.spatialBlend = spatialBlend;
            source.Play();
        }

        private AudioSource GetNextSource()
        {
            if (sources.Count == 0)
            {
                BuildPool();
            }

            AudioSource source = sources[nextSourceIndex];
            nextSourceIndex = (nextSourceIndex + 1) % sources.Count;
            return source;
        }

        private void BuildPool()
        {
            int targetCount = Mathf.Max(1, initialPoolSize);
            while (sources.Count < targetCount)
            {
                GameObject sourceObject = new GameObject($"SFX Source {sources.Count + 1}");
                sourceObject.transform.SetParent(transform, false);

                AudioSource source = sourceObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = spatialBlend;
                sources.Add(source);
            }
        }
    }
}
