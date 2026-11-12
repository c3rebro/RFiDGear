#nullable enable
using System;
using System.Threading;

namespace RFiDGear.Services.SecureKeys
{
    /// <summary>
    /// Where a command finds the <see cref="KeyResolutionContext"/> it asks providers with. The execution service opens one
    /// for each task attempt (it flows into the awaited command and nowhere else, so two runs never see each other's); a
    /// command that no run started, such as a button of a task dialog, finds the default of the application.
    /// <para>
    /// Neither holds a key. A bound slot with no context fails: it is never answered with an embedded key.
    /// </para>
    /// </summary>
    internal static class KeyResolutionScope
    {
        private static readonly AsyncLocal<KeyResolutionContext?> Ambient = new AsyncLocal<KeyResolutionContext?>();

        private static KeyResolutionContext? _default;

        /// <summary>Gets the context of the running task attempt, or else the default of the application, or <c>null</c>.</summary>
        internal static KeyResolutionContext? Current => Ambient.Value ?? Volatile.Read(ref _default);

        /// <summary>Opens a context for the current flow of execution. Dispose the result to restore the previous one.</summary>
        /// <param name="context">The context.</param>
        /// <returns>The scope; disposing it brings back the context that was current before.</returns>
        internal static IDisposable Open(KeyResolutionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var previous = Ambient.Value;
            Ambient.Value = context;

            return new Restore(previous);
        }

        /// <summary>
        /// Sets the context that commands use when no run has opened one. The application sets it once at start-up; tests
        /// set it and put it back.
        /// </summary>
        /// <param name="context">The context, or <c>null</c> for none.</param>
        internal static void SetDefault(KeyResolutionContext? context) => Volatile.Write(ref _default, context);

        private sealed class Restore : IDisposable
        {
            private readonly KeyResolutionContext? _previous;
            private int _disposed;

            internal Restore(KeyResolutionContext? previous)
            {
                _previous = previous;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                {
                    Ambient.Value = _previous;
                }
            }
        }
    }
}
