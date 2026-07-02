using System;

namespace NeonBreaker.Enemies
{
    public interface IRoomEnemy
    {
        event Action<IRoomEnemy> Died;
    }
}
