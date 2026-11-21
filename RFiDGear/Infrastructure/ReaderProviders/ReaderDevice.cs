using Elatec.NET.Cards.Mifare;
using LibLogicalAccess;
using RFiDGear.Infrastructure.AccessControl;
using RFiDGear.Infrastructure.Classic;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Infrastructure.Ultralight;
using RFiDGear.Models;
using RFiDGear.Services.Sam;
using Serilog;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RFiDGear.Infrastructure.ReaderProviders
{
    public abstract class ReaderDevice : IDisposable
    {
        public static ReaderDevice Instance
        {
            get
            {
                switch (Reader)
                {

                    case ReaderTypes.PCSC:
                        lock (syncRoot)
                        {
                            using (SettingsReaderWriter srw = new SettingsReaderWriter())
                            {
                                var settings = srw.DefaultSpecification;

                                if (instance == null)
                                {      
                                    instance = new LibLogicalAccessProvider(Reader, settings.DefaultReaderName, settings.SamReaderName);
                                    return instance;
                                }
                                else if (instance != null && !(instance is LibLogicalAccessProvider))
                                {
                                    instance = new LibLogicalAccessProvider(Reader, settings.DefaultReaderName, settings.SamReaderName);
                                    return instance;
                                }
                                else
                                {
                                    return instance;
                                }
                            }
                        }

                    case ReaderTypes.Elatec:
                        lock (syncRoot)
                        {
                            if (instance == null)
                            {
                                instance = new ElatecNetProvider();
                                return instance;
                            }
                            else if (instance != null && !(instance is ElatecNetProvider))
                            {
                                instance = new ElatecNetProvider();
                                return instance;
                            }
                            else
                            {
                                return instance;
                            }

                        }

                    case ReaderTypes.None:
                        return null;


                    default:
                        return null;
                }

            }
        }

        private static object syncRoot = new object();
        private static ReaderDevice instance;

        public abstract bool IsConnected { get; }

        /// <summary>
        /// Whether this is a PC/SC reader. Such a reader answers a read with a transport error when there is no card on
        /// it (and leaves the chip of the read before), and its <see cref="IsConnected"/> only means that a card session
        /// exists, not that a reader is present. The host's reader scan takes the result of a read accordingly.
        /// </summary>
        public virtual bool IsPcscReader => false;
        public static ReaderTypes Reader { get; set; }
        public static int PortNumber { get; set; }

        public MifareClassicSectorModel Sector { get; set; }
        public MifareClassicDataBlockModel DataBlock { get; set; }

        /// <summary>
        /// Makes the model that the dialogs show from a sector that was read: one entry per block of the sector, the trailer last; a block that
        /// no key could read has no data and is not authenticated.
        /// </summary>
        /// <param name="read">The sector that <see cref="ClassicOperations"/> read.</param>
        /// <returns>The model.</returns>
        protected static MifareClassicSectorModel ToSectorModel(ClassicSectorRead read)
        {
            ArgumentNullException.ThrowIfNull(read);

            var sector = new MifareClassicSectorModel { IsAuthenticated = read.IsAuthenticated };

            foreach (var block in read.Blocks)
            {
                sector.DataBlock.Add(new MifareClassicDataBlockModel(block.Block, block.BlockInSector)
                {
                    Data = block.Data,
                    IsAuthenticated = block.WasRead
                });
            }

            return sector;
        }

        public GenericChipModel GenericChip { get; set; }
        public MifareDesfireChipModel DesfireChip { get; set; }

        public ReaderTypes ReaderProvider { get; set; }
        public string ReaderUnitName { get; set; }
        public string ReaderUnitVersion { get; set; }

        /// <summary>
        /// Gets a short description of why the last <see cref="ConnectAsync"/> failed with an error from the reader stack,
        /// for example "ReaderException: Call was not successfull, error NotOpen" when the COM port is held by another
        /// program or another RFiDGear window. It is <see langword="null"/> when the last attempt succeeded, or found no
        /// reader attached. It says why the reader could not be opened, so it means something only while
        /// <see cref="IsConnected"/> is <see langword="false"/>.
        /// </summary>
        public string LastConnectError { get; protected set; }

        public byte[] MifareClassicData { get; set; }
        public bool DataBlockSuccessfullyAuth { get; set; }
        public bool SectorSuccessfullyAuth { get; set; }
        public byte[] MifareDESFireData { get; set; }
        public byte[] FileIDList { get; set; }
        public byte MaxNumberOfAppKeys { get; set; }
        public byte KeyVersion { get; set; }
        public DESFireKeyType EncryptionType { get; set; }
        public DESFireFileSettings DesfireFileSettings { get; set; }
        public AccessControl.DESFireKeySettings DesfireAppKeySetting { get; set; }

        #region Common

        // Common Method Definitions
        public abstract Task<ERROR> ConnectAsync();
        public abstract Task<ERROR> ReadChipPublic();

        #endregion

        #region MifareClassic
        // Mifare Classic Method Definitions
        /// <summary>Gets the gate that holds the Classic operations of this reader apart: a chip has one session, so one operation runs at a time.</summary>
        protected virtual SemaphoreSlim ClassicGate { get; } = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Gives the access to the chip on the reader for a Classic operation. The readers (and the tests) override it. The default is a
        /// reader that has none.
        /// </summary>
        /// <returns>The access, or <c>null</c> when there is no reader or no chip.</returns>
        protected virtual Task<IClassicPort> OpenClassicPortAsync() => Task.FromResult<IClassicPort>(null);

        /// <summary>
        /// Reads all blocks of a MIFARE Classic sector, the trailer included. It logs in with key A and then with key B (one attempt each) and
        /// reads every block with a key that the access bits of the sector allow. The result is in <see cref="Sector"/>: one entry per block, the
        /// trailer last; a block that no key could read has no data. The same code runs for every reader (see <see cref="ClassicOperations"/>).
        /// </summary>
        /// <param name="sectorNumber">The sector, 0 to 39, as the chip numbers it (the sectors 32 to 39 of a 4K have 16 blocks).</param>
        /// <param name="aKey">Key A as 12 hexadecimal digits, or empty for none.</param>
        /// <param name="bKey">Key B as 12 hexadecimal digits, or empty for none.</param>
        /// <returns>
        /// <see cref="ERROR.NoError"/> when a key logged in, <see cref="ERROR.AuthFailure"/> when none did (or a key is no key: nothing is sent
        /// to the chip then), <see cref="ERROR.TransportError"/> when there is no reader or chip, <see cref="ERROR.ProtocolConstraint"/> when the chip has no such sector.
        /// </returns>
        public virtual async Task<ERROR> ReadMifareClassicSingleSector(int sectorNumber, string aKey, string bKey)
        {
            return (await ReadClassicSectorAsync(sectorNumber, aKey, bKey, publish: true)).Error;
        }

        /// <summary>
        /// Reads all blocks of a MIFARE Classic sector like <see cref="ReadMifareClassicSingleSector(int, string, string)"/>, and hands the sector
        /// back with the answer. The sector is the one of this call and of no other: <see cref="Sector"/> is shared by every call on the reader, so
        /// a caller that reads it after an <c>await</c> may get the sector of another read.
        /// </summary>
        /// <param name="sectorNumber">The sector, 0 to 39, as the chip numbers it (the sectors 32 to 39 of a 4K have 16 blocks).</param>
        /// <param name="aKey">Key A as 12 hexadecimal digits, or empty for none.</param>
        /// <param name="bKey">Key B as 12 hexadecimal digits, or empty for none.</param>
        /// <returns>
        /// The answer (as of <see cref="ReadMifareClassicSingleSector(int, string, string)"/>) and the sector: one entry per block, the trailer
        /// last, and an empty sector when nothing could be read.
        /// </returns>
        public virtual async Task<(ERROR Error, MifareClassicSectorModel Sector)> ReadMifareClassicSectorAsync(int sectorNumber, string aKey, string bKey)
        {
            return await ReadClassicSectorAsync(sectorNumber, aKey, bKey, publish: false);
        }

        // The read behind both: the sector of the call is its own, and when it is to be published (the legacy property) it is set under the gate
        // only, so a read that waited or one that is refused before it starts cannot replace the sector of another read in the middle of it.
        private async Task<(ERROR Error, MifareClassicSectorModel Sector)> ReadClassicSectorAsync(int sectorNumber, string aKey, string bKey, bool publish)
        {
            var sector = new MifareClassicSectorModel();

            // A key that is no key never reaches the chip: it is a failed authentication, as it always was.
            var keysAreKeys = ClassicSectorKeys.TryCreate(aKey, bKey, out var keys);

            using (keys)
            {
                var error = await RunClassicAsync(
                    async operations =>
                    {
                        var read = await operations.ReadSectorAsync(sectorNumber, keys);

                        sector = ToSectorModel(read);

                        if (publish)
                        {
                            Sector = sector;
                        }

                        return read.Error;
                    },
                    () =>
                    {
                        if (publish)
                        {
                            Sector = sector;
                        }
                    },
                    keysAreKeys ? null : ERROR.AuthFailure);

                return (error, sector);
            }
        }

        /// <summary>
        /// Writes the data blocks of a MIFARE Classic sector in order (never the trailer, and not block 0), one attempt each and every block read
        /// back, with the key that the access bits of the sector let write each block. If one block cannot be written by a key that there is,
        /// none is written. The first block that fails ends the call.
        /// </summary>
        /// <param name="sectorNumber">The sector, 0 to 39, as the chip numbers it.</param>
        /// <param name="aKey">Key A as 12 hexadecimal digits, or empty for none.</param>
        /// <param name="bKey">Key B as 12 hexadecimal digits, or empty for none.</param>
        /// <param name="buffer">The data of the blocks: 48 bytes for a sector of 4 blocks (32 for sector 0), 240 for a sector of 16 blocks.</param>
        /// <returns>
        /// <see cref="ERROR.NoError"/> when all blocks were written and read back, <see cref="ERROR.ProtocolConstraint"/> for data of the wrong length
        /// or a sector that the chip does not have (nothing is sent), <see cref="ERROR.PermissionDenied"/> when no key that there is may write a block
        /// (nothing is written), <see cref="ERROR.AuthFailure"/> when no key logged in or the chip refused a write, <see cref="ERROR.TransportError"/>
        /// when the chip went away, and <see cref="ERROR.Unknown"/> when a block did not read back as it was written.
        /// </returns>
        public virtual async Task<ERROR> WriteMifareClassicSingleSector(int sectorNumber, string aKey, string bKey, byte[] buffer)
        {
            // The caller's array can change while the call waits (for the gate, for the reader): what is checked is what is written.
            var snapshot = (byte[])buffer?.Clone();

            // What needs no card is decided first: a request that cannot be carried out does not select the chip, whatever the keys are.
            if (!ClassicWritePolicy.CheckSector(sectorNumber, snapshot, ClassicWriteOptions.DataOnly).IsAllowed)
            {
                return ERROR.ProtocolConstraint;
            }

            if (!ClassicSectorKeys.TryCreate(aKey, bKey, out var keys))
            {
                return ERROR.AuthFailure;
            }

            using (keys)
            {
                return await RunClassicAsync(async operations =>
                    (await operations.WriteSectorAsync(sectorNumber, snapshot, keys, ClassicWriteOptions.DataOnly)).Error);
            }
        }

        /// <summary>
        /// Writes one block of a MIFARE Classic chip (one attempt, never repeated) and reads it back, with the key that the access bits of the
        /// sector let write it. A sector trailer may be written (its access bits must be consistent, and access bits that can never be changed
        /// again are refused); the manufacturer block (block 0) is not.
        /// </summary>
        /// <param name="_blockNumber">The block, as the chip numbers it (the block number of the chip, not the position in the sector).</param>
        /// <param name="aKey">Key A as 12 hexadecimal digits, or empty for none.</param>
        /// <param name="bKey">Key B as 12 hexadecimal digits, or empty for none.</param>
        /// <param name="buffer">The 16 bytes to write.</param>
        /// <returns>The same answers as <see cref="WriteMifareClassicSingleSector(int, string, string, byte[])"/>.</returns>
        public virtual async Task<ERROR> WriteMifareClassicSingleBlock(int _blockNumber, string aKey, string bKey, byte[] buffer)
        {
            var options = new ClassicWriteOptions { AllowTrailer = true };

            // The caller's array can change while the call waits (for the gate, for the reader): what is checked is what is written.
            var snapshot = (byte[])buffer?.Clone();

            // What needs no card is decided first: a request that cannot be carried out does not select the chip, whatever the keys are.
            if (!ClassicWritePolicy.CheckBlock(_blockNumber, snapshot, options).IsAllowed)
            {
                return ERROR.ProtocolConstraint;
            }

            if (!ClassicSectorKeys.TryCreate(aKey, bKey, out var keys))
            {
                return ERROR.AuthFailure;
            }

            using (keys)
            {
                return await RunClassicAsync(async operations =>
                    (await operations.WriteBlockAsync(_blockNumber, snapshot, keys, options)).Error);
            }
        }

        // One Classic operation: held apart from the others, on the chip that the reader finds, and never longer than the operation. A fault of the
        // reader stack that nobody expected is a transport error and not a refusal of the chip. What the call leaves in the reader (underTheGate)
        // is made under the gate and nowhere else; a call that is refused (refused) answers without the chip, but also under the gate.
        private async Task<ERROR> RunClassicAsync(Func<ClassicOperations, Task<ERROR>> work, Action underTheGate = null, ERROR? refused = null)
        {
            await ClassicGate.WaitAsync();

            try
            {
                underTheGate?.Invoke();

                if (refused != null)
                {
                    return refused.Value;
                }

                var port = await OpenClassicPortAsync();

                return port == null ? ERROR.TransportError : await work(new ClassicOperations(port));
            }
            catch (Exception e)
            {
                Log.ForContext<ReaderDevice>().Error(e, "A Classic operation failed.");

                return ERROR.TransportError;
            }
            finally
            {
                ClassicGate.Release();
            }
        }

        /// <summary>
        /// Writes an application through the MAD (the MIFARE Application Directory): the data exactly as given (what lies behind it in the last
        /// sector stays), the entry of the directory, and, when new keys or access bits are given, the trailers of the sectors of the application. A chip
        /// without a MAD gets one. The same code runs for every reader (see <see cref="ClassicMadOperations"/>); a refusal before the first block is
        /// written (no key, an AID of the administration, a sector that belongs to another application) writes nothing.
        /// </summary>
        /// <param name="_madApplicationID">The AID, 6 to 65535.</param>
        /// <param name="_madStartSector">The first sector of the application, or 0 for the sectors that the MAD already lists for the AID.</param>
        /// <param name="_aKeyToUse">Key A of the sectors of the application now.</param>
        /// <param name="_bKeyToUse">Key B of the sectors of the application now.</param>
        /// <param name="_aKeyToWrite">The key A that the sectors of the application are to get; empty or the same for no change.</param>
        /// <param name="_bKeyToWrite">The key B that the sectors of the application are to get; empty or the same for no change.</param>
        /// <param name="_madAKeyToUse">Key A of the MAD sectors now.</param>
        /// <param name="_madBKeyToUse">Key B of the MAD sectors now.</param>
        /// <param name="_madAKeyToWrite">The key A that the MAD sectors are to get; empty or the same for no change.</param>
        /// <param name="_madBKeyToWrite">The key B that the MAD sectors are to get; empty or the same for no change.</param>
        /// <param name="buffer">The data.</param>
        /// <param name="_madGPB">The general purpose byte of the MAD sectors when they are written (see <see cref="ClassicMadGpb"/>).</param>
        /// <param name="_sab">The access bits that the sectors of the application are to get; the default value (all zero) is none, and the card keeps its own. Bits that can never be changed again are refused.</param>
        /// <param name="_useMADToAuth">Not used any more: the MAD is always used.</param>
        /// <param name="_keyToWriteUseMAD">Whether the MAD is used; <c>false</c> is refused (a sector without the MAD is written with the sector write).</param>
        /// <returns>
        /// <see cref="ERROR.NoError"/> when everything was written and read back, <see cref="ERROR.ProtocolConstraint"/> for a refusal before the chip
        /// (see <see cref="ClassicMadStatus"/>), <see cref="ERROR.PermissionDenied"/>, <see cref="ERROR.AuthFailure"/>, <see cref="ERROR.TransportError"/> and
        /// <see cref="ERROR.Unknown"/> as for <see cref="WriteMifareClassicSingleBlock(int, string, string, byte[])"/>.
        /// </returns>
        public virtual async Task<ERROR> WriteMifareClassicWithMAD(int _madApplicationID, int _madStartSector,
                                               string _aKeyToUse, string _bKeyToUse, string _aKeyToWrite, string _bKeyToWrite,
                                               string _madAKeyToUse, string _madBKeyToUse, string _madAKeyToWrite, string _madBKeyToWrite,
                                               byte[] buffer, byte _madGPB, AccessControl.SectorAccessBits _sab, bool _useMADToAuth, bool _keyToWriteUseMAD)
        {
            if (!_keyToWriteUseMAD || _madApplicationID < 0 || _madApplicationID > ushort.MaxValue)
            {
                return ERROR.ProtocolConstraint;
            }

            var accessBits = ClassicAccessBitsOf(_sab, out var accessBitsValid);

            // Access bits that are no access bits are refused before any key is made.
            if (!accessBitsValid)
            {
                return ERROR.ProtocolConstraint;
            }

            // Keys that hold no key at all cannot log in to anything: they are no keys either.
            var applicationKeysOk = ClassicSectorKeys.TryCreate(_aKeyToUse, _bKeyToUse, out var current) && ClassicMadOperations.HasAKey(current);
            var madKeysOk = ClassicSectorKeys.TryCreate(_madAKeyToUse, _madBKeyToUse, out var madCurrent) && ClassicMadOperations.HasAKey(madCurrent);
            var applicationNewOk = NewKeysOf(_aKeyToUse, _bKeyToUse, _aKeyToWrite, _bKeyToWrite, out var applicationNew);
            var madNewOk = NewKeysOf(_madAKeyToUse, _madBKeyToUse, _madAKeyToWrite, _madBKeyToWrite, out var madNew);

            using (current)
            using (madCurrent)
            using (applicationNew)
            using (madNew)
            {
                // A key that is no key never reaches the chip.
                if (!applicationKeysOk || !madKeysOk || !applicationNewOk || !madNewOk)
                {
                    return ERROR.AuthFailure;
                }

                var request = new ClassicApplicationWrite
                {
                    Aid = (ushort)_madApplicationID,
                    StartSector = _madStartSector,
                    Data = (byte[])buffer?.Clone(),
                    ApplicationKeys = current,
                    MadKeys = madCurrent,
                    NewApplicationKeys = applicationNew,
                    NewMadKeys = madNew,
                    NewAccessBits = accessBits,
                    CreateMad = true,
                    MadGeneralPurposeByte = _madGPB
                };

                // What needs no card is decided first: a request that cannot be carried out does not select the chip.
                if (!ClassicMadOperations.IsPossible(request))
                {
                    return ERROR.ProtocolConstraint;
                }

                return await RunClassicAsync(async operations => (await new ClassicMadOperations(operations).WriteApplicationAsync(request)).Error);
            }
        }

        /// <summary>
        /// Reads the data of an application through the MAD: the sectors that the directory lists for the AID, their data blocks in order. The
        /// bytes are in <see cref="MifareClassicData"/>. The same code runs for every reader (see <see cref="ClassicMadOperations"/>).
        /// </summary>
        /// <param name="madApplicationID">The AID, 6 to 65535.</param>
        /// <param name="_aKeyToUse">Key A of the sectors of the application.</param>
        /// <param name="_bKeyToUse">Key B of the sectors of the application.</param>
        /// <param name="_madAKeyToUse">Key A of the MAD sectors.</param>
        /// <param name="_madBKeyToUse">Key B of the MAD sectors.</param>
        /// <param name="_length">How many bytes.</param>
        /// <param name="_madGPB">Not used: the MAD says itself which version it is.</param>
        /// <param name="_useMADToAuth">Not used any more: the MAD is always used.</param>
        /// <param name="_aiToUseIsMAD">Not used any more.</param>
        /// <returns>
        /// <see cref="ERROR.NoError"/> when the data was read, <see cref="ERROR.ProtocolConstraint"/> when the chip has no (usable) MAD, no sector of the AID,
        /// or fewer bytes than asked for (see <see cref="ClassicMadStatus"/>), <see cref="ERROR.AuthFailure"/> when no key logged in,
        /// <see cref="ERROR.PermissionDenied"/> when a block of the application cannot be read with the keys, and <see cref="ERROR.TransportError"/>.
        /// </returns>
        public virtual async Task<ERROR> ReadMifareClassicWithMAD(int madApplicationID, string _aKeyToUse, string _bKeyToUse,
                                                string _madAKeyToUse, string _madBKeyToUse, int _length, byte _madGPB,
                                                bool _useMADToAuth, bool _aiToUseIsMAD)
        {
            return (await ReadApplicationAsync(madApplicationID, _aKeyToUse, _bKeyToUse, _madAKeyToUse, _madBKeyToUse, _length, publish: true)).Error;
        }

        /// <summary>
        /// Reads the data of an application through the MAD like <see cref="ReadMifareClassicWithMAD(int, string, string, string, string, int, byte, bool, bool)"/>
        /// and hands the bytes back with the answer. <see cref="MifareClassicData"/> is shared by every call on the reader, so a caller that reads it
        /// after an <c>await</c> may get the bytes of another call.
        /// </summary>
        /// <param name="madApplicationID">The AID, 6 to 65535.</param>
        /// <param name="aKey">Key A of the sectors of the application.</param>
        /// <param name="bKey">Key B of the sectors of the application.</param>
        /// <param name="madAKey">Key A of the MAD sectors.</param>
        /// <param name="madBKey">Key B of the MAD sectors.</param>
        /// <param name="length">How many bytes.</param>
        /// <returns>The answer (see the other call), and the bytes: <c>null</c> when nothing was read.</returns>
        public virtual async Task<(ERROR Error, byte[] Data)> ReadMifareClassicApplicationAsync(int madApplicationID, string aKey, string bKey,
                                                string madAKey, string madBKey, int length)
        {
            return await ReadApplicationAsync(madApplicationID, aKey, bKey, madAKey, madBKey, length, publish: false);
        }

        // The read behind both. What needs no card is decided first (an AID that is no application, no bytes asked for, a key that is no key), and
        // the property of the reader is set under the gate only, so that a call that waited cannot replace the bytes of another one.
        private async Task<(ERROR Error, byte[] Data)> ReadApplicationAsync(int madApplicationID, string aKey, string bKey, string madAKey, string madBKey, int length, bool publish)
        {
            if (madApplicationID <= ClassicMadAid.NotApplicable || madApplicationID > ushort.MaxValue || length < 1)
            {
                return (ERROR.ProtocolConstraint, null);
            }

            // Keys that hold no key at all cannot log in to anything: they are no keys either.
            var applicationKeysOk = ClassicSectorKeys.TryCreate(aKey, bKey, out var current) && ClassicMadOperations.HasAKey(current);
            var madKeysOk = ClassicSectorKeys.TryCreate(madAKey, madBKey, out var madCurrent) && ClassicMadOperations.HasAKey(madCurrent);
            byte[] data = null;

            using (current)
            using (madCurrent)
            {
                if (!applicationKeysOk || !madKeysOk)
                {
                    return (ERROR.AuthFailure, null);
                }

                var error = await RunClassicAsync(
                    async operations =>
                    {
                        var read = await new ClassicMadOperations(operations).ReadApplicationAsync((ushort)madApplicationID, length, madCurrent, current);

                        data = read.Data;

                        if (publish)
                        {
                            MifareClassicData = data;
                        }

                        return read.Error;
                    },
                    () =>
                    {
                        if (publish)
                        {
                            MifareClassicData = null;
                        }
                    });

                return (error, data);
            }
        }

        // The keys that a call is to write: the new ones that are given and differ from the ones it has now. A new key that is no key makes the call
        // fail (false); no new key at all is a call without new keys (true, and no keys).
        private static bool NewKeysOf(string currentA, string currentB, string newA, string newB, out ClassicSectorKeys keys)
        {
            keys = null;

            if (!ClassicSectorKeys.TryCreate(newA, newB, out var parsed))
            {
                return false;
            }

            parsed.Dispose();

            var aDiffers = KeyDiffers(currentA, newA);
            var bDiffers = KeyDiffers(currentB, newB);

            // Only the keys that differ go in: a key that is not new is the current one.
            if ((aDiffers || bDiffers) && ClassicSectorKeys.TryCreate(aDiffers ? newA : null, bDiffers ? newB : null, out var onlyNew))
            {
                keys = onlyNew;
            }

            return true;
        }

        private static bool KeyDiffers(string current, string wanted)
        {
            if (string.IsNullOrWhiteSpace(wanted))
            {
                return false;
            }

            return !(ClassicKey.TryParse(current, out var currentBytes) && ClassicKey.TryParse(wanted, out var wantedBytes) && currentBytes.AsSpan().SequenceEqual(wantedBytes));
        }

        // The access bits that a dialog holds (a condition of 0 to 7 per group, as three bits); the default value is none, and none is no change.
        // A bit that is neither 0 nor 1 is no access bit: it is not valid, and it is never written as something else.
        private static ClassicAccessBits ClassicAccessBitsOf(AccessControl.SectorAccessBits sab, out bool valid)
        {
            var groups = new[] { sab.d_data_block0_access_bits, sab.d_data_block1_access_bits, sab.d_data_block2_access_bits, sab.d_sector_trailer_access_bits };
            var conditions = new int[groups.Length];

            valid = true;

            for (var i = 0; i < groups.Length; i++)
            {
                if (groups[i].c1 is < 0 or > 1 || groups[i].c2 is < 0 or > 1 || groups[i].c3 is < 0 or > 1)
                {
                    valid = false;

                    return null;
                }

                conditions[i] = (int)groups[i].c1 | ((int)groups[i].c2 << 1) | ((int)groups[i].c3 << 2);
            }

            return Array.TrueForAll(conditions, c => c == 0) ? null : new ClassicAccessBits(conditions[0], conditions[1], conditions[2], conditions[3]);
        }

        #endregion

        #region MifareUltralight

        /// <summary>
        /// Says, without a card, whether this reader can make a call with this access at all: the chip that is expected and the way of
        /// authenticating. A task asks before it asks a key provider for anything, so that a call that the reader refuses anyway never
        /// needs a key. The answer is no promise that the chip on the reader allows the call: the reader checks that again against the
        /// chip that it finds, and that check is the one that counts.
        /// </summary>
        /// <param name="access">
        /// What the call is about. It carries no keys at this point, and a reader must not need them to answer.
        /// </param>
        /// <returns><c>true</c> when the reader can make such a call; this default says so for every access.</returns>
        public virtual bool SupportsUltralightAccess(UltralightAccess access) => true;

        /// <summary>
        /// Reads pages of a chip of the Ultralight family (Ultralight, Ultralight C, Ultralight EV1, NTAG213/215/216). The reader finds
        /// out which chip is on it, checks it against what the task expects and the range against its layout, authenticates once if
        /// the access says so, and reads. It makes one attempt: nothing is retried, and a refusal ends the call.
        /// </summary>
        /// <param name="access">What the task expects, and how to authenticate.</param>
        /// <param name="startPage">The first page.</param>
        /// <param name="pageCount">The number of pages, at least 1.</param>
        /// <returns>
        /// The bytes read (4 per page) and the normalized error: <see cref="ERROR.ProtocolConstraint"/> when the call cannot be made
        /// to this chip or at all (another chip than expected, a page that the chip does not have, a reader without this operation),
        /// <see cref="ERROR.AuthFailure"/> for a key or password that the chip does not take, <see cref="ERROR.PermissionDenied"/> when
        /// the chip refuses the read, <see cref="ERROR.TransportError"/> when there is no chip or the reader fails.
        /// A reader that has no page operations answers <see cref="ERROR.ProtocolConstraint"/>, which is what this default does.
        /// </returns>
        public virtual Task<UltralightResult> ReadMifareUltralightPagesAsync(UltralightAccess access, int startPage, int pageCount) =>
            Task.FromResult(UltralightResult.Failed(ERROR.ProtocolConstraint));

        /// <summary>
        /// Writes whole pages of a chip of the Ultralight family, after the same checks as the read and after
        /// <see cref="UltralightWritePolicy"/> allowed the range (protected pages need the opt-in in the access and a named chip), and
        /// reads them back to check. It makes one attempt: a refused page ends the call, and nothing is written again.
        /// </summary>
        /// <param name="access">What the task expects, how to authenticate, and whether protected pages may be written.</param>
        /// <param name="startPage">The first page.</param>
        /// <param name="data">The bytes to write, a whole number of 4-byte pages.</param>
        /// <returns>
        /// The number of pages written and the normalized error, as for the read; a page that reads back different is
        /// <see cref="ERROR.Unknown"/> with <see cref="UltralightResult.FailedPage"/>. A reader that has no page operations answers
        /// <see cref="ERROR.ProtocolConstraint"/>, which is what this default does.
        /// </returns>
        public virtual Task<UltralightResult> WriteMifareUltralightPagesAsync(UltralightAccess access, int startPage, byte[] data) =>
            Task.FromResult(UltralightResult.Failed(ERROR.ProtocolConstraint));

        /// <summary>
        /// Authenticates to a chip of the Ultralight family and does nothing else. The access must name the chip and how to authenticate.
        /// It makes one attempt, so a wrong key or password is tried once.
        /// </summary>
        /// <param name="access">What the task expects, and how to authenticate.</param>
        /// <returns>
        /// The normalized error and whether the reader authenticated. A reader that has no page operations answers
        /// <see cref="ERROR.ProtocolConstraint"/>, which is what this default does.
        /// </returns>
        public virtual Task<UltralightResult> AuthenticateMifareUltralightAsync(UltralightAccess access) =>
            Task.FromResult(UltralightResult.Failed(ERROR.ProtocolConstraint));

        #endregion

        #region MifareDesfire with keys that stay in a SAM

        /// <summary>
        /// Says, without a card, whether this reader can authenticate a DESFire card, and change its keys, with keys that stay in a MIFARE SAM AV3.
        /// A task asks before it asks for any key, so that a reader that cannot do it ends the task before anything is sent. A PC/SC reader can
        /// when a reader for the SAM is set, and a TWN4 can when the SAM sits in one of its SAM sockets (hardware revision G).
        /// </summary>
        /// <returns><see langword="false"/> by default.</returns>
        public virtual bool SupportsDesfireSamKeys => false;

        /// <summary>
        /// Authenticates a key of a DESFire card with a key entry of the SAM and does nothing else. The SAM is opened for this call (it checks that
        /// the reader holds a MIFARE SAM AV3 before anything is sent to the card), the card is selected, the two parts of the authentication are
        /// relayed, and the SAM is let go. One attempt: nothing is repeated.
        /// </summary>
        /// <param name="access">Which key of the card, and which key of the SAM.</param>
        /// <returns>
        /// The normalized error, and why: <see cref="ERROR.KeyUnavailable"/> when the SAM cannot be used (none set, not there, in use),
        /// <see cref="ERROR.AuthFailure"/> when the card does not take the key, <see cref="ERROR.PermissionDenied"/> when it refuses the command,
        /// <see cref="ERROR.ProtocolConstraint"/> for a request that cannot be made or a reader that cannot do it, <see cref="ERROR.TransportError"/>
        /// when there is no card or a reader fails. This default does nothing and answers <see cref="ERROR.ProtocolConstraint"/>.
        /// </returns>
        public virtual Task<SamPiccResult> AuthenticateMifareDesfireWithSamAsync(DesfireSamAccess access) =>
            Task.FromResult(SamPiccResult.Refused(ERROR.ProtocolConstraint, SamPiccFailure.NotSupported));

        /// <summary>
        /// Changes a key of a DESFire card with keys of the SAM: authenticates, has the SAM build the cryptogram, sends it to the card and
        /// authenticates again with the new key. One attempt per step. <see cref="SamPiccResult.KeyMayHaveChanged"/> says when the card may hold the
        /// new key although the answer is not a success.
        /// </summary>
        /// <param name="change">Which key to change, and the keys of the SAM that do it.</param>
        /// <returns>The normalized error, as for <see cref="AuthenticateMifareDesfireWithSamAsync"/>. This default does nothing and answers <see cref="ERROR.ProtocolConstraint"/>.</returns>
        public virtual Task<SamPiccResult> ChangeMifareDesfireKeyWithSamAsync(DesfireSamKeyChange change) =>
            Task.FromResult(SamPiccResult.Refused(ERROR.ProtocolConstraint, SamPiccFailure.NotSupported));

        #endregion

        #region MifareDesfire

        /// <summary>
        /// Try to read the AppIds on a MIFARE DESFire chip. Try with and without authentication. If authentication fails, an empty list is returned.
        /// </summary>
        /// <param name="_appMasterKey">The PICC master key to try when authentication is required.</param>
        /// <param name="_keyTypeAppMasterKey">The key type for the PICC master key.</param>
        /// <returns cref="ERROR">Result of the operation.</returns>
        public abstract Task<ERROR> GetMiFareDESFireChipAppIDs(string _appMasterKey = "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00", DESFireKeyType _keyTypeAppMasterKey = DESFireKeyType.DF_KEY_DES);

        /// <summary>
        /// Creates a MIFARE DESFire file inside an application.
        /// </summary>
        /// <param name="_appMasterKey">The key 0 (app master key) of the application.</param>
        /// <param name="_keyTypeAppMasterKey">The app master key type (_appID != 0, keyNo = 0), used to authenticate: 3DES, 3K3DES, AES.</param>
        /// <param name="_fileType">The type of file to create.</param>
        /// <param name="_accessRights">Determines what key to use for read, write, and change operations.</param>
        /// <param name="_encMode">The communication mode (commSet) to use. File communication needs encryption: CM_PLAIN, CM_MAC, CM_ENCRYPT, CM_UNKNOWN.</param>
        /// <param name="_appID">The application that needs to be selected to create the file.</param>
        /// <param name="_fileNo">The file number to be created inside the application.</param>
        /// <param name="_fileSize">The size of the file to be created.</param>
        /// <param name="_minValue">Lower limit of a record file type.</param>
        /// <param name="_maxValue">Upper limit of a record file type.</param>
        /// <param name="_initValue">Initial value of a record file type.</param>
        /// <param name="_isValueLimited">Whether the value file enforces limits.</param>
        /// <param name="_maxNbOfRecords">Maximum number of records for record-based file types.</param>
        /// <returns cref="ERROR">Result of the operation.</returns>
        public abstract Task<ERROR> CreateMifareDesfireFile(string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey, FileType_MifareDesfireFileType _fileType, DESFireAccessRights _accessRights, EncryptionMode _encMode,
                                        int _appID, int _fileNo, int _fileSize,
                                        int _minValue = 0, int _maxValue = 1000, int _initValue = 0, bool _isValueLimited = false,
                                        int _maxNbOfRecords = 100);

        /// <summary>
        /// Reads a MIFARE DESFire file from the specified application using the provided application keys.
        /// </summary>
        /// <param name="_appReadKey">The application read key used to authenticate before reading.</param>
        /// <param name="_keyTypeAppReadKey">The key type for the application read key.</param>
        /// <param name="_readKeyNo">The key number for the application read key.</param>
        /// <param name="_appWriteKey">The application write key configured for the file.</param>
        /// <param name="_keyTypeAppWriteKey">The key type for the application write key.</param>
        /// <param name="_writeKeyNo">The key number for the application write key.</param>
        /// <param name="_encMode">The communication mode to use when reading the file.</param>
        /// <param name="_fileNo">The file number to read.</param>
        /// <param name="_appID">The application ID that owns the file.</param>
        /// <param name="_fileSize">The number of bytes to read from the file.</param>
        /// <returns cref="ERROR">Result of the operation.</returns>
        public abstract Task<ERROR> ReadMiFareDESFireChipFile(string _appReadKey, DESFireKeyType _keyTypeAppReadKey, int _readKeyNo,
                                        EncryptionMode _encMode,
                                        int _fileNo, int _appID, int _fileSize);

        /// <summary>
        /// Writes data to a MIFARE DESFire file in the specified application using the provided application keys.
        /// </summary>
        /// <param name="_appReadKey">The application read key configured for the file.</param>
        /// <param name="_keyTypeAppReadKey">The key type for the application read key.</param>
        /// <param name="_readKeyNo">The key number for the application read key.</param>
        /// <param name="_appWriteKey">The application write key used to authenticate before writing.</param>
        /// <param name="_keyTypeAppWriteKey">The key type for the application write key.</param>
        /// <param name="_writeKeyNo">The key number for the application write key.</param>
        /// <param name="_encMode">The communication mode to use when writing the file.</param>
        /// <param name="_fileNo">The file number to write.</param>
        /// <param name="_appID">The application ID that owns the file.</param>
        /// <param name="_data">The data to write to the file.</param>
        /// <returns cref="ERROR">Result of the operation.</returns>
        public abstract Task<ERROR> WriteMiFareDESFireChipFile(
                                        string _appWriteKey, DESFireKeyType _keyTypeAppWriteKey, int _writeKeyNo,
                                        EncryptionMode _encMode,
                                        int _fileNo, int _appID, byte[] _data);
        /// <summary>
        /// Try to authenticate to a MIFARE DESFire application. If _appID is omitted, it authenticates to _appId == 0 - the PICC level.
        /// </summary>
        /// <param name="_applicationMasterKey">The 16-bytes long key of the application.<</param>
        /// <param name="_keyType">The targeted app key type: 3DES, 3K3DES, AES.</param>
        /// <param name="_keyNumber">The Key Number used for Authentication. (KeyNo == 0 is the App's AppMasterKey / AMK)</param>
        /// <param name="_appID">The application ID for which authentication should be attempted.</param>
        /// <returns cref="ERROR">Result of the operation.</returns>
        public abstract Task<ERROR> AuthToMifareDesfireApplication(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumber, int _appID = 0);

        /// <summary>
        /// Verifies that a key change took effect by authenticating with the new key in a fresh session.
        /// The base implementation re-uses <see cref="AuthToMifareDesfireApplication"/> directly.
        /// Providers that keep an authenticated session alive across <c>ChangeKey</c> (e.g. ELATEC) must
        /// override this method and invalidate that session before the authoritative authentication.
        /// </summary>
        /// <param name="applicationKey">New key value to authenticate with.</param>
        /// <param name="keyType">Cryptographic type of the new key.</param>
        /// <param name="keyNumber">Changed key slot number.</param>
        /// <param name="appId">Application identifier, or 0 for PICC scope.</param>
        /// <returns>Result of the authoritative post-change authentication attempt.</returns>
        public virtual Task<ERROR> VerifyMifareDesfireKeyChange(string applicationKey, DESFireKeyType keyType, int keyNumber, int appId = 0) =>
            AuthToMifareDesfireApplication(applicationKey, keyType, keyNumber, appId);

        /// <summary>
        /// Tries to get the settings of a MIFARE DESFire application.
        /// </summary>
        /// <param name="_applicationMasterKey">The 16-bytes long key of the application.<</param>
        /// <param name="_keyType">The targeted app key type: 3DES, 3K3DES, AES.</param>
        /// <param name="_keyNumber">The Key Number used for Authentication. (KeyNo == 0 is the App's AppMasterKey / AMK)</param>
        /// <param name="_appID">The Application ID whose settings should be read</param>
        /// <param name="authenticateBeforeReading">Determines if authentication with keyNo = 0 should be performed before reading</param>
        /// <returns cref="ERROR">Result of the operation.</returns>
        public abstract Task<OperationResult> GetMifareDesfireAppSettings(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumber = 0, int _appID = 0, bool authenticateBeforeReading = true);

        /// <summary>
        /// Creates a new application, authenticating to the PICC first and trying to create the application anyway if authentication fails.
        /// </summary>
        /// <param name="_piccMasterKey">The 16 byte PICC master key, used to authenticate.</param>
        /// <param name="_keySettingsTarget">
        /// Key settings byte: KS_CHANGE_KEY_WITH_MK = 0, KS_ALLOW_CHANGE_MK = 1, KS_FREE_LISTING_WITHOUT_MK = 2,
        /// KS_FREE_CREATE_DELETE_WITHOUT_MK = 4, KS_CONFIGURATION_CHANGEABLE = 8, KS_DEFAULT = 11,
        /// KS_CHANGE_KEY_WITH_TARGETED_KEYNO = 224, KS_CHANGE_KEY_FROZEN = 240.
        /// </param>
        /// <param name="_keyTypePiccMasterKey">The PICC master key type used to authenticate: 3DES, 3K3DES, AES.</param>
        /// <param name="_keyTypeTargetApplication">The targeted app key type: 3DES, 3K3DES, AES.</param>
        /// <param name="_maxNbKeys">Number of keys created in the application.</param>
        /// <param name="_appID">Application ID for the new app.</param>
        /// <param name="authenticateToPICCFirst">Should authentication be omitted or performed?</param>
        /// <returns>The operation result, including metadata about authentication and app creation.</returns>
        public abstract Task<OperationResult> CreateMifareDesfireApplication(string _piccMasterKey, AccessControl.DESFireKeySettings _keySettingsTarget, DESFireKeyType _keyTypePiccMasterKey, DESFireKeyType _keyTypeTargetApplication, int _maxNbKeys, int _appID, bool authenticateToPICCFirst = true);

        /// <summary>
        /// Changes a MIFARE DESFire key on the currently presented card.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method supports both PICC-level keys (<paramref name="appId"/> = 0) and application-level keys
        /// (<paramref name="appId"/> &gt; 0).
        /// </para>
        /// <para>
        /// The implementation will authenticate before changing the key. The authentication key number is chosen
        /// from <paramref name="keySettings"/> (your configured "change-key policy"):
        /// </para>
        /// <list type="bullet">
        ///   <item>
        ///     <description>
        ///     If <paramref name="keySettings"/> indicates "change key with targeted key number", the provider authenticates
        ///     with <paramref name="targetKeyNo"/> using <paramref name="currentTargetKeyHex"/>.
        ///     </description>
        ///   </item>
        ///   <item>
        ///     <description>
        ///     Otherwise the provider authenticates with key 0 (master key) using <paramref name="masterKeyHex"/>.
        ///     </description>
        ///   </item>
        /// </list>
        /// <para>
        /// This method performs <b>key changes only</b>. It does not alter key settings, key type, or number-of-keys.
        /// Some reader APIs (not the card itself) require additional context values (e.g., number of keys) when issuing
        /// a key change; providers should obtain those values from the card when possible and only fall back to configured values.
        /// </para>
        /// </remarks>
        /// <param name="appId">
        /// The DESFire application identifier to select before changing the key.
        /// Use <c>0</c> to target PICC-level keys; use a non-zero AID to target an application.
        /// </param>
        /// <param name="targetKeyNo">
        /// The key number to be changed within the selected scope (PICC or application).
        /// </param>
        /// <param name="targetKeyType">
        /// The cryptographic type of the <paramref name="targetKeyNo"/> slot (e.g., DES/3DES/AES as represented by your enum).
        /// Providers may use this type for validation, authentication (when policy requires self-key auth), and for passing
        /// type metadata to reader APIs.
        /// </param>
        /// <param name="currentTargetKeyHex">
        /// The current value (old value) of the target key slot, as a hex string.
        /// This value is required by some providers to perform the change-key operation and is also used for authentication
        /// when <paramref name="keySettings"/> requires authenticating with the targeted key number.
        /// </param>
        /// <param name="newTargetKeyHex">
        /// The desired new key value to write into the target key slot, as a hex string.
        /// </param>
        /// <param name="newTargetKeyVersion">
        /// Version byte to associate with the new key value.
        /// Some providers use this directly; others may ignore it if their API does not expose versioning for the change-key call.
        /// </param>
        /// <param name="masterKeyHex">
        /// The master key value (key 0) for the selected scope (PICC master key when <paramref name="appId"/> is 0,
        /// application master key when <paramref name="appId"/> &gt; 0).
        /// This is used when <paramref name="currentKeySettings"/> requires authenticating with key 0 (master key).
        /// </param>
        /// <param name="masterKeyType">
        /// Cryptographic type of the master key value in <paramref name="masterKeyHex"/>.
        /// </param>
        /// <param name="currentKeySettings">
        /// Current key settings of the selected PICC or application. They determine which key must authorize
        /// ChangeKey and may be required by a provider to construct the command.
        /// This is context only: implementations must not modify key settings here. Key settings are changed
        /// exclusively through <see cref="ChangeMifareDesfireApplicationKeySettings"/>.
        /// </param>
        /// <returns>
        /// <see cref="ERROR.NoError"/> on success; otherwise an <see cref="ERROR"/> describing the failure.
        /// </returns>
        public abstract Task<ERROR> ChangeMifareDesfireKeyAsync(
            uint appId,
            byte targetKeyNo,
            DESFireKeyType targetKeyType,
            string currentTargetKeyHex,
            string newTargetKeyHex,
            byte newTargetKeyVersion,
            string masterKeyHex,
            DESFireKeyType masterKeyType,
            AccessControl.DESFireKeySettings currentKeySettings);

        /// <summary>
        /// Updates DESFire key settings without changing key material. Implementations must authenticate with key 0 of
        /// the selected PICC or application before applying <paramref name="keySettings"/>.
        /// </summary>
        /// <param name="_applicationMasterKeyCurrent">Master key used to authenticate against the selected Apps, KeyNo 0</param>
        /// <param name="_keyNumberCurrent">Ignored for settings updates; key 0 is always used.</param>
        /// <param name="_keyTypeCurrent">Cryptographic type of the current master key.</param>
        /// <param name="selectedDesfireAppKeyVersionTargetAsIntint">Unused placeholder for compatibility.</param>
        /// <param name="_keyTypeTarget">Desired key type metadata to submit alongside the settings byte.</param>
        /// <param name="_appIDCurrent">Application identifier to select (0 targets the PICC level).</param>
        /// <param name="keySettings">Key settings byte to apply.</param>
        /// <returns>Status of the key settings update.</returns>
        public abstract Task<ERROR> ChangeMifareDesfireApplicationKeySettings(string _applicationMasterKeyCurrent, int _keyNumberCurrent, DESFireKeyType _keyTypeCurrent,
            int _appIDCurrent, AccessControl.DESFireKeySettings keySettings);

        /// <summary>
        /// Changes the communication mode and access rights of an existing DESFire file (command 0x5F).
        /// Implementations must authenticate to the target application with the specified key before
        /// issuing the ChangeFileSettings command.
        /// </summary>
        /// <param name="changeKeyHex">Hex-encoded key used to authenticate to the application (must have ChangeKey rights on the file).</param>
        /// <param name="changeKeyType">Cryptographic algorithm of the authenticating key (DES, 3DES, 3K3DES, AES).</param>
        /// <param name="changeKeyNo">Key slot (0–13) within the application used for authentication.</param>
        /// <param name="newAccessRights">Desired read/write/read-write/change-key access rights to apply to the file.</param>
        /// <param name="newEncMode">Desired communication mode for the file (plain, MAC'd, or fully encrypted).</param>
        /// <param name="appId">Application identifier that owns the file; 0 selects the PICC level.</param>
        /// <param name="fileNo">File number within the application to reconfigure.</param>
        /// <returns>
        /// <see cref="ERROR.NoError"/> on success;
        /// <see cref="ERROR.AuthFailure"/> when authentication or key pre-fetch fails;
        /// <see cref="ERROR.TransportError"/> on reader communication failure.
        /// </returns>
        public abstract Task<ERROR> ChangeMifareDesfireFileSettings(string changeKeyHex, DESFireKeyType changeKeyType, int changeKeyNo, DESFireAccessRights newAccessRights, EncryptionMode newEncMode, int appId = 0, int fileNo = 0);

        /// <summary>
        /// Deletes a MIFARE DESFire application after authenticating to PICC Lvl (AppID = 0, KeyNo = 0).
        /// </summary>
        /// <param name="_applicationMasterKey">The master key used to authenticate before deletion.</param>
        /// <param name="_keyType">Cryptographic type of the master key: 3DES, 3K3DES, AES.</param>
        /// <param name="_appID">The AID to delete.</param>
        /// <param name="_authMethod">
        /// Whether to authenticate at PICC level (AID=0x00) or at the target application level (AID=<paramref name="_appID"/>).
        /// Defaults to <see cref="DesfireDeleteAuthMethod.PiccMasterKey"/> for backward compatibility.
        /// </param>
        /// <returns></returns>
        public abstract Task<ERROR> DeleteMifareDesfireApplication(string _applicationMasterKey, DESFireKeyType _keyType, uint _appID, DesfireDeleteAuthMethod _authMethod = DesfireDeleteAuthMethod.PiccMasterKey);

        /// <summary>
        /// Deletes a MIFARE DESFire file inside an application after authenticating to the application.
        /// </summary>
        /// <param name="_applicationMasterKey">Master key used to authenticate against the selected Apps, KeyNo 0</param>
        /// <param name="_keyType">Cryptographic type of the current master key. (byte): 3DES, 3K3DES, AES</param>
        /// <param name="_appID">The app ID that contains the file to be deleted.</param>
        /// <param name="_fileID">The ID of the File that should be deleted.</param>
        /// <returns></returns>
        public abstract Task<ERROR> DeleteMifareDesfireFile(string _applicationMasterKey, DESFireKeyType _keyType, int _appID = 0, int _fileID = 0);

        /// <summary>
        /// Authenticates to and formats a Desfire Card
        /// </summary>
        /// <param name="_applicationMasterKey">The PICC MasterKey</param>
        /// <param name="_keyType">The PICC MasterKey Type (byte): 3DES, 3K3DES, AES</param>
        /// <returns cref="ERROR">Result of the operation.</returns>
        public abstract Task<ERROR> FormatDesfireCard(string _applicationMasterKey, DESFireKeyType _keyType);

        /// <summary>
        /// Gets the file list of a MIFARE DESFire application.
        /// </summary>
        /// <param name="_applicationMasterKey"></param>
        /// <param name="_keyType"></param>
        /// <param name="_keyNumberCurrent"></param>
        /// <param name="_appID"></param>
        /// <returns></returns>
        public abstract Task<ERROR> GetMifareDesfireFileList(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent = 0, int _appID = 0);

        /// <summary>
        /// Gets the file settings of a MIFARE DESFire file inside an application.
        /// </summary>
        /// <param name="_applicationMasterKey"></param>
        /// <param name="_keyType"></param>
        /// <param name="_keyNumberCurrent"></param>
        /// <param name="_appID"></param>
        /// <param name="_fileNo"></param>
        /// <returns></returns>
        public abstract Task<ERROR> GetMifareDesfireFileSettings(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent = 0, int _appID = 0, int _fileNo = 0);

        /// <summary>
        /// Commits a DESFire transaction for FileType BACKUP, VALUE and RECORD.
        /// </summary>
        /// <returns></returns>
        public abstract Task<ERROR> CommitTransactionAsync();

        /// <summary>
        /// Get the key version of a DESFire key.
        /// </summary>
        /// <param name="keyNo">Key number</param>
        /// <returns>Key version (byte)</returns>
        /// <exception cref="ReaderException"></exception>
        public abstract Task<byte> MifareDesfire_GetKeyVersionAsync(byte keyNo);

        #endregion

        protected abstract void Dispose(bool disposing);
        public abstract void Dispose();
    }
}
