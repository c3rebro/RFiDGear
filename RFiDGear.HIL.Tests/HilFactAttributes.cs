using System;

using Xunit;

namespace RFiDGear.HIL.Tests
{
    /// <summary>
    /// A test that needs a reader. xunit 2 cannot skip a test from inside it (the dynamic skip of the assert library is
    /// reported as a failure), so the decision is made when the tests are discovered, from <c>hil.settings.json</c> (see
    /// <see cref="HilSettings"/>).
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class HilReaderFactAttribute : FactAttribute
    {
        /// <summary>Creates the attribute; the test is skipped unless the reader-scan tests were asked for.</summary>
        public HilReaderFactAttribute()
        {
            if (!HilSettings.Current.HardwareRequested)
            {
                Skip = HilSettings.Current.SkipReason(movesCards: false);
            }
        }
    }

    /// <summary>
    /// A test (with data) that changes the keys of the test application on the card and puts credentials into the Windows
    /// Credential Manager for a moment. It runs only when <c>hil.settings.json</c> sets <c>secureKeys</c>, whatever the other
    /// settings say.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class HilSecureKeyTheoryAttribute : TheoryAttribute
    {
        /// <summary>Creates the attribute; the test is skipped unless the secure-key cases were asked for.</summary>
        public HilSecureKeyTheoryAttribute()
        {
            if (!HilSettings.Current.SecureKeysRequested)
            {
                Skip = HilSettings.Current.SecureKeysSkipReason();
            }
        }
    }

    /// <summary>A test that asks the person at the reader to move cards, and so also needs the setting <c>interactive</c>.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class HilInteractiveFactAttribute : FactAttribute
    {
        /// <summary>Creates the attribute; the test is skipped unless the reader-scan tests and the questions were asked for.</summary>
        public HilInteractiveFactAttribute()
        {
            if (!HilSettings.Current.InteractiveRequested)
            {
                Skip = HilSettings.Current.SkipReason(movesCards: true);
            }
        }
    }
}
