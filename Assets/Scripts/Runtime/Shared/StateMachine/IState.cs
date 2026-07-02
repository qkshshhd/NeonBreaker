namespace NeonBreaker.Shared.StateMachine
{
    public interface IState
    {
        void Enter();

        void Tick(float deltaTime);

        void FixedTick(float fixedDeltaTime);

        void Exit();
    }
}

