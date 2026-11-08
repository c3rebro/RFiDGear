using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ReaderProviders;
using System;
using System.Reflection;

namespace RFiDGear.Tests
{
    /// <summary>
    /// Makes a test reader the one the application talks to for the length of a test, and puts the previous one back.
    /// The reader is process-wide static state, so tests that use it must not run in parallel, which the assembly
    /// already ensures.
    /// </summary>
    internal sealed class ReaderDeviceScope : IDisposable
    {
        private static readonly FieldInfo InstanceField =
            typeof(ReaderDevice).GetField("instance", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ReaderDevice has no static field 'instance' any more; update this helper.");

        private readonly ReaderTypes originalReader;
        private readonly ReaderDevice? originalInstance;

        private ReaderDeviceScope(ReaderDevice? device)
        {
            originalReader = ReaderDevice.Reader;
            originalInstance = InstanceField.GetValue(null) as ReaderDevice;

            // No device means no reader at all: the application asks for none and gets null.
            ReaderDevice.Reader = device == null ? ReaderTypes.None : ReaderTypes.Elatec;
            InstanceField.SetValue(null, device);
        }

        /// <summary>Makes <paramref name="device"/> the reader until the returned scope is disposed.</summary>
        /// <param name="device">The test reader, reported as an Elatec reader; <see langword="null"/> for no reader at all.</param>
        /// <returns>The scope; dispose it, in a <c>finally</c> or a <c>using</c>, to restore the previous reader.</returns>
        public static ReaderDeviceScope Use(ReaderDevice? device) => new ReaderDeviceScope(device);

        /// <inheritdoc />
        public void Dispose()
        {
            ReaderDevice.Reader = originalReader;
            InstanceField.SetValue(null, originalInstance);
        }
    }
}
