using System;
using System.Collections.Generic;
using UnityEngine;

namespace NeonBreaker.Pooling
{
    public sealed class ObjectPoolManager : MonoBehaviour
    {
        [Serializable]
        private sealed class PoolEntry
        {
            [SerializeField] private PoolKey key;
            [SerializeField] private PoolableGameObject prefab;
            [SerializeField] private int prewarmCount = 8;
            [SerializeField] private bool canExpand = true;
            [SerializeField] private Transform inactiveRoot;

            public PoolKey Key => key;
            public PoolableGameObject Prefab => prefab;
            public int PrewarmCount => Mathf.Max(0, prewarmCount);
            public bool CanExpand => canExpand;
            public Transform InactiveRoot => inactiveRoot;

            public void SetInactiveRoot(Transform root)
            {
                inactiveRoot = root;
            }
        }

        private sealed class RuntimePool : IPoolOwner
        {
            private readonly ObjectPoolManager owner;
            private readonly PoolEntry entry;
            private readonly Queue<PoolableGameObject> inactiveObjects = new Queue<PoolableGameObject>();

            public RuntimePool(ObjectPoolManager owner, PoolEntry entry)
            {
                this.owner = owner;
                this.entry = entry;
            }

            public void Prewarm()
            {
                for (int i = 0; i < entry.PrewarmCount; i++)
                {
                    PoolableGameObject instance = CreateInstance();
                    Despawn(instance);
                }
            }

            public PoolableGameObject Spawn(Vector3 position, Quaternion rotation)
            {
                PoolableGameObject instance = GetInstance();
                if (instance == null)
                {
                    return null;
                }

                Transform instanceTransform = instance.transform;
                instanceTransform.SetParent(null);
                instanceTransform.SetPositionAndRotation(position, rotation);

                instance.gameObject.SetActive(true);
                instance.NotifySpawned();

                return instance;
            }

            public void Despawn(PoolableGameObject instance)
            {
                if (instance == null)
                {
                    return;
                }

                instance.NotifyDespawned();
                instance.gameObject.SetActive(false);
                instance.transform.SetParent(entry.InactiveRoot);
                inactiveObjects.Enqueue(instance);
            }

            private PoolableGameObject GetInstance()
            {
                if (inactiveObjects.Count > 0)
                {
                    return inactiveObjects.Dequeue();
                }

                return entry.CanExpand ? CreateInstance() : null;
            }

            private PoolableGameObject CreateInstance()
            {
                PoolableGameObject instance = Instantiate(entry.Prefab, entry.InactiveRoot);
                instance.SetOwnerPool(this);
                return instance;
            }
        }

        [SerializeField] private bool makeGlobalInstance = true;
        [SerializeField] private PoolEntry[] pools;

        private readonly Dictionary<PoolKey, RuntimePool> poolsByKey = new Dictionary<PoolKey, RuntimePool>();

        public static ObjectPoolManager Instance { get; private set; }

        private void Awake()
        {
            if (makeGlobalInstance)
            {
                Instance = this;
            }

            BuildPools();
        }

        public PoolableGameObject Spawn(PoolKey key, Vector3 position, Quaternion rotation)
        {
            if (key == null)
            {
                Debug.LogError("[ObjectPoolManager] Spawn failed. PoolKey is null.", this);
                return null;
            }

            if (!poolsByKey.TryGetValue(key, out RuntimePool pool))
            {
                BuildPools();
                if (!poolsByKey.TryGetValue(key, out pool))
                {
                    Debug.LogError($"[ObjectPoolManager] Spawn failed. No pool registered for key: {key.name}", this);
                    return null;
                }
            }

            return pool.Spawn(position, rotation);
        }

        public bool HasPool(PoolKey key)
        {
            if (key == null)
            {
                return false;
            }

            if (poolsByKey.ContainsKey(key))
            {
                return true;
            }

            if (pools == null)
            {
                return false;
            }

            for (int i = 0; i < pools.Length; i++)
            {
                PoolEntry entry = pools[i];
                if (entry != null && entry.Key == key && entry.Prefab != null)
                {
                    return true;
                }
            }

            return false;
        }

        public T Spawn<T>(PoolKey key, Vector3 position, Quaternion rotation) where T : Component
        {
            PoolableGameObject instance = Spawn(key, position, rotation);
            if (instance == null)
            {
                return null;
            }

            T component = instance.GetComponent<T>();
            if (component == null)
            {
                component = instance.GetComponentInChildren<T>(true);
            }

            if (component == null)
            {
                Debug.LogError($"[ObjectPoolManager] Spawned pool object '{instance.name}' for key '{key.name}' has no {typeof(T).Name} component.", instance);
                instance.ReturnToPool();
                return null;
            }

            return component;
        }

        private void BuildPools()
        {
            poolsByKey.Clear();

            if (pools == null)
            {
                return;
            }

            for (int i = 0; i < pools.Length; i++)
            {
                PoolEntry entry = pools[i];
                if (entry == null || entry.Key == null || entry.Prefab == null)
                {
                    continue;
                }

                if (poolsByKey.ContainsKey(entry.Key))
                {
                    Debug.LogWarning($"[ObjectPoolManager] Duplicate pool key ignored: {entry.Key.name}", this);
                    continue;
                }

                if (entry.InactiveRoot == null)
                {
                    GameObject rootObject = new GameObject($"{entry.Key.name}_Inactive");
                    Transform root = rootObject.transform;
                    root.SetParent(transform);
                    entry.SetInactiveRoot(root);
                }

                RuntimePool runtimePool = new RuntimePool(this, entry);
                poolsByKey.Add(entry.Key, runtimePool);
                runtimePool.Prewarm();
            }
        }
    }
}
