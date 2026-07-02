using UnityEngine;

namespace NeonBreaker.Pooling
{
    public sealed class PoolableGameObject : MonoBehaviour
    {
        private IPoolOwner ownerPool;
        private IPoolLifecycle[] lifecycleTargets;

        public bool HasOwnerPool => ownerPool != null;
        public IPoolOwner OwnerPool => ownerPool;

        private void Awake()
        {
            lifecycleTargets = GetComponentsInChildren<IPoolLifecycle>(true);
        }

        public void SetOwnerPool(IPoolOwner pool)
        {
            ownerPool = pool;
        }

        public void NotifySpawned()
        {
            EnsureLifecycleCache();

            for (int i = 0; i < lifecycleTargets.Length; i++)
            {
                lifecycleTargets[i].OnSpawned();
            }
        }

        public void NotifyDespawned()
        {
            EnsureLifecycleCache();

            for (int i = 0; i < lifecycleTargets.Length; i++)
            {
                lifecycleTargets[i].OnDespawned();
            }
        }

        public void ReturnToPool()
        {
            if (ownerPool != null)
            {
                ownerPool.Despawn(this);
                return;
            }

            NotifyDespawned();
            gameObject.SetActive(false);
        }

        private void EnsureLifecycleCache()
        {
            lifecycleTargets ??= GetComponentsInChildren<IPoolLifecycle>(true);
        }
    }
}
