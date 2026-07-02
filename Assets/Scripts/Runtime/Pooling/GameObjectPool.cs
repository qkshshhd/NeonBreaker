using System.Collections.Generic;
using UnityEngine;

namespace NeonBreaker.Pooling
{
    public sealed class GameObjectPool : MonoBehaviour, IPoolOwner
    {
        [SerializeField] private PoolableGameObject prefab;
        [SerializeField] private int prewarmCount = 8;
        [SerializeField] private bool canExpand = true;
        [SerializeField] private Transform inactiveRoot;

        private readonly Queue<PoolableGameObject> inactiveObjects = new Queue<PoolableGameObject>();

        private void Awake()
        {
            if (inactiveRoot == null)
            {
                GameObject rootObject = new GameObject($"{name}_Inactive");
                inactiveRoot = rootObject.transform;
                inactiveRoot.SetParent(transform);
            }

            for (int i = 0; i < prewarmCount; i++)
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

        public T Spawn<T>(Vector3 position, Quaternion rotation) where T : Component
        {
            PoolableGameObject instance = Spawn(position, rotation);
            return instance != null ? instance.GetComponent<T>() : null;
        }

        public void Despawn(PoolableGameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            instance.NotifyDespawned();
            instance.gameObject.SetActive(false);
            instance.transform.SetParent(inactiveRoot);
            inactiveObjects.Enqueue(instance);
        }

        private PoolableGameObject GetInstance()
        {
            if (inactiveObjects.Count > 0)
            {
                return inactiveObjects.Dequeue();
            }

            return canExpand ? CreateInstance() : null;
        }

        private PoolableGameObject CreateInstance()
        {
            PoolableGameObject instance = Instantiate(prefab, inactiveRoot);
            instance.SetOwnerPool(this);
            return instance;
        }
    }
}
