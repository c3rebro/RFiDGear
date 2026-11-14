using System;
using System.Runtime.CompilerServices;

using Xunit;

namespace RFiDGear.HIL.Tests
{
    /// <summary>
    /// A test that needs a reader. The decision to skip it is made when the tests are discovered, from
    /// <c>hil.settings.json</c> (see <see cref="HilSettings"/>), so that a test that was not asked for costs nothing and does
    /// not even create the fixture of its collection. (xunit 2 could not skip a test from inside it: the dynamic skip of the
    /// assert library was reported as a failure. xunit.v3 reports it as a skip, which the cases that find no reader rely on.)
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class HilReaderFactAttribute : FactAttribute
    {
        /// <summary>Creates the attribute; the test is skipped unless the reader-scan tests were asked for.</summary>
        /// <param name="sourceFilePath">The file the attribute is in; filled in by the compiler.</param>
        /// <param name="sourceLineNumber">The line the attribute is on; filled in by the compiler.</param>
        public HilReaderFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
            : base(sourceFilePath, sourceLineNumber)
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
        /// <param name="sourceFilePath">The file the attribute is in; filled in by the compiler.</param>
        /// <param name="sourceLineNumber">The line the attribute is on; filled in by the compiler.</param>
        public HilSecureKeyTheoryAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
            : base(sourceFilePath, sourceLineNumber)
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
        /// <param name="sourceFilePath">The file the attribute is in; filled in by the compiler.</param>
        /// <param name="sourceLineNumber">The line the attribute is on; filled in by the compiler.</param>
        public HilInteractiveFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
            : base(sourceFilePath, sourceLineNumber)
        {
            if (!HilSettings.Current.InteractiveRequested)
            {
                Skip = HilSettings.Current.SkipReason(movesCards: true);
            }
        }
    }
}
