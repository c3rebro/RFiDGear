using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.AccessControl;
using RFiDGear.Infrastructure.ReaderProviders;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Infrastructure.Ultralight;
using RFiDGear.Models;
using RFiDGear.Services.Sam;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RFiDGear.Tests.KeySlots
{
    /// <summary>One call of the reader that carried keys: the method, and each key it received in the order of its parameters.</summary>
    /// <param name="Method">The name of the reader method.</param>
    /// <param name="Keys">The keys as the reader received them, without spaces and in upper case.</param>
    internal sealed record KeyCall(string Method, IReadOnlyList<string> Keys);

    /// <summary>One page operation of the Ultralight family that reached the reader.</summary>
    /// <param name="Method">The name of the reader method.</param>
    /// <param name="Access">The access as the reader received it, with the keys as hexadecimal text.</param>
    /// <param name="StartPage">The first page.</param>
    /// <param name="PageCount">The number of pages.</param>
    /// <param name="Data">A copy of the bytes of a write; <c>null</c> for a read or an authentication.</param>
    internal sealed record UltralightPageCall(string Method, UltralightAccess Access, int StartPage, int PageCount, byte[]? Data);

    /// <summary>
    /// A reader that records every call that carries a card key, and answers success to all of them. It never looks at
    /// hardware. Tests give each key slot of a task its own recognizable value, and map the recorded keys back to the
    /// slots, to say which slot a command uses for which call. It extends the fakes that already count authentications
    /// and file-settings reads, so those keep working.
    /// </summary>
    internal sealed class KeyRecordingReader : FakeFileSettingsProvider
    {
        /// <summary>Gets every call that carried a key, in the order it happened.</summary>
        public List<KeyCall> Calls { get; } = new List<KeyCall>();

        /// <summary>Gets or sets the method that answers an error instead of success (<see cref="FailWith"/>); <c>null</c> for none.</summary>
        public string? FailMethod { get; set; }

        /// <summary>Gets or sets what the failing method answers.</summary>
        public ERROR FailWith { get; set; } = ERROR.AuthFailure;

        /// <summary>Gets or sets a method that throws an exception whose text contains <see cref="ThrowText"/>; <c>null</c> for none.</summary>
        public string? ThrowMethod { get; set; }

        /// <summary>Gets or sets the text of the exception thrown by <see cref="ThrowMethod"/>.</summary>
        public string ThrowText { get; set; } = "The reader failed.";

        /// <summary>Gets or sets the position of a block of the sector that is read that no key could read (no data, not authenticated); <c>null</c> for none.</summary>
        public int? UnreadableBlock { get; set; }

        /// <summary>Gets or sets what the blocks of the sector that is read hold (every byte); 0 by default.</summary>
        public byte BlockFill { get; set; }

        /// <summary>Gets or sets how many blocks the sector that is read has (the trailer included); 4 by default. A reader that leaves out what it could not read returns fewer than the sector has.</summary>
        public int BlockCount { get; set; } = 4;

        /// <summary>Gets or sets the card the reader finds when the chip is read; <c>null</c> for none.</summary>
        public GenericChipModel? Card { get; set; }

        /// <summary>Gets or sets whether the reader says it is connected; <c>null</c> (the default) leaves it to <see cref="Card"/>.</summary>
        public bool? Connected { get; set; }

        /// <summary>Gets or sets whether the reader behaves like a PC/SC reader (see <see cref="ReaderDevice.IsPcscReader"/>).</summary>
        public bool Pcsc { get; set; }

        /// <summary>Gets or sets whether the reader can authenticate and change keys with keys that stay in a SAM (see <see cref="ReaderDevice.SupportsDesfireSamKeys"/>).</summary>
        public bool SupportsSam { get; set; }

        /// <summary>Gets or sets what the two operations with a SAM answer.</summary>
        public SamPiccResult SamAnswer { get; set; } = SamPiccResult.Ok;

        /// <summary>Gets or sets what happens while an operation with a SAM is under way (a person editing the dialog, for instance).</summary>
        public Action? WhileASamOperationRuns { get; set; }

        /// <summary>Gets the authentications with a key of the SAM that reached the reader: references, never keys.</summary>
        public List<DesfireSamAccess> SamAuthentications { get; } = new List<DesfireSamAccess>();

        /// <summary>Gets the key changes with keys of the SAM that reached the reader: references, never keys.</summary>
        public List<DesfireSamKeyChange> SamChanges { get; } = new List<DesfireSamKeyChange>();

        /// <inheritdoc />
        public override bool SupportsDesfireSamKeys => SupportsSam;

        /// <inheritdoc />
        public override Task<SamPiccResult> AuthenticateMifareDesfireWithSamAsync(DesfireSamAccess access)
        {
            SamAuthentications.Add(access);
            WhileASamOperationRuns?.Invoke();

            return Task.FromResult(SamAnswer);
        }

        /// <inheritdoc />
        public override Task<SamPiccResult> ChangeMifareDesfireKeyWithSamAsync(DesfireSamKeyChange change)
        {
            SamChanges.Add(change);
            WhileASamOperationRuns?.Invoke();

            return Task.FromResult(SamAnswer);
        }

        /// <inheritdoc />
        public override bool IsConnected => Connected ?? (Card != null || base.IsConnected);

        /// <inheritdoc />
        public override bool IsPcscReader => Pcsc;

        /// <summary>Gets how often the chip was read; a call that carries no key and so is not in <see cref="Calls"/>.</summary>
        public int ReadChipCalls { get; private set; }

        /// <inheritdoc />
        public override Task<ERROR> ReadChipPublic()
        {
            ReadChipCalls++;

            if (Card != null)
            {
                GenericChip = Card;
            }

            return base.ReadChipPublic();
        }

        /// <summary>Gets the calls of one method.</summary>
        /// <param name="method">The reader method.</param>
        /// <returns>The calls, in order.</returns>
        public IReadOnlyList<KeyCall> CallsOf(string method) => Calls.Where(call => call.Method == method).ToList();

        private static string Normalize(string? key) => key == null ? "<null>" : key.Replace(" ", string.Empty).ToUpperInvariant();

        private ERROR Record(string method, params string?[] keys)
        {
            Calls.Add(new KeyCall(method, keys.Select(Normalize).ToArray()));

            if (ThrowMethod == method)
            {
                throw new InvalidOperationException(ThrowText);
            }

            return FailMethod == method ? FailWith : ERROR.NoError;
        }

        // ── DESFire ──

        /// <inheritdoc />
        public override Task<ERROR> GetMiFareDESFireChipAppIDs(string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey)
        {
            // A card without applications; the view model reads the list after a successful call.
            DesfireChip = new MifareDesfireChipModel();

            return Task.FromResult(Record(nameof(GetMiFareDESFireChipAppIDs), _appMasterKey));
        }

        /// <inheritdoc />
        public override Task<ERROR> CreateMifareDesfireFile(
            string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey, FileType_MifareDesfireFileType _fileType,
            DESFireAccessRights _accessRights, EncryptionMode _encMode, int _appID, int _fileNo, int _fileSize,
            int _minValue = 0, int _maxValue = 1000, int _initValue = 0, bool _isValueLimited = false, int _maxNbOfRecords = 100) =>
            Task.FromResult(Record(nameof(CreateMifareDesfireFile), _appMasterKey));

        /// <inheritdoc />
        public override Task<ERROR> ReadMiFareDESFireChipFile(
            string _appReadKey, DESFireKeyType _keyTypeAppReadKey, int _readKeyNo, EncryptionMode _encMode, int _fileNo, int _appID, int _fileSize)
        {
            // The view model reads what a successful read leaves behind.
            MifareDESFireData = new byte[Math.Max(_fileSize, 0)];

            return Task.FromResult(Record(nameof(ReadMiFareDESFireChipFile), _appReadKey));
        }

        /// <inheritdoc />
        public override Task<ERROR> WriteMiFareDESFireChipFile(
            string _appWriteKey, DESFireKeyType _keyTypeAppWriteKey, int _writeKeyNo, EncryptionMode _encMode, int _fileNo, int _appID, byte[] _data) =>
            Task.FromResult(Record(nameof(WriteMiFareDESFireChipFile), _appWriteKey));

        /// <inheritdoc />
        public override async Task<ERROR> AuthToMifareDesfireApplication(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumber, int _appID)
        {
            var scripted = Record(nameof(AuthToMifareDesfireApplication), _applicationMasterKey);

            return scripted != ERROR.NoError
                ? scripted
                : await base.AuthToMifareDesfireApplication(_applicationMasterKey, _keyType, _keyNumber, _appID);
        }

        /// <inheritdoc />
        public override Task<ERROR> VerifyMifareDesfireKeyChange(string applicationKey, DESFireKeyType keyType, int keyNumber, int appId = 0)
        {
            var scripted = Record(nameof(VerifyMifareDesfireKeyChange), applicationKey);

            // The base class would authenticate through the overridden method above and record a second, different call.
            return scripted != ERROR.NoError
                ? Task.FromResult(scripted)
                : base.AuthToMifareDesfireApplication(applicationKey, keyType, keyNumber, appId);
        }

        /// <inheritdoc />
        public override Task<OperationResult> GetMifareDesfireAppSettings(
            string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent, int _appID, bool authenticateBeforeReading = true)
        {
            var scripted = Record(nameof(GetMifareDesfireAppSettings), _applicationMasterKey);

            return scripted != ERROR.NoError
                ? Task.FromResult(OperationResult.Success(scripted))
                : base.GetMifareDesfireAppSettings(_applicationMasterKey, _keyType, _keyNumberCurrent, _appID, authenticateBeforeReading);
        }

        /// <inheritdoc />
        public override Task<OperationResult> CreateMifareDesfireApplication(
            string _piccMasterKey, DESFireKeySettings _keySettingsTarget, DESFireKeyType _keyTypePiccMasterKey,
            DESFireKeyType _keyTypeTargetApplication, int _maxNbKeys, int _appID, bool authenticateToPICCFirst = true)
        {
            var method = authenticateToPICCFirst ? nameof(CreateMifareDesfireApplication) : nameof(CreateMifareDesfireApplication) + "WithoutAuth";

            return Task.FromResult(OperationResult.Success(Record(method, _piccMasterKey)));
        }

        /// <inheritdoc />
        public override Task<ERROR> ChangeMifareDesfireKeyAsync(
            uint appId, byte targetKeyNo, DESFireKeyType targetKeyType, string currentTargetKeyHex, string newTargetKeyHex,
            byte newTargetKeyVersion, string masterKeyHex, DESFireKeyType masterKeyType, DESFireKeySettings currentKeySettings) =>
            Task.FromResult(Record(nameof(ChangeMifareDesfireKeyAsync), currentTargetKeyHex, newTargetKeyHex, masterKeyHex));

        /// <inheritdoc />
        public override Task<ERROR> ChangeMifareDesfireApplicationKeySettings(
            string _applicationMasterKeyCurrent, int _keyNumberCurrent, DESFireKeyType _keyTypeCurrent, int _appIDCurrent, DESFireKeySettings keySettings) =>
            Task.FromResult(Record(nameof(ChangeMifareDesfireApplicationKeySettings), _applicationMasterKeyCurrent));

        /// <inheritdoc />
        public override Task<ERROR> ChangeMifareDesfireFileSettings(
            string changeKeyHex, DESFireKeyType changeKeyType, int changeKeyNo, DESFireAccessRights newAccessRights, EncryptionMode newEncMode,
            int appId = 0, int fileNo = 0) =>
            Task.FromResult(Record(nameof(ChangeMifareDesfireFileSettings), changeKeyHex));

        /// <inheritdoc />
        public override Task<ERROR> DeleteMifareDesfireApplication(
            string _applicationMasterKey, DESFireKeyType _keyType, uint _appID, DesfireDeleteAuthMethod _authMethod = DesfireDeleteAuthMethod.PiccMasterKey)
        {
            LastDeleteAuthMethod = _authMethod;

            return Task.FromResult(Record(nameof(DeleteMifareDesfireApplication), _applicationMasterKey));
        }

        /// <summary>Gets the method the last application delete was told to authenticate with; <c>null</c> before one.</summary>
        public DesfireDeleteAuthMethod? LastDeleteAuthMethod { get; private set; }

        /// <inheritdoc />
        public override Task<ERROR> DeleteMifareDesfireFile(string _applicationMasterKey, DESFireKeyType _keyType, int _appID = 0, int _fileID = 0) =>
            Task.FromResult(Record(nameof(DeleteMifareDesfireFile), _applicationMasterKey));

        /// <inheritdoc />
        public override Task<ERROR> FormatDesfireCard(string _applicationMasterKey, DESFireKeyType _keyType) =>
            Task.FromResult(Record(nameof(FormatDesfireCard), _applicationMasterKey));

        /// <inheritdoc />
        public override Task<ERROR> GetMifareDesfireFileList(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent = 0, int _appID = 0) =>
            Task.FromResult(Record(nameof(GetMifareDesfireFileList), _applicationMasterKey));

        /// <inheritdoc />
        public override Task<ERROR> GetMifareDesfireFileSettings(
            string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent = 0, int _appID = 0, int _fileNo = 0)
        {
            var scripted = Record(nameof(GetMifareDesfireFileSettings), _applicationMasterKey);

            return scripted != ERROR.NoError
                ? Task.FromResult(scripted)
                : base.GetMifareDesfireFileSettings(_applicationMasterKey, _keyType, _keyNumberCurrent, _appID, _fileNo);
        }

        /// <inheritdoc />
        public override Task<byte> MifareDesfire_GetKeyVersionAsync(byte keyNo) => Task.FromResult((byte)0);

        /// <inheritdoc />
        public override Task<ERROR> CommitTransactionAsync() => Task.FromResult(ERROR.NoError);

        // ── Ultralight ──

        /// <summary>Gets or sets whether the reader takes the access that a task asks about before it asks for keys.</summary>
        public bool AcceptsUltralightAccess { get; set; } = true;

        /// <summary>Gets what the task asked the reader about before it asked for keys, in order.</summary>
        public List<UltralightAccess> UltralightAccessesAsked { get; } = new List<UltralightAccess>();

        /// <inheritdoc />
        public override bool SupportsUltralightAccess(UltralightAccess access)
        {
            UltralightAccessesAsked.Add(access);

            return AcceptsUltralightAccess;
        }

        /// <summary>Gets every page operation of the Ultralight family that reached the reader, in order.</summary>
        public List<UltralightPageCall> UltralightCalls { get; } = new List<UltralightPageCall>();

        /// <summary>Gets or sets the result the page operations answer with instead of the default one; <c>null</c> for none.</summary>
        public UltralightResult? ScriptedUltralightResult { get; set; }

        /// <inheritdoc />
        public override Task<UltralightResult> ReadMifareUltralightPagesAsync(UltralightAccess access, int startPage, int pageCount) =>
            UltralightPageOperation(nameof(ReadMifareUltralightPagesAsync), access, startPage, pageCount, null);

        /// <inheritdoc />
        public override Task<UltralightResult> WriteMifareUltralightPagesAsync(UltralightAccess access, int startPage, byte[] data) =>
            UltralightPageOperation(nameof(WriteMifareUltralightPagesAsync), access, startPage, data == null ? 0 : data.Length / 4, data);

        /// <inheritdoc />
        public override Task<UltralightResult> AuthenticateMifareUltralightAsync(UltralightAccess access) =>
            UltralightPageOperation(nameof(AuthenticateMifareUltralightAsync), access, 0, 0, null);

        private Task<UltralightResult> UltralightPageOperation(string method, UltralightAccess access, int startPage, int pageCount, byte[]? data)
        {
            UltralightCalls.Add(new UltralightPageCall(method, access, startPage, pageCount, data == null ? null : (byte[])data.Clone()));

            // The key and the PACK as the reader received them, so that a test can say which slot each came from.
            var error = Record(method, access.KeyHex, access.PackHex);

            if (ScriptedUltralightResult != null)
            {
                return Task.FromResult(ScriptedUltralightResult);
            }

            if (error != ERROR.NoError)
            {
                return Task.FromResult(UltralightResult.Failed(error));
            }

            // What a successful read leaves: a recognizable byte per position, so that the pages can be told apart.
            var bytes = method == nameof(ReadMifareUltralightPagesAsync)
                ? Enumerable.Range(0, pageCount * 4).Select(i => (byte)(startPage * 4 + i)).ToArray()
                : null;

            return Task.FromResult(new UltralightResult(ERROR.NoError, bytes, UltralightVariant.Ntag215, access.Auth != UltralightAuth.None, pageCount, null));
        }

        // ── Classic ──

        /// <inheritdoc />
        public override Task<(ERROR Error, MifareClassicSectorModel Sector)> ReadMifareClassicSectorAsync(int sectorNumber, string aKey, string bKey)
        {
            var scripted = Record(nameof(ReadMifareClassicSectorAsync), aKey, bKey);

            // The view model reads the sector that a successful call hands back.
            return Task.FromResult((scripted, scripted == ERROR.NoError ? NewSector(sectorNumber) : new MifareClassicSectorModel()));
        }

        private MifareClassicSectorModel NewSector(int sectorNumber)
        {
            var sector = new MifareClassicSectorModel { SectorNumber = sectorNumber, IsAuthenticated = true };

            for (var block = 0; block < BlockCount; block++)
            {
                var unreadable = UnreadableBlock == block;
                var data = unreadable ? null : Enumerable.Repeat(BlockFill, 16).ToArray();

                sector.DataBlock.Add(new MifareClassicDataBlockModel(sectorNumber * 4 + block, block) { Data = data, IsAuthenticated = !unreadable });
            }

            return sector;
        }

        /// <inheritdoc />
        public override Task<ERROR> WriteMifareClassicSingleSector(int sectorNumber, string aKey, string bKey, byte[] buffer) =>
            Task.FromResult(Record(nameof(WriteMifareClassicSingleSector), aKey, bKey));

        /// <inheritdoc />
        public override Task<ERROR> WriteMifareClassicSingleBlock(int _blockNumber, string aKey, string bKey, byte[] buffer) =>
            Task.FromResult(Record(nameof(WriteMifareClassicSingleBlock), aKey, bKey));

        /// <inheritdoc />
        public override Task<ERROR> WriteMifareClassicWithMAD(
            int _madApplicationID, int _madStartSector, string _aKeyToUse, string _bKeyToUse, string _aKeyToWrite, string _bKeyToWrite,
            string _madAKeyToUse, string _madBKeyToUse, string _madAKeyToWrite, string _madBKeyToWrite,
            byte[] buffer, byte _madGPB, SectorAccessBits _sab, bool _useMADToAuth, bool _keyToWriteUseMAD) =>
            Task.FromResult(Record(
                nameof(WriteMifareClassicWithMAD),
                _aKeyToUse, _bKeyToUse, _aKeyToWrite, _bKeyToWrite, _madAKeyToUse, _madBKeyToUse, _madAKeyToWrite, _madBKeyToWrite));

        /// <inheritdoc />
        public override Task<ERROR> ReadMifareClassicWithMAD(
            int madApplicationID, string _aKeyToUse, string _bKeyToUse, string _madAKeyToUse, string _madBKeyToUse,
            int _length, byte _madGPB, bool _useMADToAuth, bool _aiToUseIsMAD)
        {
            var scripted = Record(nameof(ReadMifareClassicWithMAD), _aKeyToUse, _bKeyToUse, _madAKeyToUse, _madBKeyToUse);

            // The view model reads the data that a successful call leaves behind.
            if (scripted == ERROR.NoError)
            {
                MifareClassicData = new byte[Math.Max(_length, 0)];
            }

            return Task.FromResult(scripted);
        }
    }
}
