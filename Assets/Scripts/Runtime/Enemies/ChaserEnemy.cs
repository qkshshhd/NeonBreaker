namespace NeonBreaker.Enemies
{
    [UnityEngine.RequireComponent(typeof(ChaserMeleeEnemyBehavior))]
    public sealed class ChaserEnemy : EnemyController
    {
        protected override IEnemyBehavior CreateFallbackBehavior()
        {
            return gameObject.AddComponent<ChaserMeleeEnemyBehavior>();
        }
    }
}
