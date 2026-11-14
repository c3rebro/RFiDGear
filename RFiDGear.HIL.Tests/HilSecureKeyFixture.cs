#nullable enable
using RFiDGear.Infrastructure.ExtensionHost;

using System;
using System.Threading.Tasks;

using Xunit;
using Xunit.Sdk;

namespace RFiDGear.HIL.Tests
{
    /// <summary>
    /// The reader and the card for the secure-key cases. It is <see cref="HilFixture"/> behind a gate: unless
    /// <c>hil.settings.json</c> sets <c>secureKeys</c>, it does nothing at all, so it never opens the reader, and it never deletes
    /// the test application from the card when it ends. That matters because xunit creates the fixture of a collection also when
    /// every test in it is skipped: with <see cref="HilFixture"/> itself, running only the secure-key cases with the setting off
    /// would still have opened the reader and cleaned the card. The older DESFire cases keep <see cref="HilFixture"/> as it was.
    /// <para>
    /// The collections of this project run one after the other (see <c>AssemblyInfo.cs</c>), so the two fixtures never hold the
    /// reader at the same time.
    /// </para>
    /// </summary>
    public sealed class HilSecureKeyFixture : IAsyncLifetime
    {
        private readonly HilSettings _settings;
        private readonly HilFixture _reader;
        private readonly Func<ValueTask> _initialize;
        private readonly Func<ValueTask> _dispose;
        private bool _started;

        /// <summary>Creates the fixture for a test run, from the settings of the run.</summary>
        public HilSecureKeyFixture()
            : this(HilSettings.Current, new HilFixture(), null, null)
        {
        }

        /// <summary>Creates the fixture with the settings and the reader it is to use; a self-check gives its own steps.</summary>
        /// <param name="settings">What the person asked for.</param>
        /// <param name="reader">The fixture that finds the reader and the card.</param>
        /// <param name="initialize">Starts the reader; <see cref="HilFixture.InitializeAsync"/> when <see langword="null"/>.</param>
        /// <param name="dispose">Ends it; <see cref="HilFixture.DisposeAsync"/> when <see langword="null"/>.</param>
        internal HilSecureKeyFixture(HilSettings settings, HilFixture reader, Func<ValueTask>? initialize, Func<ValueTask>? dispose)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _initialize = initialize ?? reader.InitializeAsync;
            _dispose = dispose ?? reader.DisposeAsync;
        }

        /// <summary>Gets the host that drives the card, or <see langword="null"/> before the reader was started or when none was found.</summary>
        public HeadlessExtensionHost? Host => _started ? _reader.Host : null;

        /// <summary>Gets whether the fixture was asked to start the reader, and did.</summary>
        internal bool Started => _started;

        /// <inheritdoc/>
        public async ValueTask InitializeAsync()
        {
            if (!_settings.SecureKeysRequested)
            {
                return;
            }

            _started = true;
            await _initialize();
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if (_started)
            {
                await _dispose();
            }
        }

        /// <summary>
        /// Ends the test as a skip when the secure-key cases were not asked for or when no reader or DESFire card was found.
        /// A skip from inside a test is reported by xunit.v3 as a skip (xunit 2 reported it as a failure with the message prefix
        /// <c>$XunitDynamicSkip$</c>, which <c>Run-HilTests.ps1</c> still counts as skipped).
        /// </summary>
        public void SkipIfNoHardware()
        {
            if (!_started)
            {
                throw SkipException.ForSkip(_settings.SecureKeysSkipReason());
            }

            _reader.SkipIfNoHardware();
        }

        /// <summary>Deletes the reserved test application from the card if it is there; does nothing when the fixture did not start.</summary>
        /// <returns>The task of the cleanup.</returns>
        internal Task CleanupTestAidAsync() => _started ? _reader.CleanupTestAidAsync() : Task.CompletedTask;
    }

    /// <summary>The collection of the secure-key cases: one reader, one card, one case at a time.</summary>
    [CollectionDefinition("HILSecureKeys")]
    public sealed class HilSecureKeyCollection : ICollectionFixture<HilSecureKeyFixture> { }
}
