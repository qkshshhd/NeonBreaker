namespace NeonBreaker.Pooling
{
    public interface IPoolLifecycle
    {
        void OnSpawned();

        void OnDespawned();
    }
}

