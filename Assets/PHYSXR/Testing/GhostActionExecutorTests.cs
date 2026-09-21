using System;
using PHYSXR.Core.Enums;
using PHYSXR.Ghost;

namespace PHYSXR.Testing
{
    // Dependency-light tests for GhostActionExecutor. Ordinary focused
    // checks (no reflection), same style as the other *Tests classes.
    public static class GhostActionExecutorTests
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

            Run("GHOSTEXEC-01_Follow_MapsToFollowing", Test_Follow);
            Run("GHOSTEXEC-02_Warn_MapsToWarning", Test_Warn);
            Run("GHOSTEXEC-03_Investigate_MapsToInvestigating", Test_Investigate);
            Run("GHOSTEXEC-04_Protect_MapsToProtecting", Test_Protect);
            Run("GHOSTEXEC-05_Retreat_MapsToRetreating", Test_Retreat);
            Run("GHOSTEXEC-06_AllFiveActionsAreHandled", Test_AllFiveHandled);
            Run("GHOSTEXEC-07_UnrecognizedAction_ThrowsRatherThanSilentlyDefaulting", Test_UnrecognizedThrows);
            Run("GHOSTEXEC-08_RepeatedExecution_IsDeterministic", Test_Deterministic);
            Run("GHOSTEXEC-09_ExecuteDoesNotAlterSuppliedAction", Test_DoesNotAlterInput);

            Console.WriteLine(string.Format("Total: {0}, Passed: {1}, Failed: {2}", passed + failed, passed, failed));
            return failed;
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }

        private static void Test_Follow()
        {
            var executor = new GhostActionExecutor();
            AssertTrue(executor.Execute(GhostAction.FOLLOW) == GhostExecutionState.Following, "FOLLOW should map to Following");
        }

        private static void Test_Warn()
        {
            var executor = new GhostActionExecutor();
            AssertTrue(executor.Execute(GhostAction.WARN) == GhostExecutionState.Warning, "WARN should map to Warning");
        }

        private static void Test_Investigate()
        {
            var executor = new GhostActionExecutor();
            AssertTrue(executor.Execute(GhostAction.INVESTIGATE) == GhostExecutionState.Investigating, "INVESTIGATE should map to Investigating");
        }

        private static void Test_Protect()
        {
            var executor = new GhostActionExecutor();
            AssertTrue(executor.Execute(GhostAction.PROTECT) == GhostExecutionState.Protecting, "PROTECT should map to Protecting");
        }

        private static void Test_Retreat()
        {
            var executor = new GhostActionExecutor();
            AssertTrue(executor.Execute(GhostAction.RETREAT) == GhostExecutionState.Retreating, "RETREAT should map to Retreating");
        }

        private static void Test_AllFiveHandled()
        {
            var executor = new GhostActionExecutor();

            foreach (GhostAction action in Enum.GetValues(typeof(GhostAction)))
            {
                // Must not throw for any currently-defined GhostAction value.
                GhostExecutionState result = executor.Execute(action);
                AssertTrue(Enum.IsDefined(typeof(GhostExecutionState), result), "expected a defined GhostExecutionState for " + action);
            }
        }

        private static void Test_UnrecognizedThrows()
        {
            var executor = new GhostActionExecutor();
            var invalidAction = (GhostAction)999;

            bool threw = false;
            try
            {
                executor.Execute(invalidAction);
            }
            catch (ArgumentOutOfRangeException)
            {
                threw = true;
            }

            AssertTrue(threw, "an unrecognized GhostAction value must throw rather than silently resolve to a default execution state");
        }

        private static void Test_Deterministic()
        {
            var executor = new GhostActionExecutor();

            GhostExecutionState first = executor.Execute(GhostAction.PROTECT);
            GhostExecutionState second = executor.Execute(GhostAction.PROTECT);
            GhostExecutionState third = new GhostActionExecutor().Execute(GhostAction.PROTECT);

            AssertTrue(first == second, "repeated calls on the same executor must be identical");
            AssertTrue(first == third, "a fresh executor instance must produce the identical result for identical input");
        }

        private static void Test_DoesNotAlterInput()
        {
            var executor = new GhostActionExecutor();
            GhostAction action = GhostAction.INVESTIGATE;

            executor.Execute(action);

            AssertTrue(action == GhostAction.INVESTIGATE, "Execute must not alter the supplied GhostAction value");
        }
    }
}
