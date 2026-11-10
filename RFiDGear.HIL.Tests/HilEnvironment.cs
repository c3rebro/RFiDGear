using System;

using Xunit;

namespace RFiDGear.HIL.Tests
{
    /// <summary>
    /// What the person who runs the hardware tests has asked for. The tests of the reader scan need to be asked, so that
    /// they never touch a reader by accident: without the variables they are reported as skipped, and they open no COM
    /// port. (The older DESFire workflow tests are not gated; see the README.)
    /// </summary>
    internal static class HilEnvironment
    {
        /// <summary>Set to <c>1</c> to run the tests that need a reader.</summary>
        public const string HardwareVariable = "RFIDGEAR_HIL";

        /// <summary>Set to <c>1</c>, besides <see cref="HardwareVariable"/>, to run the tests that ask for cards to be moved.</summary>
        public const string InteractiveVariable = "RFIDGEAR_HIL_INTERACTIVE";

        /// <summary>Gets whether the tests that need a reader were asked for.</summary>
        public static bool HardwareRequested => IsOn(HardwareVariable);

        /// <summary>Gets whether the tests that ask for cards to be moved were asked for.</summary>
        public static bool InteractiveRequested => HardwareRequested && IsOn(InteractiveVariable);

        private static bool IsOn(string name) => string.Equals(Environment.GetEnvironmentVariable(name), "1", StringComparison.Ordinal);
    }

    /// <summary>
    /// A test that needs a reader. xunit 2 cannot skip a test from inside it (the dynamic skip of the assert library is
    /// reported as a failure), so the decision is made when the tests are discovered, from the environment.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class HilReaderFactAttribute : FactAttribute
    {
        /// <summary>Creates the attribute; the test is skipped unless a reader was asked for.</summary>
        public HilReaderFactAttribute()
        {
            if (!HilEnvironment.HardwareRequested)
            {
                Skip = $"Needs a reader. Set {HilEnvironment.HardwareVariable}=1 to run it (see RFiDGear.HIL.Tests/README.md).";
            }
        }
    }

    /// <summary>A test that asks the person at the reader to move cards, and so also needs the interactive variable.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class HilInteractiveFactAttribute : FactAttribute
    {
        /// <summary>Creates the attribute; the test is skipped unless a reader and the questions were asked for.</summary>
        public HilInteractiveFactAttribute()
        {
            if (!HilEnvironment.InteractiveRequested)
            {
                Skip = $"Asks you to move cards. Set {HilEnvironment.HardwareVariable}=1 and {HilEnvironment.InteractiveVariable}=1 to run it (see RFiDGear.HIL.Tests/README.md).";
            }
        }
    }
}
