using System;
using PHYSXR.Ghost;

namespace PHYSXR.Testing
{
    // Dependency-light tests for GhostBehaviourController. Ordinary focused
    // checks (no reflection), same style as the other *Tests classes.
    public static class GhostBehaviourControllerTests
    {
        public static int RunAll()
        {
            int passed = 0;
            int failed = 0;

            void Run(string name, Action test)
            {
                try
                {
                    test();
                    passed++;
                    Console.WriteLine("PASS: " + name);
                }
                catch (Exception ex)
                {
                    failed++;
                    Console.WriteLine("FAIL: " + name + " - " + ex.Message);
                }
            }

            Run("GHOSTCTRL-01_AllFiveStates_CanBeApplied", Test_AllFiveApply);
            Run("GHOSTCTRL-02_CurrentState_ReflectsEachAppliedState", Test_CurrentStateReflectsApplied);
            Run("GHOSTCTRL-03_ReapplyingSameState_IsSafeAndDeterministic", Test_ReapplySameState);
            Run("GHOSTCTRL-04_SwitchingBetweenStates_WorksCorrectly", Test_SwitchingStates);
            Run("GHOSTCTRL-05_InvalidState_ThrowsArgumentOutOfRangeException", Test_InvalidStateThrows);
            Run("GHOSTCTRL-06_ApplyState_DoesNotAlterSuppliedValue", Test_DoesNotAlterInput);

            Console.WriteLine(string.Format("Total: {0}, Passed: {1}, Failed: {2}", passed + failed, passed, failed));
            return failed;
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }

        private static readonly GhostExecutionState[] AllStates =
        {
            GhostExecutionState.Following,
            GhostExecutionState.Warning,
            GhostExecutionState.Investigating,
            GhostExecutionState.Protecting,
            GhostExecutionState.Retreating
        };

        private static void Test_AllFiveApply()
        {
            var controller = new GhostBehaviourController();

            foreach (GhostExecutionState state in AllStates)
            {
                // Must not throw for any currently-defined GhostExecutionState value.
                controller.ApplyState(state);
            }
        }

        private static void Test_CurrentStateReflectsApplied()
        {
            foreach (GhostExecutionState state in AllStates)
            {
                var controller = new GhostBehaviourController();
                controller.ApplyState(state);
                AssertTrue(controller.CurrentState == state, "expected CurrentState to reflect applied state " + state);
            }
        }

        private static void Test_ReapplySameState()
        {
            var controller = new GhostBehaviourController();

            controller.ApplyState(GhostExecutionState.Protecting);
            controller.ApplyState(GhostExecutionState.Protecting);
            controller.ApplyState(GhostExecutionState.Protecting);

            AssertTrue(controller.CurrentState == GhostExecutionState.Protecting, "reapplying the same state repeatedly must remain deterministic");
        }

        private static void Test_SwitchingStates()
        {
            var controller = new GhostBehaviourController();

            controller.ApplyState(GhostExecutionState.Following);
            AssertTrue(controller.CurrentState == GhostExecutionState.Following, "expected Following");

            controller.ApplyState(GhostExecutionState.Retreating);
            AssertTrue(controller.CurrentState == GhostExecutionState.Retreating, "expected Retreating");

            controller.ApplyState(GhostExecutionState.Warning);
            AssertTrue(controller.CurrentState == GhostExecutionState.Warning, "expected Warning after switching again");
        }

        private static void Test_InvalidStateThrows()
        {
            var controller = new GhostBehaviourController();
            var invalidState = (GhostExecutionState)999;

            bool threw = false;
            try
            {
                controller.ApplyState(invalidState);
            }
            catch (ArgumentOutOfRangeException)
            {
                threw = true;
            }

            AssertTrue(threw, "an undefined GhostExecutionState value must throw ArgumentOutOfRangeException");
        }

        private static void Test_DoesNotAlterInput()
        {
            var controller = new GhostBehaviourController();
            GhostExecutionState state = GhostExecutionState.Investigating;

            controller.ApplyState(state);

            AssertTrue(state == GhostExecutionState.Investigating, "ApplyState must not alter the supplied GhostExecutionState value");
        }
    }
}
