namespace NeonBreaker.Shared.StateMachine
{
    public sealed class StateMachine
    {
        public IState CurrentState { get; private set; }

        public void ChangeState(IState nextState)
        {
            if (CurrentState == nextState)
            {
                return;
            }

            CurrentState?.Exit();
            CurrentState = nextState;
            CurrentState?.Enter();
        }

        public void Tick(float deltaTime)
        {
            CurrentState?.Tick(deltaTime);
        }

        public void FixedTick(float fixedDeltaTime)
        {
            CurrentState?.FixedTick(fixedDeltaTime);
        }
    }
}

