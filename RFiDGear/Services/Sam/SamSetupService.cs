#nullable enable
using System;
using System.Threading.Tasks;

using Elatec.NET.Interfaces;
using Elatec.NET.Sam;

using RFiDGear.Contracts;

using Serilog;

namespace RFiDGear.Services.Sam
{
    /// <summary>How a step of the SAM setup ended.</summary>
    internal enum SamStepResult
    {
        /// <summary>The step was done.</summary>
        Done,

        /// <summary>There is no SAM in the reader (or the setup has not been connected to one).</summary>
        NoSam,

        /// <summary>The SAM answered the host authentication with <c>6985</c>: it was never activated. No key was used.</summary>
        NotActivated,

        /// <summary>The SAM did not accept the authentication (a wrong key, or another refusal).</summary>
        AuthFailed,

        /// <summary>The person said no to the confirmation. Nothing was sent to the SAM.</summary>
        Declined,

        /// <summary>The step needs a host authentication first, and there is none. Nothing was sent to the SAM.</summary>
        NotAuthenticated,

        /// <summary>The key entry number is not one that may be written (1 to 127). Nothing was sent to the SAM.</summary>
        InvalidEntry,

        /// <summary>The key could not be obtained (not stored, the vault is down, not entered). Nothing was sent to the SAM.</summary>
        KeyUnavailable,

        /// <summary>What was entered or stored is not a 16 byte AES key. Nothing was sent to the SAM.</summary>
        InvalidKey,

        /// <summary>The SAM refused the command with a status word.</summary>
        Refused,

        /// <summary>The reader failed. The connection was let go of; the SAM may be left in any state.</summary>
        ReaderError,

        /// <summary>
        /// The step may have taken effect although it ended with an error: the SAM answered success, but its answer could not
        /// be checked (the proof or the CMAC is missing or wrong), or the reader failed after the last command was sent.
        /// For an activation, check the SAM and authenticate to find out. A key that was written cannot be read back and
        /// cannot be proved by an authentication: whether to write it again is for the person to decide.
        /// </summary>
        MayHaveTakenEffect,

        /// <summary>Something else went wrong; the type of the exception is in the log.</summary>
        Failed,
    }

    /// <summary>How a step ended, and the status word when the SAM said no.</summary>
    /// <param name="Result">The result.</param>
    /// <param name="StatusWord">The status word of the SAM, <c>0</c> when there is none.</param>
    /// <param name="Check">For a key entry that was written: what reading it back found. <see cref="SamKeyEntryCheck.NotChecked"/> for every other step.</param>
    internal sealed record SamStepOutcome(SamStepResult Result, ushort StatusWord = 0, SamKeyEntryCheck Check = SamKeyEntryCheck.NotChecked);

    /// <summary>What connecting to the SAM of a reader came to.</summary>
    /// <param name="Result"><see cref="SamStepResult.Done"/>, <see cref="SamStepResult.NoSam"/> or <see cref="SamStepResult.ReaderError"/>.</param>
    /// <param name="Reader">The reader of the SAM, when there is one.</param>
    /// <param name="Version">What SAM_GetVersion said, or <c>null</c> when the SAM did not say.</param>
    internal sealed record SamConnectOutcome(SamStepResult Result, SamReaderInfo? Reader, SamVersionInfo? Version);

    /// <summary>The questions the setup has to put to the person.</summary>
    internal interface ISamSetupDialogs
    {
        /// <summary>
        /// Asks whether the SAM may be activated. The activation worked on the one MIFARE SAM AV3 it was tried on and stayed after
        /// the SAM was taken out and put back; no way to undo it is known, so it is treated as permanent. Anything but a clear yes
        /// is a no.
        /// </summary>
        /// <returns><c>true</c> when the person confirmed.</returns>
        bool ConfirmActivate();
    }

    /// <summary>
    /// The steps that set a MIFARE SAM AV3 up through a PC/SC reader: connect, authenticate as host, activate a new SAM and
    /// write one key entry. Each step is one attempt; nothing is repeated, because a command that the SAM may have executed
    /// must not be sent twice (the one-attempt rule). The connection stays open between the steps, because the session of
    /// the host authentication belongs to it. Not thread safe: the caller runs one step at a time.
    /// </summary>
    /// <remarks>
    /// A key exists only in a <see cref="SamKeyBuffer"/> for the length of the step and is erased in a <c>finally</c>. The
    /// log gets step names, results, status words and the type of an exception, and never a key, a reference, a serial number
    /// or the text of an exception.
    /// </remarks>
    internal sealed class SamSetupService : IDisposable
    {
        /// <summary>The first key entry that may be written: entry 0 is the master key.</summary>
        internal const int FirstWritableEntry = 1;

        /// <summary>The last key entry of a MIFARE SAM AV3.</summary>
        internal const int LastWritableEntry = 127;

        /// <summary>The slot of the key that authenticates and activates (the master key, entry 0).</summary>
        internal const string MasterSlot = "sam.master";

        /// <summary>The slot of the key that is written.</summary>
        internal const string NewKeySlot = "sam.newkey";

        private const ushort NotActivatedStatus = 0x6985;
        private const ushort SuccessStatus = 0x9000;

        private readonly SamReaderFinder _finder;
        private readonly Func<ISamApduChannel, ISamHostSession> _createSession;
        private readonly ISamSetupDialogs _dialogs;
        private readonly SamKeyReader _keys;

        private SamConnection? _connection;
        private ISamHostSession? _session;

        /// <summary>Creates the setup.</summary>
        /// <param name="finder">Finds and opens the SAM in a PC/SC reader.</param>
        /// <param name="createSession">Makes the host session for a channel to the SAM.</param>
        /// <param name="dialogs">The questions to the person.</param>
        /// <param name="keys">Turns a named key into bytes.</param>
        internal SamSetupService(SamReaderFinder finder, Func<ISamApduChannel, ISamHostSession> createSession, ISamSetupDialogs dialogs, SamKeyReader keys)
        {
            _finder = finder ?? throw new ArgumentNullException(nameof(finder));
            _createSession = createSession ?? throw new ArgumentNullException(nameof(createSession));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _keys = keys ?? throw new ArgumentNullException(nameof(keys));
        }

        /// <summary>Whether a SAM is connected, so that the other steps have something to work on.</summary>
        internal bool IsConnected => _session != null;

        /// <summary>Whether the host authentication of this connection succeeded and nothing has failed since.</summary>
        internal bool IsAuthenticated => _session?.IsAuthenticated == true;

        /// <summary>Looks for SAMs in the PC/SC readers. Lets go of the current connection first.</summary>
        /// <returns>The readers with a SAM.</returns>
        internal Task<SamSearchResult> FindAsync()
        {
            Close();

            return Task.Run(() => _finder.Find());
        }

        /// <summary>
        /// Connects to the SAM in a reader and asks it for its version (SAM_GetVersion, which needs no key and changes nothing).
        /// The connection stays open for the steps that follow.
        /// </summary>
        /// <param name="readerName">The reader.</param>
        /// <returns>The outcome, with the hardware and software information when the SAM gave it.</returns>
        internal async Task<SamConnectOutcome> ConnectAsync(string readerName)
        {
            Close();

            try
            {
                // Opening the reader blocks: not on the thread of the window.
                var connection = await Task.Run(() => _finder.Open(readerName)).ConfigureAwait(false);

                if (connection == null)
                {
                    Record("connect", SamStepResult.NoSam, 0);
                    return new SamConnectOutcome(SamStepResult.NoSam, null, null);
                }

                _connection = connection;
                _session = _createSession(connection.Channel);

                var version = await SamDetection.ReadVersionAsync(connection.Channel).ConfigureAwait(false);

                Record("connect", SamStepResult.Done, 0);
                return new SamConnectOutcome(SamStepResult.Done, connection.Reader, version);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                Close();
                LogFailure("connect", e);
                return new SamConnectOutcome(SamStepResult.ReaderError, null, null);
            }
        }

        /// <summary>
        /// Authenticates as host with the key of entry 0, version 0. A SAM that was never activated answers with
        /// <c>6985</c>, which is <see cref="SamStepResult.NotActivated"/> and uses no key. A wrong key counts as a use of the
        /// key if a usage counter is linked to the entry.
        /// </summary>
        /// <param name="master">The key of entry 0.</param>
        /// <returns>The outcome.</returns>
        internal async Task<SamStepOutcome> AuthenticateAsync(SamKeySource master)
        {
            if (_session == null)
            {
                return Record("authenticate", SamStepResult.NoSam, 0);
            }

            using var read = await _keys.ReadAsync(master, SecureKeyUsage.Authenticate, MasterSlot).ConfigureAwait(false);

            if (read.Key == null)
            {
                return Record("authenticate", read.Failure ?? SamStepResult.KeyUnavailable, 0);
            }

            try
            {
                await _session.AuthenticateHostAsync(0, 0, read.Key.Bytes).ConfigureAwait(false);

                return Record("authenticate", SamStepResult.Done, 0);
            }
            catch (SamReaderException e)
            {
                return ReaderFailed("authenticate", e);
            }
            catch (SamException e)
            {
                return Record("authenticate", e.StatusWord == NotActivatedStatus ? SamStepResult.NotActivated : SamStepResult.AuthFailed, e.StatusWord);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                return OtherFailure("authenticate", e);
            }
        }

        /// <summary>
        /// Activates a new SAM with SAM_LockUnlock. It asks the person first and sends nothing, and reads no key, when the
        /// answer is not a clear yes. The activation (the frame comes from an AV2 library) worked on a real MIFARE SAM AV3 on 2026-11-15 and persists; no way back is known, so it is treated as irreversible (RFiDGear.HIL.Tests/SAM-AV3-FIRST-CONTACT.md).
        /// There is no authentication afterwards: the person starts it.
        /// </summary>
        /// <param name="master">The key of entry 0 (all zero on a new SAM).</param>
        /// <returns>The outcome.</returns>
        internal async Task<SamStepOutcome> ActivateAsync(SamKeySource master)
        {
            if (_session == null)
            {
                return Record("activate", SamStepResult.NoSam, 0);
            }

            if (!_dialogs.ConfirmActivate())
            {
                return Record("activate", SamStepResult.Declined, 0);
            }

            using var read = await _keys.ReadAsync(master, SecureKeyUsage.Authenticate, MasterSlot).ConfigureAwait(false);

            if (read.Key == null)
            {
                return Record("activate", read.Failure ?? SamStepResult.KeyUnavailable, 0);
            }

            try
            {
                await _session.ActivateSamAsync(0, 0, read.Key.Bytes).ConfigureAwait(false);

                return Record("activate", SamStepResult.Done, 0);
            }
            catch (SamReaderException e)
            {
                // The last of the three commands may have been executed before the reader failed.
                Close();
                LogFailure("activate", e);
                return Record("activate", SamStepResult.MayHaveTakenEffect, 0);
            }
            catch (SamException e)
            {
                if (e.StatusWord != SuccessStatus)
                {
                    return Record("activate", SamStepResult.Refused, e.StatusWord);
                }

                // Success of the last command with a proof that does not check out: the SAM may be activated all the same. What the
                // window knows of it is not to be trusted any more (it may still offer an activation), so the connection ends and
                // the next step has to start with a fresh check.
                Close();

                return Record("activate", SamStepResult.MayHaveTakenEffect, e.StatusWord);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                return OtherFailure("activate", e);
            }
        }

        /// <summary>
        /// Writes an AES-128 PICC key into a key entry of the SAM (version 0, changeable with the master key, no usage
        /// counter). Needs a host authentication first. The master key entry is never written.
        /// </summary>
        /// <param name="entryNumber">The key entry, 1 to 127.</param>
        /// <param name="key">The key to write.</param>
        /// <returns>The outcome. The SAM's answer is proof that it was written; the key cannot be read back.</returns>
        internal async Task<SamStepOutcome> WriteKeyEntryAsync(int entryNumber, SamKeySource key)
        {
            if (entryNumber < FirstWritableEntry || entryNumber > LastWritableEntry)
            {
                return Record("write", SamStepResult.InvalidEntry, 0);
            }

            if (_session == null)
            {
                return Record("write", SamStepResult.NoSam, 0);
            }

            if (!_session.IsAuthenticated)
            {
                return Record("write", SamStepResult.NotAuthenticated, 0);
            }

            using var read = await _keys.ReadAsync(key, SecureKeyUsage.ChangeKeyNew, NewKeySlot).ConfigureAwait(false);

            if (read.Key == null)
            {
                return Record("write", read.Failure ?? SamStepResult.KeyUnavailable, 0);
            }

            try
            {
                await _session.ChangeKeyEntryAsync((byte)entryNumber, SamAv3KeyEntry.CreateAes128PiccKey(read.Key.Bytes, 0)).ConfigureAwait(false);

                // The key cannot be read back, but the settings of its entry can: a blank entry is disabled (SET bit 9), and a key
                // that was written to an entry that stays disabled is no key anybody can use.
                return Record("write", SamStepResult.Done, 0, await ReadBackAsync((byte)entryNumber).ConfigureAwait(false));
            }
            catch (SamReaderException e)
            {
                return ReaderFailed("write", e);
            }
            catch (SamException e)
            {
                // The library throws with 9000 when the SAM said success and its CMAC is missing or wrong: the entry may have been
                // written, and "refused" would say that it was not. The session is gone either way (the library discards it).
                return Record("write", e.StatusWord == SuccessStatus ? SamStepResult.MayHaveTakenEffect : SamStepResult.Refused, e.StatusWord);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                return OtherFailure("write", e);
            }
        }

        /// <summary>Lets go of the SAM and the reader. The host session of the SAM ends with the connection.</summary>
        internal void Close()
        {
            try
            {
                _session?.Dispose();
            }
            finally
            {
                _session = null;

                try
                {
                    _connection?.Dispose();
                }
                finally
                {
                    _connection = null;
                }
            }
        }

        /// <inheritdoc />
        public void Dispose() => Close();

        private SamStepOutcome ReaderFailed(string step, Exception e)
        {
            Close();
            LogFailure(step, e);

            return Record(step, SamStepResult.ReaderError, 0);
        }

        private SamStepOutcome OtherFailure(string step, Exception e)
        {
            Close();
            LogFailure(step, e);

            return Record(step, SamStepResult.Failed, 0);
        }

        // The write is done when this runs; whatever it finds, it never turns the write into a failure.
        private async Task<SamKeyEntryCheck> ReadBackAsync(byte entryNumber)
        {
            try
            {
                var info = await _session!.ReadKeyEntryAsync(entryNumber).ConfigureAwait(false);

                return info?.Verdict ?? SamKeyEntryCheck.Unreadable;
            }
            catch (SamReaderException e)
            {
                Close();
                LogFailure("read back", e);

                return SamKeyEntryCheck.Unreadable;
            }
            catch (SamException e)
            {
                // The library discards the session after a failed command; the entry was written all the same.
                LogFailure("read back", e);

                return SamKeyEntryCheck.Unreadable;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                Close();
                LogFailure("read back", e);

                return SamKeyEntryCheck.Unreadable;
            }
        }

        private static SamStepOutcome Record(string step, SamStepResult result, ushort statusWord, SamKeyEntryCheck check = SamKeyEntryCheck.NotChecked)
        {
            Log.ForContext<SamSetupService>().Information("SAM setup: {Step} -> {Result} (status word {StatusWord:X4}, entry check {Check}).", step, result, statusWord, check);

            return new SamStepOutcome(result, statusWord, check);
        }

        private static void LogFailure(string step, Exception e)
        {
            // The type only: the text of an exception of a reader library or of the SAM layer is not for a log.
            Log.ForContext<SamSetupService>().Warning("SAM setup: {Step} failed ({ExceptionType}).", step, e.GetType().Name);
        }
    }
}
