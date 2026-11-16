#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;

using Elatec.NET.Interfaces;
using Elatec.NET.Sam;

using RFiDGear.Infrastructure;

using Serilog;

namespace RFiDGear.Services.Sam
{
    /// <summary>Why a PICC authentication or key change through the SAM did not end well; <see cref="SamPiccResult.Error"/> is what the task sees.</summary>
    internal enum SamPiccFailure
    {
        /// <summary>Nothing went wrong.</summary>
        None,

        /// <summary>No SAM reader is configured.</summary>
        NotConfigured,

        /// <summary>The SAM is in use: the setup window has it, or another command.</summary>
        SamBusy,

        /// <summary>The SAM reader is the reader of the card: one of them is wrong.</summary>
        SamIsCardReader,

        /// <summary>No SAM AV3 in the configured reader.</summary>
        SamNotFound,

        /// <summary>The SAM refused a command (the status word says which): no key entry like that, an entry that is no PICC key, a SAM that does not allow the key change.</summary>
        SamRefused,

        /// <summary>The card refused a command (the status word says which).</summary>
        CardRefused,

        /// <summary>The card accepted the SAM's first message, and the SAM did not accept the card's answer (<c>901E</c> was seen when the key entry lacked Keep IV).</summary>
        SamIntegrity,

        /// <summary>The key was changed on the card (it said so), and the authentication with the new key afterwards did not work.</summary>
        ChangeUnverified,

        /// <summary>A reader or the channel to the SAM failed.</summary>
        ReaderFailure,

        /// <summary>The request cannot be sent: an application number of more than 24 bits, a key number of the card that does not exist, or a SAM key that is no reference (entry 0). Nothing was sent.</summary>
        InvalidRequest,
    }

    /// <summary>
    /// What a PICC authentication or key change through the SAM ended with. It holds no key, no reference and no text of an exception.
    /// </summary>
    /// <param name="Error">What the task sees.</param>
    /// <param name="Failure">Why, when it did not end well.</param>
    /// <param name="SamStatus">The status word of the SAM that ended it, or 0.</param>
    /// <param name="CardStatus">The status word of the card that ended it, or 0.</param>
    /// <param name="KeyMayHaveChanged">Whether the card may hold the new key although the result is not a success: the cryptogram was sent and the answer is not known.</param>
    internal sealed record SamPiccResult(ERROR Error, SamPiccFailure Failure = SamPiccFailure.None, ushort SamStatus = 0, ushort CardStatus = 0, bool KeyMayHaveChanged = false)
    {
        /// <summary>Gets the result of a step that worked.</summary>
        internal static SamPiccResult Ok { get; } = new SamPiccResult(ERROR.NoError);

        /// <summary>Gets whether it worked.</summary>
        internal bool Succeeded => Error == ERROR.NoError;
    }

    /// <summary>The key change of a DESFire card through the SAM: which key authenticates, which key is replaced, and by which key.</summary>
    /// <param name="AppId">The application the card is in: 0 for the card itself.</param>
    /// <param name="AuthKeyNo">The number of the key on the card that authenticates.</param>
    /// <param name="AuthType">The kind of the key that authenticates.</param>
    /// <param name="AuthKey">The SAM key that authenticates.</param>
    /// <param name="TargetKeyNo">The number of the key on the card that is replaced.</param>
    /// <param name="NewType">The kind of the new key: for the card master key the type that the card gets, otherwise the type of the application.</param>
    /// <param name="CurrentKey">The SAM key that is replaced (the same as <paramref name="AuthKey"/> when the key that authenticates is replaced).</param>
    /// <param name="NewKey">The SAM key that the card gets.</param>
    /// <param name="Verify">Whether to authenticate with the new key afterwards.</param>
    internal readonly record struct SamPiccKeyChange(
        uint AppId,
        byte AuthKeyNo,
        SamPiccKeyType AuthType,
        SamKeyReference AuthKey,
        byte TargetKeyNo,
        SamPiccKeyType NewType,
        SamKeyReference CurrentKey,
        SamKeyReference NewKey,
        bool Verify);

    /// <summary>
    /// A DESFire card authenticated, and its keys changed, with keys that stay in a MIFARE SAM AV3. The PC relays between the SAM and the card
    /// (the SAM in the contact slot of a reader, the card in the contactless slot); the random numbers and the cryptograms come from the SAM
    /// and the card, so nothing here knows a key. Every step is <b>one attempt</b>: nothing that changes the card is repeated. The only thing
    /// sent twice is a SelectApplication, when the card answers the first with <c>91CA</c> (it dropped an authentication that was left half
    /// done); that command changes nothing. The SAM's PICC authentication is killed before the first step and after a part 1 that was not
    /// finished, because a SAM that waits for part 2 answers the next part 1 with <c>6700</c>.
    /// </summary>
    internal sealed class SamPiccSession
    {
        private readonly ISamApduChannel _sam;
        private readonly ISamTransport _card;

        /// <summary>Creates a session over the channel to the SAM and the transport to the card.</summary>
        /// <param name="sam">The channel to the SAM; the session does not close it.</param>
        /// <param name="card">The transport to the card; the session does not close it.</param>
        internal SamPiccSession(ISamApduChannel sam, ISamTransport card)
        {
            _sam = sam ?? throw new ArgumentNullException(nameof(sam));
            _card = card ?? throw new ArgumentNullException(nameof(card));
        }

        /// <summary>Authenticates a key of the card through a key entry of the SAM.</summary>
        /// <param name="appId">The application, 0 for the card itself.</param>
        /// <param name="cardKeyNo">The key number on the card.</param>
        /// <param name="type">The kind of key.</param>
        /// <param name="key">The SAM key entry and version.</param>
        /// <returns>The result; no key and no text of an exception is in it.</returns>
        internal async Task<SamPiccResult> AuthenticateAsync(uint appId, byte cardKeyNo, SamPiccKeyType type, SamKeyReference key)
        {
            if (!IsValidTarget(appId, cardKeyNo) || !key.IsValid)
            {
                return Invalid("authenticate");
            }

            try
            {
                var result = await AuthenticateCoreAsync("authenticate", appId, cardKeyNo, type, key).ConfigureAwait(false);

                return result;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                return Failed("authenticate", e, keyMayHaveChanged: false);
            }
        }

        /// <summary>
        /// Changes a key of the card through the SAM: authenticates, has the SAM build the cryptogram, sends it to the card and (if asked)
        /// authenticates again with the new key. Nothing is repeated, and nothing is sent to the card after a refusal.
        /// </summary>
        /// <param name="change">What to change.</param>
        /// <returns>The result; <see cref="SamPiccResult.KeyMayHaveChanged"/> says when the card may hold the new key although it did not end well.</returns>
        internal async Task<SamPiccResult> ChangeKeyAsync(SamPiccKeyChange change)
        {
            // default(SamKeyReference) is entry 0, the master entry of the SAM: none of the three keys may be one.
            if (!IsValidTarget(change.AppId, change.AuthKeyNo) || !IsValidTarget(change.AppId, change.TargetKeyNo)
                || !change.AuthKey.IsValid || !change.CurrentKey.IsValid || !change.NewKey.IsValid)
            {
                return Invalid("change key");
            }

            var cryptogramSent = false;

            try
            {
                var auth = await AuthenticateCoreAsync("change key: authenticate", change.AppId, change.AuthKeyNo, change.AuthType, change.AuthKey).ConfigureAwait(false);

                if (!auth.Succeeded)
                {
                    return auth;
                }

                var master = change.AppId == 0 && change.TargetKeyNo == 0;
                var same = change.TargetKeyNo == change.AuthKeyNo;
                var command = SamPiccFrames.SamChangeKeyPicc(same, master, change.TargetKeyNo, change.CurrentKey, change.NewKey);
                var built = await _sam.TransmitAsync(command).ConfigureAwait(false);

                if (built.StatusWord != SamPiccFrames.SamSuccess || built.Data.Length == 0 || built.Data.Length > 32)
                {
                    Record("change key: SAM cryptogram", "SamRefused", built.StatusWord, 0);

                    return new SamPiccResult(ERROR.KeyUnavailable, SamPiccFailure.SamRefused, SamStatus: built.StatusWord);
                }

                var keyNumberByte = master ? SamPiccFrames.MasterKeyNumberByte(change.NewType) : change.TargetKeyNo;

                cryptogramSent = true;
                var (_, cardStatus) = await ExchangeCardAsync(SamPiccFrames.CardChangeKey(keyNumberByte, built.Data)).ConfigureAwait(false);

                if (cardStatus != SamPiccFrames.CardSuccess)
                {
                    Record("change key: card", "CardRefused", 0, cardStatus);

                    // A card that answers with an error did not change the key.
                    return new SamPiccResult(SamPiccFrames.ErrorOfCard(cardStatus), SamPiccFailure.CardRefused, CardStatus: cardStatus);
                }

                Record("change key: card", "Done", 0, cardStatus);

                if (!change.Verify)
                {
                    return SamPiccResult.Ok;
                }

                // The authentication that was made is over (when the key that authenticated was changed) or stays (another key). Either way: start again.
                var verifyType = master ? change.NewType : change.AuthType;
                var verified = await AuthenticateCoreAsync("change key: verify", change.AppId, change.TargetKeyNo, verifyType, change.NewKey).ConfigureAwait(false);

                if (!verified.Succeeded)
                {
                    return new SamPiccResult(ERROR.Unknown, SamPiccFailure.ChangeUnverified, verified.SamStatus, verified.CardStatus, KeyMayHaveChanged: true);
                }

                return SamPiccResult.Ok;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // After the cryptogram went to the card, an exception means that the answer is not known.
                return Failed("change key", e, keyMayHaveChanged: cryptogramSent);
            }
        }

        // Select, part 1 of the card, part 1 of the SAM, part 2 of the card, part 2 of the SAM. The kill of the SAM's PICC authentication comes first
        // (best effort) and again when a part 1 was left unfinished.
        private async Task<SamPiccResult> AuthenticateCoreAsync(string step, uint appId, byte cardKeyNo, SamPiccKeyType type, SamKeyReference key)
        {
            var part1Sent = false;
            var finished = false;

            try
            {
                await KillAsync().ConfigureAwait(false);

                var selected = await SelectAsync(appId).ConfigureAwait(false);

                if (selected != SamPiccFrames.CardSuccess)
                {
                    Record(step + ": select", "CardRefused", 0, selected);

                    return new SamPiccResult(SamPiccFrames.ErrorOfCard(selected), SamPiccFailure.CardRefused, CardStatus: selected);
                }

                var length = SamPiccFrames.RandomLength(type);
                var (challenge, cardPart1) = await ExchangeCardAsync(SamPiccFrames.CardAuthenticatePart1(type, cardKeyNo)).ConfigureAwait(false);

                if (cardPart1 != SamPiccFrames.CardMoreFrames || challenge.Length != length)
                {
                    Record(step + ": card part 1", "CardRefused", 0, cardPart1);

                    return new SamPiccResult(SamPiccFrames.ErrorOfCard(cardPart1), SamPiccFailure.CardRefused, CardStatus: cardPart1);
                }

                part1Sent = true;
                var samPart1 = await _sam.TransmitAsync(SamPiccFrames.SamAuthenticatePart1(key, challenge)).ConfigureAwait(false);

                if (samPart1.StatusWord != SamPiccFrames.SamMoreParts || samPart1.Data.Length != 2 * length)
                {
                    Record(step + ": SAM part 1", "SamRefused", samPart1.StatusWord, 0);

                    return new SamPiccResult(ERROR.KeyUnavailable, SamPiccFailure.SamRefused, SamStatus: samPart1.StatusWord);
                }

                var (answer, cardPart2) = await ExchangeCardAsync(SamPiccFrames.CardAdditionalFrame(samPart1.Data)).ConfigureAwait(false);

                if (cardPart2 != SamPiccFrames.CardSuccess || answer.Length != length)
                {
                    Record(step + ": card part 2", "CardRefused", 0, cardPart2);

                    return new SamPiccResult(SamPiccFrames.ErrorOfCard(cardPart2), SamPiccFailure.CardRefused, CardStatus: cardPart2);
                }

                var samPart2 = await _sam.TransmitAsync(SamPiccFrames.SamAuthenticatePart2(answer)).ConfigureAwait(false);

                // Whatever the SAM says now, its part 2 was answered: it does not wait for anything.
                finished = true;

                if (samPart2.StatusWord != SamPiccFrames.SamSuccess)
                {
                    Record(step + ": SAM part 2", "SamIntegrity", samPart2.StatusWord, 0);

                    return new SamPiccResult(ERROR.Unknown, SamPiccFailure.SamIntegrity, SamStatus: samPart2.StatusWord);
                }

                Record(step, "Done", 0, 0);

                return SamPiccResult.Ok;
            }
            finally
            {
                if (part1Sent && !finished)
                {
                    await KillAsync().ConfigureAwait(false);
                }
            }
        }

        // SelectApplication; a 91CA answer says that the card dropped an authentication that was left half done, and the select is sent again once.
        private async Task<ushort> SelectAsync(uint appId)
        {
            var (_, status) = await ExchangeCardAsync(SamPiccFrames.CardSelectApplication(appId)).ConfigureAwait(false);

            if (status == SamPiccFrames.CardAborted)
            {
                (_, status) = await ExchangeCardAsync(SamPiccFrames.CardSelectApplication(appId)).ConfigureAwait(false);
            }

            return status;
        }

        // Kills an unfinished PICC authentication of the SAM. Best effort: a SAM that has none answers 9000 too, and a failure here is no reason to stop.
        private async Task KillAsync()
        {
            try
            {
                var answer = await _sam.TransmitAsync(SamPiccFrames.SamKillPiccAuthentication()).ConfigureAwait(false);

                if (answer.StatusWord != SamPiccFrames.SamSuccess)
                {
                    Record("kill", "NotAccepted", answer.StatusWord, 0);
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                Log.ForContext<SamPiccSession>().Warning("SAM PICC: kill failed ({ExceptionType}).", e.GetType().Name);
            }
        }

        // One APDU to the card: the data and the status word (the last two bytes). An answer of less than two bytes is a failure of the reader.
        private async Task<(byte[] Data, ushort Status)> ExchangeCardAsync(byte[] apdu)
        {
            var answer = await Task.Run(() => _card.Exchange(apdu)).ConfigureAwait(false);

            // An answer without a status word is no answer: after the cryptogram of a key change it must not read as a refusal.
            if (answer == null || answer.Length < 2)
            {
                throw new SamReaderException("The card answered without a status word.");
            }

            return (answer.Take(answer.Length - 2).ToArray(), (ushort)((answer[answer.Length - 2] << 8) | answer[answer.Length - 1]));
        }

        // An application number has 24 bits (the select sends three bytes), and a key of a card has a number from 0 to 13.
        private static bool IsValidTarget(uint appId, byte keyNo) => appId <= 0xFFFFFF && keyNo <= 0x0D;

        private static SamPiccResult Invalid(string step)
        {
            Record(step, "InvalidRequest", 0, 0);

            return new SamPiccResult(ERROR.ProtocolConstraint, SamPiccFailure.InvalidRequest);
        }

        private static SamPiccResult Failed(string step, Exception e, bool keyMayHaveChanged)
        {
            Log.ForContext<SamPiccSession>().Warning("SAM PICC: {Step} failed ({ExceptionType}), the key may have changed: {KeyMayHaveChanged}.", step, e.GetType().Name, keyMayHaveChanged);

            return new SamPiccResult(ERROR.TransportError, SamPiccFailure.ReaderFailure, KeyMayHaveChanged: keyMayHaveChanged);
        }

        // No key, no reference, no reader name, no UID: the step, the result and the status words.
        private static void Record(string step, string result, int samStatus, int cardStatus) =>
            Log.ForContext<SamPiccSession>().Information("SAM PICC: {Step} -> {Result} (SAM {SamStatus:X4}, card {CardStatus:X4}).", step, result, samStatus, cardStatus);
    }
}