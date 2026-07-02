namespace NeonBreaker.Pooling
{
    public interface IPoolOwner
    {
        void Despawn(PoolableGameObject instance);
    }
}

