using ByteArrayHelper.Extensions;
using Elatec.NET;
using Elatec.NET.Cards.Mifare;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Infrastructure.Ultralight;
using RFiDGear.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using static LibLogicalAccess.Card.DESFireCommands;

namespace RFiDGear.Infrastructure.ReaderProviders
{
    public class ElatecNetProvider : ReaderDevice, IDisposable
    {
        private TWN4ReaderDevice readerDevice;
        private readonly byte DESFIRE_AUTHMODE_COMPATIBLE = 0;
        private readonly byte DESFIRE_AUTHMODE_EV1 = 1;

        private GenericChipModel hfTag;
        private GenericChipModel lfTag;
        private GenericChipModel legicTag;

        private bool _disposed;
        private readonly SemaphoreSlim _comPortLock = new SemaphoreSlim(1, 1);

        // The monitor retries a connect every few seconds; the log gets one line per distinct failure, not one per retry.
        private string lastLoggedConnectFailure;

        private bool IsMultiTechReader => ReaderUnitVersion?.Contains("CCF") == true;

        #region Constructor

        private async Task Initialize()
        {
            GenericChip ??= new GenericChipModel();

            try
            {
                var readerList = EnumerateReaders().ToList();

                if (readerList != null && readerList.Count > 0)
                {
                    readerDevice = readerList.FirstOrDefault();

                    if (readerDevice.IsConnected)
                    {
                        ReaderUnitVersion = await readerDevice.GetVersionStringAsync();
                    }
                    else if (readerDevice != null && readerDevice.AvailableReadersCount >= 1 && !readerDevice.IsConnected)
                    {
                        if (await readerDevice.ConnectAsync())
                        {
                            ReaderUnitVersion = await readerDevice.GetVersionStringAsync();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LastConnectError = DescribeConnectFailure(e);
                Log.ForContext<ElatecNetProvider>().Error(e, "Elatec operation failed.");
            }
        }

        public ElatecNetProvider()
        {
            GenericChip ??= new GenericChipModel();
        }

        #endregion

        #region Common

        /// <summary>
        /// 
        /// </summary>
        public override bool IsConnected => readerDevice?.IsConnected == true;

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async override Task<ERROR> ReadChipPublic()
        {
            await _comPortLock.WaitAsync();
            try
            {
            try
            {
                if (readerDevice == null)
                {
                    await Initialize();
                }

                if (readerDevice != null)
                {

                    if (!readerDevice.IsConnected)
                    {
                        await readerDevice.ConnectAsync();
                    }

                    await readerDevice.SetTagTypesAsync(LFTagTypes.NOTAG, HFTagTypes.AllHFTags & ~HFTagTypes.LEGIC);
                    var tmpTag = await readerDevice.GetSingleChipAsync();

                    if (tmpTag != null && tmpTag.ChipType == ChipType.MIFARE)
                    {
                        if ((MifareChipSubType)((byte)(tmpTag as MifareChip).SubType & 0xF0) == MifareChipSubType.MifareClassic)
                        {
                            var subType = (tmpTag as MifareChip).SubType;

                            if (subType == MifareChipSubType.SmartMX_Mifare_1K ||
                                subType == MifareChipSubType.SmartMX_Mifare_2K ||
                                subType == MifareChipSubType.SmartMX_Mifare_4K)
                            {
                                var versionL4 = await TryDetectSmartMXDesFireEmulationAsync((tmpTag as MifareChip).UID);

                                if (versionL4 != null)
                                {
                                    hfTag = new MifareDesfireChipModel(
                                        tmpTag.UIDHexString,
                                        MapSmartMXVersionToCardType(versionL4),
                                        ByteArrayConverter.GetStringFrom((tmpTag as MifareChip).SAK),
                                        ByteArrayConverter.GetStringFrom((tmpTag as MifareChip).ATS),
                                        ByteArrayConverter.GetStringFrom(versionL4));
                                }
                                else
                                {
                                    hfTag = new MifareClassicChipModel(tmpTag.UIDHexString, (CARD_TYPE)((short)subType << 8));
                                }
                            }
                            else
                            {
                                hfTag = new MifareClassicChipModel(tmpTag.UIDHexString, (CARD_TYPE)((short)(tmpTag as MifareChip).SubType << 8));
                            }
                        }
                        else if ((MifareChipSubType)((byte)(tmpTag as MifareChip).SubType & 0x40) == MifareChipSubType.DESFire)
                        {
                            hfTag = new MifareDesfireChipModel(tmpTag.UIDHexString, (CARD_TYPE)((short)(tmpTag as MifareChip).SubType << 8),
                                    ByteArrayConverter.GetStringFrom((tmpTag as MifareChip).SAK),
                                    ByteArrayConverter.GetStringFrom((tmpTag as MifareChip).ATS),
                                    ByteArrayConverter.GetStringFrom((tmpTag as MifareChip).VersionL4));
                        }
                        else
                        {
                            hfTag = new GenericChipModel(tmpTag.UIDHexString, (CARD_TYPE)((short)(tmpTag as MifareChip).SubType << 8));
                        }
                    }
                    else
                    {
                        // No MIFARE chip: none at all, or one of a kind this application has no model for. Either way the
                        // tag of the read before is gone and must not be shown (nor get the LF and LEGIC children again).
                        hfTag = null;
                    }

                    await readerDevice.SetTagTypesAsync(LFTagTypes.AllLFTags, HFTagTypes.NOTAG);
                    tmpTag = await readerDevice.GetSingleChipAsync();
                    lfTag = tmpTag != null ? new GenericChipModel(tmpTag.UIDHexString, (CARD_TYPE)tmpTag.ChipType) : null;

                    await readerDevice.SetTagTypesAsync(LFTagTypes.NOTAG, HFTagTypes.LEGIC);
                    tmpTag = await readerDevice.GetSingleChipAsync();
                    legicTag = tmpTag != null ? new GenericChipModel(tmpTag.UIDHexString, (CARD_TYPE)tmpTag.ChipType) : null;

                    await readerDevice.SetTagTypesAsync(LFTagTypes.NOTAG, HFTagTypes.AllHFTags);

                    if (!string.IsNullOrWhiteSpace(hfTag?.UID) && GenericChip.UID == hfTag.UID)
                    {
                        return ERROR.NoError;
                    }

                    if (!string.IsNullOrWhiteSpace(hfTag?.UID) && !(GenericChip.UID == hfTag.UID))
                    {
                        if (!string.IsNullOrWhiteSpace(hfTag?.UID))
                        {
                            GenericChip = hfTag;
                        }
                    }

                    if (hfTag != null && !string.IsNullOrEmpty(lfTag?.UID))
                    {
                        if (GenericChip.Childs == null)
                        {
                            GenericChip.Childs = new List<GenericChipModel>();
                        }

                        GenericChip.Childs.Add(lfTag);
                    }

                    else if (!string.IsNullOrEmpty(lfTag?.UID))
                    {
                        GenericChip = lfTag;
                    }

                    if (hfTag != null && !string.IsNullOrEmpty(legicTag?.UID))
                    {
                        if (GenericChip.Childs == null)
                        {
                            GenericChip.Childs = new List<GenericChipModel>();
                        }

                        GenericChip.Childs.Add(legicTag);
                    }

                    else if (!string.IsNullOrEmpty(legicTag?.UID))
                    {
                        GenericChip = legicTag;
                    }
                }

                if (hfTag == null && lfTag == null && legicTag == null)
                {
                    GenericChip = new GenericChipModel();
                }

                return ERROR.NoError;
            }
            catch (Exception e)
            {
                Log.ForContext<ElatecNetProvider>().Error(e, "Elatec operation failed.");

                return ERROR.TransportError;
            }
            }
            finally
            {
                _comPortLock.Release();
            }
        }


        /// <summary>
        /// Lists the readers that are attached. A seam for tests, which must not enumerate the machine's real readers.
        /// </summary>
        /// <returns>The attached TWN4 readers.</returns>
        protected virtual IEnumerable<TWN4ReaderDevice> EnumerateReaders() => TWN4ReaderDevice.Instance;

        /// <summary>
        /// Opens the first attached reader and reads its version. An error from the reader stack (the COM port is held by
        /// another program, the reader was pulled while it was being opened) is reported as
        /// <see cref="ERROR.TransportError"/> with the reason in <see cref="ReaderDevice.LastConnectError"/>; nothing is
        /// thrown. It makes one attempt and never retries: the caller decides when to try again.
        /// </summary>
        /// <returns>
        /// <see cref="ERROR.NoError"/> when the reader is open and answered, <see cref="ERROR.TransportError"/> when no
        /// reader is attached or it could not be opened or queried.
        /// </returns>
        public async override Task<ERROR> ConnectAsync()
        {
            LastConnectError = null;

            try
            {
                readerDevice = EnumerateReaders().FirstOrDefault();

                if (readerDevice == null)
                {
                    return ERROR.TransportError;
                }

                // If the SDK device is already connected (e.g. left open by the settings dialog),
                // skip ConnectAsync and just fetch the version.
                if (!readerDevice.IsConnected && !await readerDevice.ConnectAsync())
                {
                    return ERROR.TransportError;
                }

                ReaderUnitVersion = await readerDevice.GetVersionStringAsync();
                lastLoggedConnectFailure = null;

                return ERROR.NoError;
            }
            catch (Exception e)
            {
                LastConnectError = DescribeConnectFailure(e);

                if (!string.Equals(LastConnectError, lastLoggedConnectFailure, StringComparison.Ordinal))
                {
                    lastLoggedConnectFailure = LastConnectError;
                    Log.ForContext<ElatecNetProvider>().Warning(e, "The Elatec reader could not be connected: {Reason}", LastConnectError);
                }

                return ERROR.TransportError;
            }
        }

        /// <summary>
        /// Describes a failure to connect in a few words that carry no key, UID or port name.
        /// </summary>
        /// <param name="e">The exception.</param>
        /// <returns>For example "ReaderException: Call was not successfull, error NotOpen".</returns>
        private static string DescribeConnectFailure(Exception e)
        {
            if (e is not ReaderException readerException)
            {
                return e.GetType().Name;
            }

            // The errors Elatec.NET raises while it opens a port name themselves in the message and leave ErrorNumber at
            // None; the message carries no port, key or UID.
            var detail = readerException.ErrorNumber != ReaderError.None
                ? readerException.ErrorNumber.ToString()
                : readerException.Message;

            return "ReaderException: " + detail;
        }

        #endregion

        #region MifareClassic

        /// <inheritdoc />
        public async override Task<ERROR> WriteMifareClassicSingleBlock(int _blockNumber, string aKey, string bKey, byte[] buffer)
        {
            await _comPortLock.WaitAsync();
            try
            {
                try
                {
                    await readerDevice.MifareClassic_LoginAsync(aKey, 0, (byte)CustomConverter.GetSectorNumberFromChipBasedDataBlockNumber(_blockNumber));
                }
                catch
                {
                    try
                    {
                        await readerDevice.MifareClassic_LoginAsync(bKey, 1, (byte)CustomConverter.GetSectorNumberFromChipBasedDataBlockNumber(_blockNumber));
                    }
                    catch
                    {
                        try
                        {
                            await readerDevice.MifareClassic_WriteBlockAsync(buffer, (byte)_blockNumber);
                        }
                        catch
                        {
                            return ERROR.AuthFailure;
                        }
                    }
                } // Login
                return ERROR.NoError;
            }
            finally
            {
                _comPortLock.Release();
            }
        }

        /// <inheritdoc />
        public async override Task<ERROR> ReadMifareClassicSingleSector(int sectorNumber, string aKey, string bKey)
        {
            await _comPortLock.WaitAsync();
            try
            {
                return await ReadWriteAccessOnClassicSector(sectorNumber, aKey, bKey, null);
            }
            finally
            {
                _comPortLock.Release();
            }
        }

        /// <inheritdoc />
        public async override Task<ERROR> WriteMifareClassicSingleSector(int sectorNumber, string aKey, string bKey, byte[] buffer)
        {
            await _comPortLock.WaitAsync();
            try
            {
                return await ReadWriteAccessOnClassicSector(sectorNumber, aKey, bKey, buffer);
            }
            finally
            {
                _comPortLock.Release();
            }
        }


        public override Task<ERROR> WriteMifareClassicWithMAD(int _madApplicationID, int _madStartSector,
            string _aKeyToUse, string _bKeyToUse, string _aKeyToWrite, string _bKeyToWrite,
            string _madAKeyToUse, string _madBKeyToUse, string _madAKeyToWrite,
            string _madBKeyToWrite, byte[] buffer, byte _madGPB, AccessControl.SectorAccessBits _sab,
            bool _useMADToAuth, bool _keyToWriteUseMAD)
        {
            throw new NotImplementedException();
        }

        public override Task<ERROR> ReadMifareClassicWithMAD(int madApplicationID, string _aKeyToUse,
            string _bKeyToUse, string _madAKeyToUse, string _madBKeyToUse, int _length, byte _madGPB,
            bool _useMADToAuth, bool _aiToUseIsMAD)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sectorNumber"></param>
        /// <param name="aKey"></param>
        /// <param name="bKey"></param>
        /// <param name="buffer"></param>
        /// <returns></returns>
        private async Task<ERROR> ReadWriteAccessOnClassicSector(int sectorNumber, string aKey, string bKey, byte[] buffer)
        {
            if (!readerDevice.IsConnected)
            {
                return ERROR.TransportError;
            }

            try { await readerDevice.SearchTagAsync(); } catch { }

            Sector = new MifareClassicSectorModel();

            var elatecSpecificSectorNumber = sectorNumber > 31 ? (sectorNumber - 32) * 4 + 32 : sectorNumber; // elatec uses special sectornumbers

            for (byte k = 0; k < (sectorNumber > 31 ? 16 : 4); k++) // if sector > 31 is 16 blocks each sector i.e. mifare 4k else its 1k or 2k with 4 blocks each sector
            {
                DataBlock = new MifareClassicDataBlockModel(
                    (byte)CustomConverter.GetChipBasedDataBlockNumber(sectorNumber, k),
                    k);

                try
                {
                    var isAuth = false;

                    try
                    {
                        await readerDevice.MifareClassic_LoginAsync(aKey, 0, (byte)elatecSpecificSectorNumber);
                        isAuth = true;
                    }
                    catch
                    {
                        isAuth = false;
                    }

                    if (buffer == null || buffer.Length != 16 && isAuth) // Read Mode
                    {
                        try
                        {
                            var data = await readerDevice.MifareClassic_ReadBlockAsync((byte)CustomConverter.GetChipBasedDataBlockNumber(sectorNumber, k));

                            if (data.Length > 1)
                            {
                                DataBlock.Data = data;
                                DataBlock.IsAuthenticated = true;
                                Sector.IsAuthenticated = isAuth;
                                Sector.DataBlock.Add(DataBlock);
                            }
                        }
                        catch // No Read Access Allowed, try bKey
                        {
                            try
                            {
                                await readerDevice.MifareClassic_LoginAsync(bKey, 1, (byte)elatecSpecificSectorNumber);
                                isAuth = true;

                                var data = await readerDevice.MifareClassic_ReadBlockAsync((byte)CustomConverter.GetChipBasedDataBlockNumber(sectorNumber, k));

                                if (data.Length > 1)
                                {
                                    DataBlock.Data = data;
                                    DataBlock.IsAuthenticated = true;
                                    Sector.IsAuthenticated = isAuth;
                                    Sector.DataBlock.Add(DataBlock);
                                }
                            }
                            catch
                            {
                                isAuth = false;
                                Sector.IsAuthenticated = false;
                                DataBlock.IsAuthenticated = false; // finally failed to read data
                            }
                        }
                    } // read Data

                    else if (buffer != null && buffer.Length == 16)
                    {
                        try
                        {
                            await readerDevice.MifareClassic_WriteBlockAsync(buffer, k);
                            return ERROR.NoError;
                        }
                        catch
                        {
                            return ERROR.AuthFailure;
                        }
                    } // write Data
                }
                catch
                {
                    return ERROR.TransportError; // IO ElatecError
                }
            }

            if (Sector.IsAuthenticated)
            {
                return ERROR.NoError; //NO ElatecError
            }

            return ERROR.AuthFailure; // Auth ElatecError
        }

        #endregion

        #region MifareUltralight

        /// <summary>What the reader found on the antenna: whether a tag answered and, for a MIFARE chip, its sub type.</summary>
        protected readonly struct UltralightSighting
        {
            /// <summary>Creates the sighting.</summary>
            /// <param name="tagFound">Whether a tag answered.</param>
            /// <param name="subType">The MIFARE sub type of the tag, or <c>null</c> for a tag that is no MIFARE chip.</param>
            public UltralightSighting(bool tagFound, MifareChipSubType? subType)
            {
                TagFound = tagFound;
                SubType = subType;
            }

            /// <summary>Gets a value that says whether a tag answered.</summary>
            public bool TagFound { get; }

            /// <summary>Gets the MIFARE sub type, or <c>null</c>.</summary>
            public MifareChipSubType? SubType { get; }
        }

        /// <summary>Gets how long the selection of the chip waits before it asks again (it asks three times at most).</summary>
        protected virtual TimeSpan UltralightSelectRetryDelay => TimeSpan.FromMilliseconds(100);

        /// <summary>Selects the tag on the antenna and tells what it is; the first call of every page operation. Overridden by the tests.</summary>
        /// <returns>What the reader found.</returns>
        protected virtual async Task<UltralightSighting> SelectUltralightChipAsync()
        {
            var chip = await readerDevice.GetSingleChipAsync();

            return chip == null
                ? new UltralightSighting(false, null)
                : new UltralightSighting(true, (chip as MifareChip)?.SubType);
        }

        /// <summary>The GET_VERSION command: 8 bytes, or <c>null</c> when the chip refuses it (an Ultralight and an Ultralight C do, and are idle afterwards).</summary>
        /// <returns>The answer.</returns>
        protected virtual Task<byte[]> GetUltralightVersionAsync() => readerDevice.MifareUltralightEV1_GetVersionAsync();

        /// <summary>The READ command: 16 bytes, the page and the three after it (and from page 0 again past the last page), or <c>null</c> when the chip refuses.</summary>
        /// <param name="page">The page.</param>
        /// <returns>The 16 bytes.</returns>
        protected virtual Task<byte[]> ReadUltralightPageAsync(byte page) => readerDevice.MifareUltralight_ReadPageAsync(page);

        /// <summary>The WRITE command for one page. A refusal is an exception.</summary>
        /// <param name="page">The page.</param>
        /// <param name="data">4 bytes.</param>
        /// <returns>A task that ends when the chip took the data.</returns>
        protected virtual Task WriteUltralightPageAsync(byte page, byte[] data) => readerDevice.MifareUltralight_WritePageAsync(page, data);

        /// <summary>The 3DES authentication of an Ultralight C. A refusal is an exception.</summary>
        /// <param name="key">The 16-byte key.</param>
        /// <returns>A task that ends when the chip took the key.</returns>
        protected virtual Task AuthUltralightCAsync(byte[] key) => readerDevice.MifareUltralightC_AuthenticateAsync(key);

        /// <summary>The PWD_AUTH of an Ultralight EV1 or an NTAG. A refusal is an exception.</summary>
        /// <param name="password">The 4-byte password.</param>
        /// <param name="pwdAck">The 2-byte PACK the chip must answer with.</param>
        /// <returns>A task that ends when the chip took the password.</returns>
        protected virtual Task PwdAuthUltralightAsync(byte[] password, byte[] pwdAck) => readerDevice.MifareUltralightEV1_PwdAuthAsync(password, pwdAck);

        /// <summary>Selects the tag again and says whether it is there: what tells a chip that refused from one that was taken away.</summary>
        /// <returns><c>true</c> when a tag answers.</returns>
        protected virtual async Task<bool> IsUltralightTagPresentAsync() => await readerDevice.SearchTagAsync() != null;

        /// <inheritdoc />
        public override Task<UltralightResult> ReadMifareUltralightPagesAsync(UltralightAccess access, int startPage, int pageCount) =>
            PerformUltralightAsync(UltralightRequest.ForRead(access, startPage, pageCount));

        /// <inheritdoc />
        public override Task<UltralightResult> WriteMifareUltralightPagesAsync(UltralightAccess access, int startPage, byte[] data) =>
            PerformUltralightAsync(UltralightRequest.ForWrite(access, startPage, data));

        /// <inheritdoc />
        public override Task<UltralightResult> AuthenticateMifareUltralightAsync(UltralightAccess access) =>
            PerformUltralightAsync(UltralightRequest.ForAuthenticate(access));

        /// <inheritdoc />
        public override async Task<ERROR> ReadMifareUltralightSinglePage(int _pageNo)
        {
            var result = await ReadMifareUltralightPagesAsync(UltralightAccess.Open(), _pageNo, 1);

            MifareUltralightPageData = result.Data;

            return result.Error;
        }

        // One call is one boundary under the lock of the port: what is wrong with the call itself is found before the port is
        // touched; then the chip is selected and told, the call is checked against what it is, the authentication is made once,
        // and the work is done. Nothing is repeated after a refusal (the selection is the one exception, and changes nothing on the
        // card). The bytes of the key are cleared when the request is disposed, whatever happens.
        private async Task<UltralightResult> PerformUltralightAsync(UltralightRequest request)
        {
            using (request)
            {
                if (request.Error != ERROR.NoError)
                {
                    return UltralightResult.Failed(request.Error);
                }

                await _comPortLock.WaitAsync();
                try
                {
                    if (!IsConnected)
                    {
                        return UltralightResult.Failed(ERROR.TransportError);
                    }

                    try
                    {
                        return await RunUltralightAsync(request);
                    }
                    catch (Exception e) when (!(e is OperationCanceledException))
                    {
                        // A fault outside the calls that classify their own: the call ends, and the lock is released below.
                        LogUltralightFailure(request.Operation, "unexpected", e);

                        return UltralightResult.Failed(ElatecUltralightErrors.Classify(e, UltralightCall.Select));
                    }
                }
                finally
                {
                    _comPortLock.Release();
                }
            }
        }

        private async Task<UltralightResult> RunUltralightAsync(UltralightRequest request)
        {
            var (detectError, map) = await DetectUltralightChipAsync(request.Access.Expected);

            if (detectError != ERROR.NoError)
            {
                return UltralightResult.Failed(detectError);
            }

            var variant = map.Variant;

            // The checks that need the chip: that it is the one the task expects, that it has the pages, that a write is allowed.
            var check = request.CheckAgainst(map);

            if (check != ERROR.NoError)
            {
                return new UltralightResult(check, null, variant, false, 0, null);
            }

            var authenticated = false;

            if (request.Access.Auth != UltralightAuth.None)
            {
                var call = request.Access.Auth == UltralightAuth.UltralightC ? UltralightCall.AuthUltralightC : UltralightCall.AuthPassword;

                try
                {
                    if (call == UltralightCall.AuthUltralightC)
                    {
                        await AuthUltralightCAsync(request.Key);
                    }
                    else
                    {
                        await PwdAuthUltralightAsync(request.Key, request.Pack);
                    }

                    authenticated = true;
                }
                catch (Exception e) when (!(e is OperationCanceledException))
                {
                    // Once. A wrong key is not tried again, and nothing is read or written after it.
                    LogUltralightFailure(request.Operation, call.ToString(), e);

                    return new UltralightResult(ElatecUltralightErrors.Classify(e, call), null, variant, false, 0, null);
                }
            }

            switch (request.Operation)
            {
                case UltralightOperation.Read:
                    return await ReadUltralightRangeAsync(request.StartPage, request.PageCount, variant, authenticated);

                case UltralightOperation.Write:
                    return await WriteUltralightRangeAsync(request, variant, authenticated);

                default:
                    return new UltralightResult(ERROR.NoError, null, variant, authenticated, 0, null);
            }
        }

        // Finds the chip: it is selected (three times at most, which sets nothing on the card), and told by its sub type and by GET_VERSION.
        private async Task<(ERROR Error, UltralightMemoryMap Map)> DetectUltralightChipAsync(UltralightExpectation expected)
        {
            var sighting = default(UltralightSighting);

            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    sighting = await SelectUltralightChipAsync();
                }
                catch (Exception e) when (!(e is OperationCanceledException))
                {
                    LogUltralightFailure(UltralightOperation.Read, UltralightCall.Select.ToString(), e);

                    return (ElatecUltralightErrors.Classify(e, UltralightCall.Select), null);
                }

                if (sighting.TagFound)
                {
                    break;
                }

                if (attempt < 3)
                {
                    await Task.Delay(UltralightSelectRetryDelay);
                }
            }

            if (!sighting.TagFound)
            {
                return (ERROR.TransportError, null);
            }

            if (!IsUltralightFamily(sighting.SubType))
            {
                return (ERROR.ProtocolConstraint, null);
            }

            if (sighting.SubType == MifareChipSubType.MifareUltralightC)
            {
                return (ERROR.NoError, UltralightMemoryMap.For(UltralightVariant.C));
            }

            byte[] version;

            try
            {
                version = await GetUltralightVersionAsync();
            }
            catch (Exception e) when (!(e is OperationCanceledException))
            {
                LogUltralightFailure(UltralightOperation.Read, UltralightCall.Version.ToString(), e);

                return (ElatecUltralightErrors.Classify(e, UltralightCall.Version), null);
            }

            if (version != null)
            {
                // An answer that names a chip of the table, or nothing this class knows: not guessed at.
                return UltralightMemoryMap.TryFromGetVersion(version, out var named) ? (ERROR.NoError, named) : (ERROR.ProtocolConstraint, null);
            }

            // The chip refused GET_VERSION and is idle: select it again. A plain Ultralight does so, and so does an Ultralight C, which
            // Elatec.NET 0.7.0-beta.1 reports as MifareUltralight too, so what the task expects tells them apart (an authentication
            // with the key proves it).
            try
            {
                sighting = await SelectUltralightChipAsync();
            }
            catch (Exception e) when (!(e is OperationCanceledException))
            {
                LogUltralightFailure(UltralightOperation.Read, UltralightCall.Select.ToString(), e);

                return (ElatecUltralightErrors.Classify(e, UltralightCall.Select), null);
            }

            if (!sighting.TagFound)
            {
                return (ERROR.TransportError, null);
            }

            return (ERROR.NoError, UltralightMemoryMap.For(expected == UltralightExpectation.C ? UltralightVariant.C : UltralightVariant.Plain));
        }

        // The sub types of Elatec.NET that are chips of this family: Ultralight (0x90), Ultralight C (0x91), Ultralight EV1 (0x92), NTAG21x (0xA0-0xA6).
        private static bool IsUltralightFamily(MifareChipSubType? subType)
        {
            if (!subType.HasValue)
            {
                return false;
            }

            var value = (int)subType.Value;

            return (value >= 0x90 && value <= 0x92) || (value >= 0xA0 && value <= 0xA6);
        }

        // One READ returns four pages (and wraps to page 0 past the last page), so the range is read four pages at a time and only
        // the pages that were asked for are kept: the range is in the chip, so those are always real pages.
        private async Task<UltralightResult> ReadUltralightRangeAsync(int startPage, int pageCount, UltralightVariant variant, bool authenticated)
        {
            var data = new byte[pageCount * UltralightMemoryMap.PageSize];
            var done = 0;

            while (done < pageCount)
            {
                var page = startPage + done;
                var need = Math.Min(4, pageCount - done);
                byte[] block;

                try
                {
                    block = await ReadUltralightPageAsync((byte)page);
                }
                catch (Exception e) when (!(e is OperationCanceledException))
                {
                    LogUltralightFailure(UltralightOperation.Read, UltralightCall.Read.ToString(), e);

                    return new UltralightResult(ElatecUltralightErrors.Classify(e, UltralightCall.Read), data.Take(done * 4).ToArray(), variant, authenticated, done, page);
                }

                if (block == null || block.Length < need * UltralightMemoryMap.PageSize)
                {
                    // The call returns null for the chip that said no and for the chip that is gone; one look at the antenna tells which.
                    var error = await ClassifyRefusalAsync();

                    return new UltralightResult(error, data.Take(done * 4).ToArray(), variant, authenticated, done, page);
                }

                Array.Copy(block, 0, data, done * 4, need * UltralightMemoryMap.PageSize);
                done += need;
            }

            return new UltralightResult(ERROR.NoError, data, variant, authenticated, done, null);
        }

        private async Task<UltralightResult> WriteUltralightRangeAsync(UltralightRequest request, UltralightVariant variant, bool authenticated)
        {
            var written = 0;

            while (written < request.PageCount)
            {
                var page = request.StartPage + written;
                var chunk = new byte[UltralightMemoryMap.PageSize];
                Array.Copy(request.Data, written * UltralightMemoryMap.PageSize, chunk, 0, UltralightMemoryMap.PageSize);

                try
                {
                    await WriteUltralightPageAsync((byte)page, chunk);
                }
                catch (Exception e) when (!(e is OperationCanceledException))
                {
                    // One attempt: the page that was refused is not written again, and no later page is written.
                    LogUltralightFailure(UltralightOperation.Write, UltralightCall.Write.ToString(), e);

                    return new UltralightResult(ElatecUltralightErrors.Classify(e, UltralightCall.Write), null, variant, authenticated, written, page);
                }

                written++;
            }

            // What the call returned says nothing about the page: it is read back (reads only, no second write).
            var back = await ReadUltralightRangeAsync(request.StartPage, request.PageCount, variant, authenticated);

            if (!back.Succeeded)
            {
                return new UltralightResult(back.Error, null, variant, authenticated, written, back.FailedPage);
            }

            for (var i = 0; i < request.PageCount; i++)
            {
                for (var b = 0; b < UltralightMemoryMap.PageSize; b++)
                {
                    if (back.Data[i * UltralightMemoryMap.PageSize + b] != request.Data[i * UltralightMemoryMap.PageSize + b])
                    {
                        return new UltralightResult(ERROR.Unknown, null, variant, authenticated, written, request.StartPage + i);
                    }
                }
            }

            return new UltralightResult(ERROR.NoError, null, variant, authenticated, written, null);
        }

        private async Task<ERROR> ClassifyRefusalAsync()
        {
            try
            {
                return await IsUltralightTagPresentAsync() ? ERROR.PermissionDenied : ERROR.TransportError;
            }
            catch (Exception e) when (!(e is OperationCanceledException))
            {
                LogUltralightFailure(UltralightOperation.Read, UltralightCall.Select.ToString(), e);

                return ElatecUltralightErrors.Classify(e, UltralightCall.Select);
            }
        }

        // The log gets the operation, the call, and the name of the exception type and reader error: never a key, a password, a UID or the text of an exception.
        private static void LogUltralightFailure(UltralightOperation operation, string call, Exception exception) =>
            Log.ForContext<ElatecNetProvider>().Warning(
                "Elatec Ultralight {Operation} failed at {Call}: {Fault}",
                operation,
                call,
                ElatecUltralightErrors.Describe(exception));

        #endregion

        #region MifareDesfire

        /// <inheritdoc />
        public async override Task<byte> MifareDesfire_GetKeyVersionAsync(byte keyNo)
        {
            await _comPortLock.WaitAsync();
            try
            {
                if (readerDevice == null || !readerDevice.IsConnected)
                {
                    throw new ReaderException("Reader not connected");
                }

                try
                {
                    var keyVersion = await readerDevice.MifareDesfire_GetKeyVersionAsync(keyNo);
                    KeyVersion = keyVersion;
                    return keyVersion;
                }
                catch (Exception e)
                {
                    Log.ForContext<ElatecNetProvider>().Error(e, "Unable to read DESFire key version.");
                    throw;
                }
            }
            finally
            {
                _comPortLock.Release();
            }
        }

        /// <inheritdoc />
        public async override Task<ERROR> GetMiFareDESFireChipAppIDs(string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey)
        {
            await _comPortLock.WaitAsync();
            try
            {
                if (!IsConnected)
                {
                    return ERROR.TransportError;
                }

                DesfireChip ??= new MifareDesfireChipModel();
                DesfireChip.AppList = new List<MifareDesfireAppModel>();

                uint[] appArr = null;
                Exception lastDirectoryException = null;
                const int maxAttempts = 3;
                for (var attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    try
                    {
                        var tag = await readerDevice.SearchTagAsync();
                        if (tag == null)
                        {
                            throw new InvalidOperationException("SearchTag did not return a tag.");
                        }
                        await readerDevice.MifareDesfire_SelectApplicationAsync(0);
                        appArr = await readerDevice.MifareDesfire_GetAppIDsAsync();
                        lastDirectoryException = null;
                        break;
                    }
                    catch (Exception e)
                    {
                        lastDirectoryException = e;
                        if (attempt < maxAttempts)
                        {
                            Log.ForContext<ElatecNetProvider>().Warning(e,
                                "Elatec DESFire directory listing attempt {Attempt}/{MaxAttempts} failed; retrying SearchTag/GetAppIDs.",
                                attempt, maxAttempts);
                            await Task.Delay(100);
                        }
                    }
                }

                if (lastDirectoryException != null)
                {
                    Log.ForContext<ElatecNetProvider>().Error(lastDirectoryException,
                        "Elatec DESFire directory listing failed after {MaxAttempts} attempts.", maxAttempts);
                    return ERROR.PermissionDenied;
                }

                if (appArr != null)
                {
                    foreach (var appid in appArr)
                    {
                        DesfireChip.AppList.Add(new MifareDesfireAppModel(appid));
                    }
                }

                try
                {
                    await readerDevice.SearchTagAsync();
                    await readerDevice.MifareDesfire_SelectApplicationAsync(0);
                    DesfireChip.FreeMemory = await readerDevice.MifareDesfire_GetFreeMemoryAsync();
                }
                catch (Exception e)
                {
                    DesfireChip.FreeMemory = 0;
                    Log.ForContext<ElatecNetProvider>().Warning(e,
                        "Elatec DESFire free-memory metadata lookup failed; directory listing remains valid.");
                }

                return ERROR.NoError;
            }
            finally
            {
                _comPortLock.Release();
            }
        }

        /// <inheritdoc />
        public async override Task<ERROR> AuthToMifareDesfireApplication(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumber, int _appID)
        {
            await _comPortLock.WaitAsync();
            try
            {
                return await AuthToMifareDesfireApplicationCore(_applicationMasterKey, _keyType, _keyNumber, _appID);
            }
            finally
            {
                _comPortLock.Release();
            }
        }

        /// <inheritdoc />
        public override async Task<ERROR> VerifyMifareDesfireKeyChange(string applicationKey, DESFireKeyType keyType, int keyNumber, int appId = 0)
        {
            await _comPortLock.WaitAsync();
            try
            {
                if (readerDevice == null)
                {
                    return ERROR.TransportError;
                }

                // The ELATEC SDK retains an authenticated DESFire session after ChangeKey.
                // A same-session Authenticate call therefore reports success even if the key
                // was not changed. Disconnect and reconnect to get a clean session before the
                // authoritative check. ChangeKey can leave IsConnected stale while the port is
                // already closed, so DisconnectAsync must run unconditionally.
                if (readerDevice.IsConnected)
                {
                    await readerDevice.DisconnectAsync();
                }

                await Task.Delay(250);

                if (!await readerDevice.ConnectAsync())
                {
                    return ERROR.TransportError;
                }

                return await AuthToMifareDesfireApplicationCore(applicationKey, keyType, keyNumber, appId);
            }
            catch (Exception e)
            {
                Log.ForContext<ElatecNetProvider>().Error(e,
                    "DESFire fresh-session key verification failed for AppId {AppId} KeyNo {KeyNumber}.",
                    appId, keyNumber);
                return ERROR.TransportError;
            }
            finally
            {
                _comPortLock.Release();
            }
        }

        protected virtual async Task<ERROR> AuthToMifareDesfireApplicationCore(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumber, int _appID)
        {
            if (!IsConnected)
            {
                return ERROR.TransportError;
            }

            Exception lastSelectException = null;
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    var tag = await readerDevice.SearchTagAsync();
                    if (tag == null)
                    {
                        throw new InvalidOperationException("SearchTag did not return a tag.");
                    }

                    await readerDevice.MifareDesfire_SelectApplicationAsync((uint)_appID);
                    lastSelectException = null;
                    break;
                }
                catch (Exception e)
                {
                    lastSelectException = e;
                    if (attempt < 3)
                    {
                        Log.ForContext<ElatecNetProvider>().Warning(e,
                            "Elatec DESFire context establishment attempt {Attempt}/3 failed for AppId {AppId} KeyNo {KeyNumber}; retrying SearchTag/Select.",
                            attempt, _appID, _keyNumber);
                        await Task.Delay(100);
                    }
                }
            }

            if (lastSelectException != null)
            {
                Log.ForContext<ElatecNetProvider>().Error(lastSelectException,
                    "Elatec DESFire SearchTag/Select failed after 3 attempts for AppId {AppId} KeyNo {KeyNumber}.",
                    _appID, _keyNumber);
                return ERROR.TransportError;
            }

            try
            {
                await readerDevice.MifareDesfire_AuthenticateAsync(
                    _applicationMasterKey,
                    (byte)_keyNumber,
                    (byte)(int)Enum.Parse(typeof(Elatec.NET.Cards.Mifare.DESFireKeyType), Enum.GetName(typeof(DESFireKeyType), _keyType)),
                    DESFIRE_AUTHMODE_EV1);
                return ERROR.NoError;
            }
            catch (Exception e)
            {
                Log.ForContext<ElatecNetProvider>().Error(e,
                    "Elatec DESFire Authenticate failed for AppId {AppId} KeyNo {KeyNumber} KeyType {KeyType}.",
                    _appID, _keyNumber, _keyType);
                return ERROR.AuthFailure;
            }
        }

        /// <inheritdoc />
        public async override Task<OperationResult> GetMifareDesfireAppSettings(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent, int _appID, bool authenticateBeforeReading = true)
        {
            await _comPortLock.WaitAsync();
            try
            {
            if (readerDevice.IsConnected)
            {
                try { await readerDevice.SearchTagAsync(); } catch { }

                try
                {
                    await readerDevice.MifareDesfire_SelectApplicationAsync((uint)_appID);

                    if (authenticateBeforeReading)
                    {
                        try
                        {
                            await readerDevice.MifareDesfire_AuthenticateAsync(_applicationMasterKey, (byte)_keyNumberCurrent, (byte)(int)Enum.Parse(typeof(Elatec.NET.Cards.Mifare.DESFireKeyType), Enum.GetName(typeof(DESFireKeyType), _keyType)), DESFIRE_AUTHMODE_EV1);
                        }
                        catch
                        {
                            // Auth failed — fall through and attempt GetKeySettings without
                            // authentication, since many apps permit free key-settings reads.
                        }
                    }

                    var ks = await readerDevice.MifareDesfire_GetKeySettingsAsync();

                    MaxNumberOfAppKeys = (byte)ks.NumberOfKeys;
                    EncryptionType = ResolveDesfireKeyType(ks.KeyType.ToString(), _keyType);
                    DesfireAppKeySetting = (AccessControl.DESFireKeySettings)ks.AccessRights;

                    var metadata = new Dictionary<string, string>
                    {
                        { "ApplicationId", _appID.ToString(CultureInfo.CurrentCulture) },
                        { "KeyNumber", _keyNumberCurrent.ToString(CultureInfo.CurrentCulture) }
                    };

                    return OperationResult.Success(
                        operation: nameof(GetMifareDesfireAppSettings),
                        wasAuthenticated: authenticateBeforeReading,
                        metadata: metadata);
                }
                catch (TimeoutException e)
                {
                    return OperationResult.Failure(
                        ERROR.TransportError,
                        "Reading application settings timed out",
                        e.Message,
                        nameof(GetMifareDesfireAppSettings),
                        authenticateBeforeReading,
                        new Dictionary<string, string>
                        {
                            { "ApplicationId", _appID.ToString(CultureInfo.CurrentCulture) },
                            { "KeyNumber", _keyNumberCurrent.ToString(CultureInfo.CurrentCulture) }
                        });
                }
                catch (UnauthorizedAccessException e)
                {
                    return OperationResult.Failure(
                        ERROR.PermissionDenied,
                        "Reader denied access while fetching application settings",
                        e.Message,
                        nameof(GetMifareDesfireAppSettings),
                        authenticateBeforeReading,
                        new Dictionary<string, string>
                        {
                            { "ApplicationId", _appID.ToString(CultureInfo.CurrentCulture) },
                            { "KeyNumber", _keyNumberCurrent.ToString(CultureInfo.CurrentCulture) }
                        });
                }
                catch (Exception e)
                {
                    var code = ERROR.TransportError;
                    if (e.Message.Contains("AUTH") || e.Message.Contains("auth", StringComparison.InvariantCultureIgnoreCase))
                    {
                        code = ERROR.AuthFailure;
                    }

                    return OperationResult.Failure(
                        code,
                        "Failed to read application settings",
                        e.Message,
                        nameof(GetMifareDesfireAppSettings),
                        authenticateBeforeReading,
                        new Dictionary<string, string>
                        {
                            { "ApplicationId", _appID.ToString(CultureInfo.CurrentCulture) },
                            { "KeyNumber", _keyNumberCurrent.ToString(CultureInfo.CurrentCulture) }
                        });
                }
            }

            else
            {
                return OperationResult.Failure(
                    ERROR.TransportError,
                    "Reader not connected",
                    operation: nameof(GetMifareDesfireAppSettings),
                    metadata: new Dictionary<string, string>
                    {
                        { "ApplicationId", _appID.ToString(CultureInfo.CurrentCulture) },
                        { "KeyNumber", _keyNumberCurrent.ToString(CultureInfo.CurrentCulture) }
                    });
            }
            }
            finally
            {
                _comPortLock.Release();
            }
        }

        /// <inheritdoc />
        public async override Task<OperationResult> CreateMifareDesfireApplication(string _piccMasterKey, AccessControl.DESFireKeySettings _keySettingsTarget,
                                        DESFireKeyType _keyTypePiccMasterKey, DESFireKeyType _keyTypeTargetApplication,
                                        int _maxNbKeys, int _appID, bool authenticateToPICCFirst = true)
        {
            await _comPortLock.WaitAsync();
            try
            {
                if (!IsConnected)
                {
                    return OperationResult.Failure(
                        ERROR.TransportError,
                        "Reader not connected",
                        operation: nameof(CreateMifareDesfireApplication),
                        metadata: new Dictionary<string, string>
                        {
                            { "ApplicationId", _appID.ToString(CultureInfo.CurrentCulture) },
                            { "MaxKeys", _maxNbKeys.ToString(CultureInfo.CurrentCulture) }
                        });
                }

                var metadata = new Dictionary<string, string>
                {
                    { "ApplicationId", _appID.ToString(CultureInfo.CurrentCulture) },
                    { "MaxKeys", _maxNbKeys.ToString(CultureInfo.CurrentCulture) },
                    { "AuthenticateToPICCFirst", authenticateToPICCFirst.ToString(CultureInfo.CurrentCulture) }
                };

                if (authenticateToPICCFirst)
                {
                    // Exactly one complete boundary. The caller must not authenticate first.
                    var authResult = await AuthToMifareDesfireApplicationCore(
                        _piccMasterKey, _keyTypePiccMasterKey, 0, 0);
                    if (authResult != ERROR.NoError)
                    {
                        return OperationResult.Failure(
                            authResult,
                            "PICC authentication failed while creating application",
                            operation: nameof(CreateMifareDesfireApplication),
                            wasAuthenticated: false,
                            metadata: metadata);
                    }
                }
                else
                {
                    try
                    {
                        await readerDevice.SearchTagAsync();
                        await readerDevice.MifareDesfire_SelectApplicationAsync(0);
                    }
                    catch (Exception e)
                    {
                        Log.ForContext<ElatecNetProvider>().Error(e,
                            "Elatec DESFire PICC selection failed before unauthenticated CreateApplication for AppId {AppId}.", _appID);
                        return OperationResult.Failure(
                            ERROR.TransportError,
                            "PICC selection failed while creating application",
                            e.Message,
                            nameof(CreateMifareDesfireApplication),
                            false,
                            metadata);
                    }
                }

                try
                {
                    await readerDevice.MifareDesfire_CreateApplicationAsync(
                                                (DESFireAppAccessRights)_keySettingsTarget,
                                                (Elatec.NET.Cards.Mifare.DESFireKeyType)Enum.Parse(typeof(Elatec.NET.Cards.Mifare.DESFireKeyType), Enum.GetName(typeof(DESFireKeyType), _keyTypeTargetApplication)),
                                                _maxNbKeys,
                                                _appID);

                    return OperationResult.Success(
                        operation: nameof(CreateMifareDesfireApplication),
                        wasAuthenticated: authenticateToPICCFirst,
                        metadata: metadata);
                }
                catch (TimeoutException e)
                {
                    return OperationResult.Failure(
                        ERROR.TransportError,
                        "Creating application timed out",
                        e.Message,
                        nameof(CreateMifareDesfireApplication),
                        authenticateToPICCFirst,
                        new Dictionary<string, string>
                        {
                            { "ApplicationId", _appID.ToString(CultureInfo.CurrentCulture) },
                            { "MaxKeys", _maxNbKeys.ToString(CultureInfo.CurrentCulture) }
                        });
                }
                catch (UnauthorizedAccessException e)
                {
                    return OperationResult.Failure(
                        ERROR.PermissionDenied,
                        "Reader denied access while creating application",
                        e.Message,
                        nameof(CreateMifareDesfireApplication),
                        authenticateToPICCFirst,
                        new Dictionary<string, string>
                        {
                            { "ApplicationId", _appID.ToString(CultureInfo.CurrentCulture) },
                            { "MaxKeys", _maxNbKeys.ToString(CultureInfo.CurrentCulture) }
                        });
                }
                catch (Exception e)
                {
                    Log.ForContext<ElatecNetProvider>().Error(e,
                        "Elatec DESFire CreateApplication command failed for AppId {AppId} MaxKeys {MaxKeys}.",
                        _appID, _maxNbKeys);
                    return OperationResult.Failure(
                        ERROR.PermissionDenied,
                        "Failed to create application",
                        e.Message,
                        nameof(CreateMifareDesfireApplication),
                        authenticateToPICCFirst,
                        new Dictionary<string, string>
                        {
                            { "ApplicationId", _appID.ToString(CultureInfo.CurrentCulture) },
                            { "MaxKeys", _maxNbKeys.ToString(CultureInfo.CurrentCulture) }
                        });
                }
            }
            finally
            {
                _comPortLock.Release();
            }
        }

        /// <inheritdoc />
        public override async Task<ERROR> ChangeMifareDesfireKeyAsync(
            uint appId,
            byte targetKeyNo,
            DESFireKeyType targetKeyType,
            string currentTargetKeyHex,
            string newTargetKeyHex,
            byte newTargetKeyVersion,
            string masterKeyHex,
            DESFireKeyType masterKeyType,
            AccessControl.DESFireKeySettings currentKeySettings)
        {
            await _comPortLock.WaitAsync();
            try
            {

            if (!readerDevice.IsConnected)
            {
                return ERROR.TransportError;
            }

            try { await readerDevice.SearchTagAsync(); } catch { }

            try
            {
                var resolved = DesfireKeyChangeInputs.Resolve(
                    appId,
                    targetKeyNo,
                    targetKeyType,
                    currentTargetKeyHex,
                    newTargetKeyHex,
                    newTargetKeyVersion,
                    masterKeyHex,
                    masterKeyType,
                    currentKeySettings);

                // Scope selection: appId==0 selects PICC; appId>0 selects that application.
                await readerDevice.MifareDesfire_SelectApplicationAsync(resolved.AppId);

                // Authenticate according to the change-key policy (key 0 or target key).
                await readerDevice.MifareDesfire_AuthenticateAsync(
                    resolved.AuthKeyHex,
                    resolved.AuthKeyNo,
                    (byte)ToElatecKeyType(resolved.AuthKeyType),
                    DESFIRE_AUTHMODE_EV1);

                // Some reader APIs require key count/key type context to issue ChangeKey.
                // Prefer reading live values; fall back to configured or conservative defaults.
                byte keyCount;
                DESFireKeyType keyTypeForContext = resolved.TargetKeyType;

                try
                {
                    var ks = await readerDevice.MifareDesfire_GetKeySettingsAsync();
                    keyCount = (byte)ks.NumberOfKeys;

                    // Optional: keep provider state synced if other operations depend on it.
                    MaxNumberOfAppKeys = keyCount;

                    // Try map the returned key type to your enum by name; if it fails, keep the requested targetKeyType.
                    if (Enum.TryParse<DESFireKeyType>(ks.KeyType.ToString(), out var parsed))
                    {
                        keyTypeForContext = ResolveKeyTypeForChange(resolved.AppId, resolved.TargetKeyType, parsed);
                    }
                }
                catch
                {
                    // PICC level is always 1 "master key slot" in your existing logic.
                    // For applications, prefer MaxNumberOfAppKeys if set, otherwise use 15 as a conservative default.
                    keyCount = resolved.AppId == 0
                        ? (byte)1
                        : (MaxNumberOfAppKeys > 0 ? MaxNumberOfAppKeys : (byte)15);
                }

                // The ELATEC SDK requires the current settings, key count and key type as builder
                // context for ChangeKey. This value describes the existing card policy; this
                // operation does not issue ChangeKeySettings or modify that policy.
                await readerDevice.MifareDesfire_ChangeKeyAsync(
                    resolved.OldTargetKeyHex,
                    resolved.NewTargetKeyHex,
                    resolved.NewTargetKeyVersion,
                    resolved.CurrentKeySettingsContext,
                    resolved.TargetKeyNo,
                    (uint)keyCount,
                    ToElatecKeyType(keyTypeForContext));

                return ERROR.NoError;
            }
            catch
            {
                // You may want a richer mapping (auth vs transport vs protocol) later.
                return ERROR.AuthFailure;
            }

            }
            finally
            {
                _comPortLock.Release();
            }

            /// <summary>
            /// Converts your local DESFireKeyType enum to the Elatec wrapper enum.
            /// Uses name-based parsing to avoid brittle numeric casts when enum values differ.
            /// </summary>
            static Elatec.NET.Cards.Mifare.DESFireKeyType ToElatecKeyType(DESFireKeyType t)
            {
                if (Enum.TryParse<Elatec.NET.Cards.Mifare.DESFireKeyType>(t.ToString(), out var mapped))
                {
                    return mapped;
                }

                throw new ArgumentOutOfRangeException(nameof(t), $"No Elatec key type mapping for {t}.");
            }
        }

        /// <inheritdoc />
        public override async Task<ERROR> CommitTransactionAsync()
        {
            await _comPortLock.WaitAsync();
            try
            {
                if (!readerDevice.IsConnected)
                {
                    return ERROR.TransportError;
                }

                try
                {
                    await readerDevice.MifareDesfire_CommitTransactionAsync();
                    return ERROR.NoError;
                }
                catch
                {
                    return ERROR.AuthFailure;
                }
            }
            finally
            {
                _comPortLock.Release();
            }
        }

        /// <summary>
        /// Resolves the key type to use when issuing a DESFire ChangeKey command via the Elatec API.
        /// </summary>
        /// <param name="appId">Application identifier (0 = PICC, &gt;0 = application).</param>
        /// <param name="targetKeyType">Desired target key type from the UI.</param>
        /// <param name="detectedKeyType">Detected key type from the card, if available.</param>
        /// <returns>The key type to pass to the reader API for ChangeKey.</returns>
        internal static DESFireKeyType ResolveKeyTypeForChange(uint appId, DESFireKeyType targetKeyType, DESFireKeyType? detectedKeyType)
        {
            if (appId == 0)
            {
                return targetKeyType;
            }

            return detectedKeyType ?? targetKeyType;
        }

        /// <summary>
        /// Maps a provider-reported key type name to the local DESFire key type enum.
        /// </summary>
        /// <param name="providerKeyTypeName">Name reported by the provider for the key type.</param>
        /// <param name="fallback">Fallback when the name is not recognized.</param>
        /// <returns>The resolved DESFire key type.</returns>
        internal static DESFireKeyType ResolveDesfireKeyType(string providerKeyTypeName, DESFireKeyType fallback)
        {
            if (Enum.TryParse<DESFireKeyType>(providerKeyTypeName, out var parsed))
            {
                return parsed;
            }

            return fallback;
        }

        /// <inheritdoc />
        public async override Task<ERROR> ChangeMifareDesfireApplicationKeySettings(string _applicationMasterKeyCurrent, int _keyNumberCurrent, DESFireKeyType _keyTypeCurrent,
           int _appIDCurrent, AccessControl.DESFireKeySettings keySettings)
        {
            await _comPortLock.WaitAsync();
            try
            {
            if (readerDevice.IsConnected)
            {
                try { await readerDevice.SearchTagAsync(); } catch { }

                try
                {
                    await readerDevice.MifareDesfire_SelectApplicationAsync((uint)_appIDCurrent);

                    await readerDevice.MifareDesfire_AuthenticateAsync(_applicationMasterKeyCurrent, 0, (byte)(int)Enum.Parse(typeof(Elatec.NET.Cards.Mifare.DESFireKeyType), Enum.GetName(typeof(DESFireKeyType), _keyTypeCurrent)), DESFIRE_AUTHMODE_EV1);

                    byte? cardKeyCount = null;
                    DESFireKeyType? cardKeyType = null;

                    try
                    {
                        var desfireSettings = await readerDevice.MifareDesfire_GetKeySettingsAsync();
                        cardKeyCount = (byte)desfireSettings.NumberOfKeys;
                        MaxNumberOfAppKeys = cardKeyCount.Value;

                        cardKeyType = ResolveDesfireKeyType(desfireSettings.KeyType.ToString(), _keyTypeCurrent);
                        EncryptionType = cardKeyType.Value;
                    }
                    catch (Exception e)
                    {
                        Log.ForContext<ElatecNetProvider>().Warning(e, "Unable to read DESFire key settings after authentication.");
                    }

                    var configuredKeyType = EncryptionType;
                    if (configuredKeyType == default && _keyTypeCurrent != default)
                    {
                        configuredKeyType = _keyTypeCurrent;
                    }

                    var resolvedSettings = DesfireKeySettingsResolver.Resolve(
                        keySettings,
                        cardKeyCount,
                        cardKeyType,
                        MaxNumberOfAppKeys,
                        configuredKeyType);

                    foreach (var warning in resolvedSettings.Warnings)
                    {
                        Log.ForContext<ElatecNetProvider>().Warning("{WarningMessage}", warning);
                    }

                    MaxNumberOfAppKeys = resolvedSettings.KeyCount;
                    EncryptionType = resolvedSettings.KeyType;

                    await readerDevice.MifareDesfire_ChangeKeySettingsAsync(
                        (DESFireAppAccessRights)resolvedSettings.Settings,
                        resolvedSettings.KeyCount,
                        (Elatec.NET.Cards.Mifare.DESFireKeyType)Enum.Parse(
                            typeof(Elatec.NET.Cards.Mifare.DESFireKeyType),
                            Enum.GetName(typeof(DESFireKeyType), resolvedSettings.KeyType)
                            )
                        );

                    return ERROR.NoError;
                }
                catch
                {
                    return ERROR.AuthFailure;
                }
            }

            else
            {
                return ERROR.TransportError;
            }
            }
            finally
            {
                _comPortLock.Release();
            }
        }

        /// <inheritdoc />
        public async override Task<ERROR> ChangeMifareDesfireFileSettings(string changeKeyHex, DESFireKeyType changeKeyType, int changeKeyNo, DESFireAccessRights newAccessRights, EncryptionMode newEncMode, int appId = 0, int fileNo = 0)
        {
            await _comPortLock.WaitAsync();
            try
            {
                if (readerDevice.IsConnected)
                {
                    try { await readerDevice.SearchTagAsync(); } catch { }

                    try
                    {
                        await readerDevice.MifareDesfire_SelectApplicationAsync((uint)appId);

                        await readerDevice.MifareDesfire_AuthenticateAsync(
                            changeKeyHex,
                            (byte)changeKeyNo,
                            (byte)(int)Enum.Parse(typeof(Elatec.NET.Cards.Mifare.DESFireKeyType),
                            Enum.GetName(typeof(DESFireKeyType), changeKeyType)),
                            1);

                        var rawSettings = await readerDevice.MifareDesfire_GetFileSettingsAsync((byte)fileNo);
                        if (rawSettings == null)
                        {
                            return ERROR.AuthFailure;
                        }

                        var oldAR = rawSettings.accessRights;
                        var newAR = BuildDesfireAccessRights(newAccessRights);

                        await readerDevice.MifareDesfire_ChangeFileSettingsAsync(
                            (byte)fileNo,
                            (Elatec.NET.Cards.Mifare.EncryptionMode)newEncMode,
                            oldAR,
                            newAR);

                        return ERROR.NoError;
                    }
                    catch
                    {
                        return ERROR.TransportError;
                    }
                }
                else
                {
                    return ERROR.TransportError;
                }
            }
            finally
            {
                _comPortLock.Release();
            }
        }

        /// <inheritdoc />
        public async override Task<ERROR> DeleteMifareDesfireApplication(string _applicationMasterKey, DESFireKeyType _keyType, uint _appID, DesfireDeleteAuthMethod _authMethod = DesfireDeleteAuthMethod.PiccMasterKey)
        {
            await _comPortLock.WaitAsync();
            try
            {
                if (!readerDevice.IsConnected)
                {
                    return ERROR.TransportError;
                }

                try
                {
                    await readerDevice.SearchTagAsync();
                }
                catch
                {
                    return ERROR.TransportError;
                }

                // For PICC-level auth: select AID=0 (appId=0).
                // For app-level auth: select the target AID so the card accepts deletion with the app master key.
                var authAppId = _authMethod == DesfireDeleteAuthMethod.ApplicationMasterKey0 ? (int)_appID : 0;

                var authResult = await AuthToMifareDesfireApplicationCore(_applicationMasterKey, _keyType, 0, authAppId);
                if (authResult != ERROR.NoError)
                {
                    return authResult;
                }

                try
                {
                    await readerDevice.MifareDesfire_DeleteApplicationAsync(_appID);
                    return ERROR.NoError;
                }
                catch
                {
                    // Some ELATEC firmware/SDK combinations throw after the card has already
                    // completed the delete. Only accept that ambiguous outcome after a fresh,
                    // unauthenticated PICC inventory proves the exact target AID is absent.
                    return await VerifyApplicationAbsentAfterDeleteAsync(_appID)
                        ? ERROR.NoError
                        : ERROR.AuthFailure;
                }
            }
            finally
            {
                _comPortLock.Release();
            }
        }

        private async Task<bool> VerifyApplicationAbsentAfterDeleteAsync(uint appId)
        {
            Exception lastException = null;
            const int maxAttempts = 3;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    var tag = await readerDevice.SearchTagAsync();
                    if (tag == null)
                    {
                        throw new InvalidOperationException("SearchTag did not return a tag while verifying DeleteApplication.");
                    }

                    await readerDevice.MifareDesfire_SelectApplicationAsync(0);
                    var applicationIds = await readerDevice.MifareDesfire_GetAppIDsAsync();
                    if (applicationIds != null && !applicationIds.Contains(appId))
                    {
                        Log.ForContext<ElatecNetProvider>().Warning(
                            "DeleteApplication for AppId {AppId} reported an error, but a fresh PICC inventory proves the target application is absent; accepting the verified final state.",
                            appId);
                        return true;
                    }

                    return false;
                }
                catch (Exception exception)
                {
                    lastException = exception;
                    if (attempt < maxAttempts)
                    {
                        await Task.Delay(100);
                    }
                }
            }

            if (lastException != null)
            {
                Log.ForContext<ElatecNetProvider>().Warning(lastException,
                    "Unable to verify final PICC inventory after DeleteApplication for AppId {AppId}.", appId);
            }
            return false;
        }

        /// <inheritdoc />
        public async override Task<ERROR> DeleteMifareDesfireFile(string _applicationMasterKey, DESFireKeyType _keyType, int _appID, int _fileID)
        {
            await _comPortLock.WaitAsync();
            try
            {
            if (readerDevice.IsConnected)
            {
                try
                {
                    await readerDevice.SearchTagAsync();
                }
                catch
                {
                    return ERROR.TransportError;
                }

                try
                {
                    if (await AuthToMifareDesfireApplicationCore(_applicationMasterKey, _keyType, 0, _appID) == ERROR.NoError)
                    {
                        await readerDevice.MifareDesfire_DeleteFileAsync((byte)_fileID);
                    }
                    else
                    {
                        return ERROR.AuthFailure;
                    }
                }
                catch
                {
                    return ERROR.AuthFailure;
                }
                return ERROR.NoError;
            }

            else
            {
                return ERROR.TransportError;
            }
            }
            finally
            {
                _comPortLock.Release();
            }
        }

        /// <inheritdoc />
        public async override Task<ERROR> FormatDesfireCard(string _applicationMasterKey, DESFireKeyType _keyType)
        {
            await _comPortLock.WaitAsync();
            try
            {
            if (readerDevice.IsConnected)
            {
                try
                {
                    await readerDevice.SearchTagAsync();
                }
                catch
                {
                    return ERROR.TransportError;
                }

                try
                {
                    if (await AuthToMifareDesfireApplicationCore(_applicationMasterKey, _keyType, 0, 0) == ERROR.NoError)
                    {
                        await readerDevice.MifareDesfire_FormatTagAsync();
                    }
                    else
                    {
                        return ERROR.AuthFailure;
                    }
                }

                catch
                {
                    return ERROR.AuthFailure;
                }

                return ERROR.NoError;
            }

            else
            {
                return ERROR.TransportError;
            }
            }
            finally
            {
                _comPortLock.Release();
            }
        }

        /// <inheritdoc />
        public async override Task<ERROR> GetMifareDesfireFileList(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent, int _appID)
        {
            await _comPortLock.WaitAsync();
            try
            {
            if (readerDevice.IsConnected)
            {
                try { await readerDevice.SearchTagAsync(); } catch { }

                try
                {
                    await readerDevice.MifareDesfire_SelectApplicationAsync((uint)_appID);

                    try
                    {
                        await readerDevice.MifareDesfire_AuthenticateAsync(_applicationMasterKey, (byte)_keyNumberCurrent, (byte)(int)Enum.Parse(typeof(Elatec.NET.Cards.Mifare.DESFireKeyType), Enum.GetName(typeof(DESFireKeyType), _keyType)), DESFIRE_AUTHMODE_EV1);
                    } // try to auth first.
                    catch
                    {

                    }

                    var fids = await readerDevice.MifareDesfire_GetFileIDsAsync();

                    if (fids != null)
                    {
                        FileIDList = fids;
                    }
                }
                catch
                {
                    return ERROR.TransportError;
                }

                return ERROR.NoError;
            }

            else
            {
                return ERROR.TransportError;
            }
            }
            finally
            {
                _comPortLock.Release();
            }
        }

        /// <inheritdoc />
        public async override Task<ERROR> GetMifareDesfireFileSettings(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumberCurrent, int _appID, int _fileNo)
        {
            await _comPortLock.WaitAsync();
            try
            {
            if (readerDevice.IsConnected)
            {
                try { await readerDevice.SearchTagAsync(); } catch { }

                try
                {
                    await readerDevice.MifareDesfire_SelectApplicationAsync((uint)_appID);

                    try
                    {
                        await readerDevice.MifareDesfire_AuthenticateAsync(
                            _applicationMasterKey,
                            (byte)_keyNumberCurrent,
                            (byte)(int)Enum.Parse(typeof(Elatec.NET.Cards.Mifare.DESFireKeyType),
                            Enum.GetName(typeof(DESFireKeyType), _keyType)),
                            1);
                    } // try to auth first.
                    catch
                    {

                    }

                    var fileSettings = await readerDevice.MifareDesfire_GetFileSettingsAsync((byte)_fileNo);

                    if (fileSettings != null)
                    {
                        DesfireFileSettings = new DESFireFileSettings
                        {
                            FileType = (byte)fileSettings.FileType,
                            comSett = fileSettings.ComSett
                        };
                        DesfireFileSettings.dataFile.fileSize = fileSettings.DataFileSetting != null ? fileSettings.DataFileSetting.FileSize : 0;
                        DesfireFileSettings.accessRights = new byte[2];
                        DesfireFileSettings.accessRights[0] |= fileSettings.accessRights.ReadKeyNo;
                        DesfireFileSettings.accessRights[0] |= (byte)(fileSettings.accessRights.WriteKeyNo << 4);
                        DesfireFileSettings.accessRights[1] |= fileSettings.accessRights.ReadWriteKeyNo;
                        DesfireFileSettings.accessRights[1] |= (byte)(fileSettings.accessRights.ChangeKeyNo << 4);

                        return ERROR.NoError;
                    }
                    else
                    {
                        return ERROR.AuthFailure;
                    }
                }
                catch
                {
                    return ERROR.TransportError;
                }
            }

            else
            {
                return ERROR.TransportError;
            }
            }
            finally
            {
                _comPortLock.Release();
            }
        }

        /// <summary>
        /// Creates a DESFire file on the selected application.
        /// </summary>
        /// <remarks>
        /// Backup file creation prefers a dedicated Elatec API if available and falls back to the standard data file creation
        /// path with <see cref="DESFireFileType.BackupFile"/> when the API is unavailable.
        /// </remarks>
        public async override Task<ERROR> CreateMifareDesfireFile(string _appMasterKey, DESFireKeyType _keyTypeAppMasterKey, FileType_MifareDesfireFileType _fileType, DESFireAccessRights _accessRights, EncryptionMode _encMode,
                                     int _appID, int _fileNo, int _fileSize,
                                     int _minValue = 0, int _maxValue = 1000, int _initValue = 0, bool _isValueLimited = false,
                                     int _maxNbOfRecords = 100)
        {
            await _comPortLock.WaitAsync();
            try
            {
            if (IsConnected)
            {
                try
                {
                    var authResult = await AuthToMifareDesfireApplicationCore(_appMasterKey, _keyTypeAppMasterKey, 0, _appID);
                    if (authResult != ERROR.NoError)
                    {
                        return authResult;
                    }

                    var ar = BuildDesfireAccessRights(_accessRights);

                    switch (_fileType)
                    {
                        case FileType_MifareDesfireFileType.StdDataFile:
                            try
                            {
                                await CreateStdDataFileAsync((byte)_fileNo, _fileType, _encMode, ar, (uint)_fileSize);
                            }
                            catch (Exception e)
                            {
                                Log.ForContext<ElatecNetProvider>().Error(e,
                                    "Elatec DESFire CreateStdDataFile failed for AppId {AppId} FileNo {FileNo} Size {FileSize}.",
                                    _appID, _fileNo, _fileSize);
                                return ERROR.PermissionDenied;
                            }

                            break;

                        case FileType_MifareDesfireFileType.BackupFile:
                            try
                            {
                                await CreateBackupFileAsync((byte)_fileNo, _encMode, ar, (uint)_fileSize);
                            }
                            catch (Exception e)
                            {
                                Log.ForContext<ElatecNetProvider>().Error(e,
                                    "Elatec DESFire CreateBackupFile failed for AppId {AppId} FileNo {FileNo} Size {FileSize}.",
                                    _appID, _fileNo, _fileSize);
                                return ERROR.PermissionDenied;
                            }

                            break;

                        default:
                            return ERROR.ProtocolConstraint;
                    }
                }
                catch (Exception e)
                {
                    Log.ForContext<ElatecNetProvider>().Error(e, "Elatec operation failed.");
                    return ERROR.TransportError;
                }

                return ERROR.NoError;
            }

            else
            {
                return ERROR.TransportError;
            }
            }
            finally
            {
                _comPortLock.Release();
            }
        }

        private static DESFireFileAccessRights BuildDesfireAccessRights(DESFireAccessRights accessRights)
        {
            return new DESFireFileAccessRights
            {
                ReadKeyNo = (byte)accessRights.readAccess,
                WriteKeyNo = (byte)accessRights.writeAccess,
                ReadWriteKeyNo = (byte)accessRights.readAndWriteAccess,
                ChangeKeyNo = (byte)accessRights.changeAccess
            };
        }

        protected virtual Task CreateStdDataFileAsync(byte fileNo, FileType_MifareDesfireFileType fileType, EncryptionMode encMode, DESFireFileAccessRights accessRights, uint fileSize)
        {
            return readerDevice.MifareDesfire_CreateStdDataFileAsync(
                fileNo,
                (DESFireFileType)fileType,
                (Elatec.NET.Cards.Mifare.EncryptionMode)encMode,
                accessRights,
                fileSize);
        }

        protected virtual Task<byte[]> ReadDesfireDataAsync(byte fileNo, int fileSize, EncryptionMode encMode)
        {
            return readerDevice.MifareDesfire_ReadDataAsync(
                fileNo,
                fileSize,
                (Elatec.NET.Cards.Mifare.EncryptionMode)encMode);
        }

        protected virtual Task WriteDesfireDataAsync(byte fileNo, byte[] data, EncryptionMode encMode)
        {
            return readerDevice.MifareDesfire_WriteDataAsync(
                fileNo,
                data,
                (Elatec.NET.Cards.Mifare.EncryptionMode)encMode);
        }

        protected virtual async Task CreateBackupFileAsync(byte fileNo, EncryptionMode encMode, DESFireFileAccessRights accessRights, uint fileSize)
        {
            var backupMethod = readerDevice?.GetType().GetMethod("MifareDesfire_CreateBackupFileAsync");
            if (backupMethod != null && TryBuildBackupMethodArguments(backupMethod, fileNo, encMode, accessRights, fileSize, out var arguments))
            {
                var result = backupMethod.Invoke(readerDevice, arguments);
                if (result is Task task)
                {
                    await task;
                    return;
                }
            }

            await CreateStdDataFileAsync(fileNo, FileType_MifareDesfireFileType.BackupFile, encMode, accessRights, fileSize);
        }

        private static bool TryBuildBackupMethodArguments(MethodInfo backupMethod, byte fileNo, EncryptionMode encMode, DESFireFileAccessRights accessRights, uint fileSize, out object[] arguments)
        {
            var parameters = backupMethod.GetParameters();

            if (parameters.Length == 4
                && parameters[0].ParameterType == typeof(byte)
                && parameters[1].ParameterType == typeof(Elatec.NET.Cards.Mifare.EncryptionMode)
                && parameters[2].ParameterType == typeof(DESFireFileAccessRights)
                && parameters[3].ParameterType == typeof(uint))
            {
                arguments = new object[]
                {
                    fileNo,
                    (Elatec.NET.Cards.Mifare.EncryptionMode)encMode,
                    accessRights,
                    fileSize
                };
                return true;
            }

            if (parameters.Length == 5
                && parameters[0].ParameterType == typeof(byte)
                && parameters[1].ParameterType == typeof(DESFireFileType)
                && parameters[2].ParameterType == typeof(Elatec.NET.Cards.Mifare.EncryptionMode)
                && parameters[3].ParameterType == typeof(DESFireFileAccessRights)
                && parameters[4].ParameterType == typeof(uint))
            {
                arguments = new object[]
                {
                    fileNo,
                    DESFireFileType.DF_FT_BACKUPDATAFILE,
                    (Elatec.NET.Cards.Mifare.EncryptionMode)encMode,
                    accessRights,
                    fileSize
                };
                return true;
            }

            arguments = Array.Empty<object>();
            return false;
        }

        /// <inheritdoc />
        public async override Task<ERROR> ReadMiFareDESFireChipFile(string _appReadKey, DESFireKeyType _keyTypeAppReadKey, int _readKeyNo,
                                       EncryptionMode _encMode,
                                       int _fileNo, int _appID, int _fileSize)
        {
            await _comPortLock.WaitAsync();
            try
            {
                try
                {
                    // Exactly one complete boundary: SearchTag (TWN4 LEGIC) -> Select -> Authenticate -> Read.
                    var authResult = await AuthToMifareDesfireApplicationCore(_appReadKey, _keyTypeAppReadKey, _readKeyNo, _appID);
                    if (authResult != ERROR.NoError)
                    {
                        return authResult;
                    }

                    MifareDESFireData = await ReadDesfireDataAsync((byte)_fileNo, _fileSize, _encMode);
                    return MifareDESFireData != null ? ERROR.NoError : ERROR.Unknown;
                }
                catch (Exception e)
                {
                    Log.ForContext<ElatecNetProvider>().Error(e,
                        "Elatec DESFire ReadData failed for AppId {AppId} FileNo {FileNo} Size {FileSize} Mode {Mode}.",
                        _appID, _fileNo, _fileSize, _encMode);
                    return ERROR.PermissionDenied;
                }
            }
            finally
            {
                _comPortLock.Release();
            }
        }

        /// <inheritdoc />
        public async override Task<ERROR> WriteMiFareDESFireChipFile(string _appWriteKey, DESFireKeyType _keyTypeAppWriteKey, int _writeKeyNo,
                                        EncryptionMode _encMode,
                                        int _fileNo, int _appID, byte[] _data)
        {
            await _comPortLock.WaitAsync();
            try
            {
                try
                {
                    // Exactly one complete boundary: SearchTag (TWN4 LEGIC) -> Select -> Authenticate -> Write.
                    var authResult = await AuthToMifareDesfireApplicationCore(_appWriteKey, _keyTypeAppWriteKey, _writeKeyNo, _appID);
                    if (authResult != ERROR.NoError)
                    {
                        return authResult;
                    }

                    await WriteDesfireDataAsync((byte)_fileNo, _data, _encMode);
                }
                catch (Exception e)
                {
                    Log.ForContext<ElatecNetProvider>().Error(e,
                        "Elatec DESFire WriteData failed for AppId {AppId} FileNo {FileNo} Size {FileSize} Mode {Mode}.",
                        _appID, _fileNo, _data?.Length ?? 0, _encMode);
                    return ERROR.PermissionDenied;
                }

                return ERROR.NoError;
            }
            finally
            {
                _comPortLock.Release();
            }
        }

        #endregion

        #region SmartMX Detection

        /// <summary>
        /// Attempts an ISO 14443-4 VersionL4 activation to detect SmartMX DESFire emulation.
        /// </summary>
        /// <remarks>
        /// On MultiTech (CCF) readers the card must be re-selected via SearchMultiTag and SelectTag
        /// before the L4 exchange succeeds. On Legic (CCB) readers the SearchTag context is
        /// sufficient. Returns null when the card does not respond as a DESFire emulation or when
        /// communication fails (non-fatal; caller falls back to Classic classification).
        /// </remarks>
        private async Task<byte[]?> TryDetectSmartMXDesFireEmulationAsync(byte[] uidBytes)
        {
            try
            {
                if (IsMultiTechReader)
                {
                    var uids = await readerDevice.ISO14443A_SearchMultiTagAsync();
                    if (uids == null || uids.Count == 0)
                    {
                        return null;
                    }

                    var target = uids.FirstOrDefault(u =>
                        u.Length == uidBytes.Length && u.SequenceEqual(uidBytes)) ?? uids[0];

                    if (!await readerDevice.ISO14443A_SelectTagAsync(target))
                    {
                        return null;
                    }
                }

                var version = await readerDevice.ISO14443_4_TdxAsync(new byte[] { 0x60 });

                // Response: [0]=AF (more frames), [1]=04 (NXP vendor), [2]=type (0x01=native, 0x81=emulated)
                if (version == null || version.Length < 7 || version[2] != 0x81)
                {
                    return null;
                }

                return version;
            }
            catch (Exception e)
            {
                Log.ForContext<ElatecNetProvider>().Debug(e, "SmartMX DESFire emulation probe failed (non-fatal).");
                return null;
            }
        }

        /// <summary>
        /// Maps a raw VersionL4 GetVersion response (0x60 command) to the matching SmartMX DESFire CARD_TYPE.
        /// </summary>
        /// <remarks>
        /// Mirrors the EV-generation and storage-size decoding in the Elatec SDK DetectMifareSubType.
        /// version[4] low nibble = EV generation (0=EV0, 1=EV1, 2=EV2, 3=EV3).
        /// version[6] = storage size byte (0x10=256B, 0x16=2K, 0x18=4K, 0x1A=8K, 0x1C=16K, 0x1E=32K).
        /// </remarks>
        private static CARD_TYPE MapSmartMXVersionToCardType(byte[] version)
        {
            int evGen = version[4] & 0x0F;
            byte sizeByte = version[6];

            return (evGen, sizeByte) switch
            {
                (0, 0x10) => CARD_TYPE.SmartMX_DESFireEV0_256,
                (0, _)    => CARD_TYPE.SmartMX_DESFireEV0_4K,

                (1, 0x10) => CARD_TYPE.SmartMX_DESFireEV1_256,
                (1, 0x16) => CARD_TYPE.SmartMX_DESFireEV1_2K,
                (1, 0x18) => CARD_TYPE.SmartMX_DESFireEV1_4K,
                (1, _)    => CARD_TYPE.SmartMX_DESFireEV1_8K,

                (2, 0x16) => CARD_TYPE.SmartMX_DESFireEV2_2K,
                (2, 0x18) => CARD_TYPE.SmartMX_DESFireEV2_4K,
                (2, 0x1A) => CARD_TYPE.SmartMX_DESFireEV2_8K,
                (2, 0x1C) => CARD_TYPE.SmartMX_DESFireEV2_16K,
                (2, _)    => CARD_TYPE.SmartMX_DESFireEV2_32K,

                (3, 0x16) => CARD_TYPE.SmartMX_DESFireEV3_2K,
                (3, 0x18) => CARD_TYPE.SmartMX_DESFireEV3_4K,
                (3, 0x1A) => CARD_TYPE.SmartMX_DESFireEV3_8K,
                (3, 0x1C) => CARD_TYPE.SmartMX_DESFireEV3_16K,
                (3, _)    => CARD_TYPE.SmartMX_DESFireEV3_32K,

                _ => CARD_TYPE.SmartMX_DESFire,
            };
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {

                }

                _disposed = true;
            }
        }

        public override void Dispose()
        {
            _disposed = false;
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
