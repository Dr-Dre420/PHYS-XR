using PHYSXR.Core.Data;

namespace PHYSXR.PhysicalWorld
{
    public class PhysicalStateManager
    {
        private PhysicalState currentState;

        public void UpdateState(PhysicalState newState)
        {
            if (newState == null)
                return;

            currentState = newState;
        }

        public PhysicalState GetCurrentState()
        {
            return currentState;
        }
    }
}