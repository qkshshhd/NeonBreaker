using System.Collections.Generic;
using NeonBreaker.Dungeon;
using NeonBreaker.Rooms;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace NeonBreaker.Environment
{
    public sealed class RoomAtmosphere2D : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private RoomRunManager runManager;
        [SerializeField] private TilemapDungeonGenerator dungeonGenerator;

        [Header("Dust")]
        [SerializeField] private bool createDustParticles = true;
        [SerializeField] private ParticleSystem dustParticles;
        [SerializeField] private Material dustMaterial;
        [SerializeField, Min(0)] private int maxDustParticles = 90;
        [SerializeField, Min(0f)] private float dustRatePerArea = 0.07f;
        [SerializeField] private Vector2 dustLifetimeRange = new Vector2(5f, 9f);
        [SerializeField] private Vector2 dustSizeRange = new Vector2(0.015f, 0.045f);
        [SerializeField] private Vector2 dustSpeedRange = new Vector2(0.02f, 0.08f);
        [SerializeField, Range(0f, 1f)] private float dustAlpha = 0.28f;
        [SerializeField] private Color dustColor = new Color(0.55f, 0.9f, 1f, 1f);
        [SerializeField] private float dustNoiseStrength = 0.32f;
        [SerializeField] private float dustNoiseFrequency = 0.18f;
        [SerializeField] private string dustSortingLayer = "Default";
        [SerializeField] private int dustSortingOrder = 15;

        [Header("Accent Lights")]
        [SerializeField] private bool createAccentLights = true;
        [SerializeField, Min(0)] private int accentLightCount = 4;
        [SerializeField] private Color[] accentLightColors =
        {
            new Color(0.1f, 0.95f, 1f, 1f),
            new Color(1f, 0.12f, 0.55f, 1f),
            new Color(0.75f, 0.25f, 1f, 1f)
        };
        [SerializeField] private Vector2 accentIntensityRange = new Vector2(0.35f, 0.8f);
        [SerializeField] private Vector2 accentRadiusRange = new Vector2(2.2f, 4.8f);
        [SerializeField] private Vector2 accentFlickerSpeedRange = new Vector2(1.2f, 2.8f);
        [SerializeField, Range(0f, 1f)] private float accentFlickerAmount = 0.16f;
        [SerializeField, Min(0)] private int lightPlacementPaddingCells = 2;

        private readonly List<Light2D> accentLights = new List<Light2D>();
        private readonly List<Vector3> safeRoomPositions = new List<Vector3>();

        private void Awake()
        {
            ResolveSources();
            EnsureDustParticles();
            EnsureAccentLights();
        }

        private void OnEnable()
        {
            ResolveSources();
            if (runManager != null)
            {
                runManager.RunRoomStarted += HandleRunRoomStarted;
                runManager.RunRoomCleared += HandleRunRoomCleared;
            }

            RefreshForCurrentRoom();
        }

        private void OnDisable()
        {
            if (runManager != null)
            {
                runManager.RunRoomStarted -= HandleRunRoomStarted;
                runManager.RunRoomCleared -= HandleRunRoomCleared;
            }
        }

        private void HandleRunRoomStarted(int roomIndex, RoomDefinition room)
        {
            RefreshForRoom(roomIndex, false);
        }

        private void HandleRunRoomCleared(int roomIndex, RoomDefinition room)
        {
            RefreshForRoom(roomIndex, true);
        }

        [ContextMenu("Refresh Atmosphere")]
        public void RefreshForCurrentRoom()
        {
            int roomIndex = runManager != null ? runManager.CurrentRoomIndex : 0;
            RefreshForRoom(roomIndex, false);
        }

        private void RefreshForRoom(int roomIndex, bool roomCleared)
        {
            ResolveSources();

            if (dungeonGenerator == null || !TryGetRoomWorldBounds(roomIndex, out Bounds bounds))
            {
                SetDustPlaying(false);
                SetAccentLightsActive(false);
                return;
            }

            if (createDustParticles)
            {
                ConfigureDust(bounds, roomCleared);
            }

            if (createAccentLights)
            {
                PositionAccentLights(roomIndex, bounds, roomCleared);
            }
        }

        private void ConfigureDust(Bounds bounds, bool roomCleared)
        {
            EnsureDustParticles();
            if (dustParticles == null)
            {
                return;
            }

            float area = Mathf.Max(1f, bounds.size.x * bounds.size.y);
            float rate = Mathf.Min(maxDustParticles, area * dustRatePerArea);
            if (roomCleared)
            {
                rate *= 0.55f;
            }

            Transform dustTransform = dustParticles.transform;
            dustTransform.position = new Vector3(bounds.center.x, bounds.center.y, transform.position.z);

            ParticleSystem.MainModule main = dustParticles.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(0, maxDustParticles);
            main.startLifetime = new ParticleSystem.MinMaxCurve(
                Mathf.Max(0.1f, dustLifetimeRange.x),
                Mathf.Max(dustLifetimeRange.x, dustLifetimeRange.y));
            main.startSpeed = new ParticleSystem.MinMaxCurve(
                Mathf.Max(0f, dustSpeedRange.x),
                Mathf.Max(dustSpeedRange.x, dustSpeedRange.y));
            main.startSize = new ParticleSystem.MinMaxCurve(
                Mathf.Max(0.001f, dustSizeRange.x),
                Mathf.Max(dustSizeRange.x, dustSizeRange.y));
            Color color = dustColor;
            color.a = dustAlpha;
            main.startColor = color;
            main.gravityModifier = 0f;

            ParticleSystem.EmissionModule emission = dustParticles.emission;
            emission.enabled = rate > 0f;
            emission.rateOverTime = rate;

            ParticleSystem.ShapeModule shape = dustParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(Mathf.Max(0.1f, bounds.size.x), Mathf.Max(0.1f, bounds.size.y), 0.1f);

            ParticleSystem.NoiseModule noise = dustParticles.noise;
            noise.enabled = dustNoiseStrength > 0f;
            noise.strength = dustNoiseStrength;
            noise.frequency = dustNoiseFrequency;
            noise.scrollSpeed = 0.18f;

            ParticleSystemRenderer renderer = dustParticles.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.sortingLayerName = dustSortingLayer;
                renderer.sortingOrder = dustSortingOrder;
                if (dustMaterial != null)
                {
                    renderer.sharedMaterial = dustMaterial;
                }
            }

            if (!dustParticles.isPlaying)
            {
                dustParticles.Play(true);
            }
        }

        private void PositionAccentLights(int roomIndex, Bounds bounds, bool roomCleared)
        {
            EnsureAccentLights();
            SetAccentLightsActive(true);

            safeRoomPositions.Clear();
            if (dungeonGenerator != null)
            {
                dungeonGenerator.TryGetRoomSafeSpawnPositions(roomIndex, safeRoomPositions);
            }

            for (int i = 0; i < accentLights.Count; i++)
            {
                Light2D light = accentLights[i];
                if (light == null)
                {
                    continue;
                }

                Vector3 position = GetAccentLightPosition(bounds);
                light.transform.position = position;
                light.color = GetAccentLightColor(i);
                light.intensity = Random.Range(accentIntensityRange.x, Mathf.Max(accentIntensityRange.x, accentIntensityRange.y))
                    * (roomCleared ? 0.78f : 1f);
                light.pointLightOuterRadius = Random.Range(accentRadiusRange.x, Mathf.Max(accentRadiusRange.x, accentRadiusRange.y));
                light.pointLightInnerRadius = light.pointLightOuterRadius * 0.22f;

                RoomAccentLightFlicker2D flicker = light.GetComponent<RoomAccentLightFlicker2D>();
                if (flicker == null)
                {
                    flicker = light.gameObject.AddComponent<RoomAccentLightFlicker2D>();
                }

                flicker.Configure(
                    light,
                    light.intensity,
                    accentFlickerAmount,
                    Random.Range(accentFlickerSpeedRange.x, Mathf.Max(accentFlickerSpeedRange.x, accentFlickerSpeedRange.y)));
            }
        }

        private Vector3 GetAccentLightPosition(Bounds bounds)
        {
            if (safeRoomPositions.Count > 0)
            {
                Vector3 candidate = safeRoomPositions[Random.Range(0, safeRoomPositions.Count)];
                candidate.z = transform.position.z;
                return candidate;
            }

            float padding = Mathf.Max(0f, lightPlacementPaddingCells);
            float xMin = bounds.min.x + padding;
            float xMax = bounds.max.x - padding;
            float yMin = bounds.min.y + padding;
            float yMax = bounds.max.y - padding;
            if (xMin > xMax)
            {
                xMin = bounds.min.x;
                xMax = bounds.max.x;
            }

            if (yMin > yMax)
            {
                yMin = bounds.min.y;
                yMax = bounds.max.y;
            }

            return new Vector3(Random.Range(xMin, xMax), Random.Range(yMin, yMax), transform.position.z);
        }

        private Color GetAccentLightColor(int index)
        {
            if (accentLightColors == null || accentLightColors.Length == 0)
            {
                return Color.cyan;
            }

            return accentLightColors[Mathf.Abs(index) % accentLightColors.Length];
        }

        private bool TryGetRoomWorldBounds(int roomIndex, out Bounds bounds)
        {
            bounds = default;
            if (dungeonGenerator == null)
            {
                return false;
            }

            bool found = dungeonGenerator.TryGetRoomFloorBounds(roomIndex, out RectInt roomBounds)
                || dungeonGenerator.TryGetRoomBounds(roomIndex, out roomBounds);
            Tilemap floorTilemap = dungeonGenerator.FloorTilemap;
            if (!found || floorTilemap == null)
            {
                return false;
            }

            Vector3 min = floorTilemap.CellToWorld(new Vector3Int(roomBounds.xMin, roomBounds.yMin, 0));
            Vector3 max = floorTilemap.CellToWorld(new Vector3Int(roomBounds.xMax, roomBounds.yMax, 0));
            bounds = new Bounds((min + max) * 0.5f, new Vector3(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y), 0f));
            return true;
        }

        private void EnsureDustParticles()
        {
            if (!createDustParticles || dustParticles != null)
            {
                return;
            }

            GameObject dustObject = new GameObject("Room Dust Particles");
            dustObject.transform.SetParent(transform, false);
            dustParticles = dustObject.AddComponent<ParticleSystem>();
        }

        private void EnsureAccentLights()
        {
            if (!createAccentLights)
            {
                return;
            }

            int desiredCount = Mathf.Max(0, accentLightCount);
            while (accentLights.Count < desiredCount)
            {
                GameObject lightObject = new GameObject($"Room Accent Light {accentLights.Count + 1}");
                lightObject.transform.SetParent(transform, false);
                Light2D light = lightObject.AddComponent<Light2D>();
                light.lightType = Light2D.LightType.Point;
                accentLights.Add(light);
            }

            for (int i = 0; i < accentLights.Count; i++)
            {
                if (accentLights[i] != null)
                {
                    accentLights[i].gameObject.SetActive(i < desiredCount);
                }
            }
        }

        private void SetDustPlaying(bool playing)
        {
            if (dustParticles == null)
            {
                return;
            }

            if (playing)
            {
                dustParticles.Play(true);
            }
            else
            {
                dustParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void SetAccentLightsActive(bool active)
        {
            for (int i = 0; i < accentLights.Count; i++)
            {
                if (accentLights[i] != null)
                {
                    accentLights[i].gameObject.SetActive(active);
                }
            }
        }

        private void ResolveSources()
        {
            if (runManager == null)
            {
                runManager = FindAnyObjectByType<RoomRunManager>();
            }

            if (dungeonGenerator == null)
            {
                dungeonGenerator = FindAnyObjectByType<TilemapDungeonGenerator>();
            }
        }
    }
}
