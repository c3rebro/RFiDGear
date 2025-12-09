using System;
using System.IO.Ports;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using Elatec.NET.Cards;
using Elatec.NET.Cards.Mifare;
using Elatec.NET.Helpers.ByteArrayHelper.Extensions;
using System.IO;

/*
* Elatec.NET is a C# library to easily Talk to Elatec's TWN4 Devices
* 
* Some TWN4 Specific "Special" information:
* 
* Getting the ATS on different Readers works differently.
* 
*/

namespace Elatec.NET
{
    /// <summary>
    ///     This class offers communications methods with a TWN4 Reader device, e.g. TWN4 MultiTech 2.
    ///     The methods are on different abstraction levels:<br/>
    ///     <list type="number">
    ///     <item>Low level TWN4 Simple Protocol APIs<br/>
    ///     - <see cref="CallFunctionAsync(byte[])"/> which takes a raw byte[] as input and returns a parser. Errors are thrown as TwnException.<br />
    ///     - See <see cref="CallFunctionParserAsync(byte[])"/> and <see cref="CallFunctionRawAsync(byte[])"/> for variants without error handling and parser.</item>
    ///     <item>TWN4 Simple Protocol APIs<br/>
    ///     - e.g. <see cref="GpioSetBitsAsync(Gpios)"/></item>
    ///     <item>High level<br/>
    ///     - e.g. <see cref="GetSingleChipAsync(bool)"/> providing detailed information</item>
    ///     </list>
    /// </summary>
    public class TWN4ReaderDevice : IDisposable
    {
        private bool _disposed;

        private static readonly object syncRoot = new object();
        private static List<TWN4ReaderDevice> instance;
        private SerialPort twnPort;

        public static List<TWN4ReaderDevice> Instance
        {
            get
            {
                lock (TWN4ReaderDevice.syncRoot)
                {
                    instance = DeviceManager.GetAvailableReaders();

                    if (instance == null)
                    {
                        return new List<TWN4ReaderDevice>();
                    }

                    return instance;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public TWN4ReaderDevice(string portName)
        {
            PortName = portName;
        }

        /// <summary>
        /// 
        /// </summary>
        public static readonly int TIMEOUT = 20;

        #region Low level APIs

        #region API_SYS / System Functions

        public static readonly byte API_SYS = 0;

        // Not supported: SYSFUNC(API_SYS, 0, bool SysCall(TEnvSysCall* Env))

        /// <summary>
        /// This function is performing a reset of the firmware, which also includes a restart of the currently running App.
        /// </summary>
        /// <returns></returns>
        public async Task ResetAsync()
        {
            await CallFunctionAsync(new byte[] { API_SYS, 1 });
        }

        /// <summary>
        /// This function is performing a manual call of the boot loader. As a consequence the execution of the App is stopped.
        /// </summary>
        /// <returns></returns>
        public async Task StartBootloaderAsync()
        {
            await CallFunctionAsync(new byte[] { API_SYS, 2 });
        }

        /// <summary>
        /// Retrieve number of system ticks, specified in multiple of 1 milliseconds, since startup of the firmware.
        /// </summary>
        /// <returns></returns>
        public async Task<uint> GetSysTicksAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_SYS, 3 });
            uint ticks = parser.ParseUInt32();
            return ticks;
        }

        /// <summary>
        /// Retrieve version information.
        /// </summary>
        /// <returns></returns>
        public async Task<string> GetVersionStringAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_SYS, 4, /* maxLen */ byte.MaxValue });
            string version = parser.ParseAsciiString();
            var subVersion = version.Split('/');
            IsTWN4LegicReader = subVersion.Length >= 3 && subVersion[2].Contains('B');
            return version;
        }

        /// <summary>
        ///     Retrieve type of USB communication. This could by keyboard emulation or CDC emulation or some other
        ///     value for future or custom implementations.
        /// </summary>
        /// <returns></returns>
        public async Task<UsbType> GetUsbTypeAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_SYS, 5 });
            var type = (UsbType)parser.ParseByte();
            return type;
        }

        /// <summary>
        /// Retrieve type of underlying TWN4 hardware.
        /// </summary>
        /// <returns></returns>
        public async Task<DeviceType> GetDeviceTypeAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_SYS, 6 });
            var type = (DeviceType)parser.ParseByte();
            return type;
        }

        /// <summary>
        ///     The device enters the sleep state for a specified time. During sleep state, the device reduces the current
        ///     consumption to a value, which depends on the mode of sleep.
        /// </summary>
        /// <param name="ticks">Time, specified in milliseconds, the device should enter the sleep state.</param>
        /// <param name="flags">See TWN4 API Reference.</param>
        /// <returns>See TWN4 API Reference.</returns>
        public async Task<byte> SleepAsync(uint ticks, uint flags)
        {
            List<byte> bytes = new List<byte> { API_RF, 7 };
            bytes.AddUInt32(ticks);
            bytes.AddUInt32(flags);
            var parser = await CallFunctionAsync(bytes.ToArray());
            var result = parser.ParseByte();
            return result;
        }

        /// <summary>
        /// This function returns a UID, which is unique to the specific TWN4 device.
        /// </summary>
        /// <returns></returns>
        public async Task<byte[]> GetDeviceUidAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_SYS, 8 });
            byte[] result = parser.ParseFixByteArray(12);
            return result;
        }

        /// <summary>
        ///     This function allows to set parameters, which influence the behaviour of the TWN4 firmware. See also
        ///     chapter System Parameters of TWN4 API Reference for a description of the TLV list and all available parameters.
        /// </summary>
        /// <param name="TLV">The raw bytes of the TLV list. Do not include TLV_END, as it is appended automatically!</param>
        /// <returns>The function returns true, if the parameters were set to the new value. Otherwise
        ///     the function returns false.</returns>
        /// <remarks>SYSFUNC(API_SYS, 9, bool SetParameters(const byte* TLV,int ByteCount))</remarks>
        public async Task<bool> SetParametersAsync(byte[] TLV)
        {
            List<byte> bytes = new List<byte> { API_SYS, 9 };
            bytes.Add((byte)(TLV.Length + 1));
            bytes.AddRange(TLV);
            bytes.Add(0); // TLV_END
            var parser = await CallFunctionAsync(bytes.ToArray());
            var result = parser.ParseBool();
            return result;
        }

        /// <summary>
        /// This function is used to retrieve internal system errors of the reader. Do not deduce protocol or communication errors from this function call.
        /// </summary>
        /// <returns></returns>
        /// <remarks>SYSFUNC(API_SYS,10, unsigned int GetLastError(void))</remarks>
        public async Task<ReaderError> GetLastErrorAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_SYS, 10 });
            var result = (ReaderError)parser.ParseUInt32();
            return result;
        }

        // Not supported: SYSFUNC(API_SYS,11, int Diagnostic(int Mode,const void* In,int InLen,void* Out,int* OutLen,int MaxOutLen))

        /// <summary>
        /// Get the product serial number of the TWN device.
        /// </summary>
        /// <returns></returns>
        public async Task<string> GetProdSerNoAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_SYS, 13, /* maxBytes */ byte.MaxValue });
            string result = parser.ParseAsciiString();
            return result;
        }

        // Not supported: SYSFUNC(API_SYS,14, bool SetInterruptHandler(TInterruptHandler InterruptHandler, int IntNo))

        /// <summary>
        /// Retrieve version information.
        /// </summary>
        /// <returns></returns>
        /// <remarks>SYSFUNC(API_SYS,15, void GetVersionInfo(TVersionInfo* VersionInfo)).<br/>
        ///     This internal method is not documented in TWN4 API reference.
        /// </remarks>
        public async Task<VersionInfo> GetVersionInfoAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_SYS, 15 });
            var info = new VersionInfo();
            info.Compatibility = parser.ParseUInt16();
            info.BootBranch = parser.ParseUInt16();
            var minor = parser.ParseByte();
            var major = parser.ParseByte();
            info.BootVersion = new Version(major, minor);
            info.FirmwareKeyType = parser.ParseUInt16();
            info.BranchNum = parser.ParseByte();
            info.BranchChar = (char)parser.ParseByte();
            minor = parser.ParseByte();
            major = parser.ParseByte();
            info.FirmwareVersion = new Version(major, minor);
            info.AppChars = parser.ParseFixByteArray(4);
            minor = parser.ParseByte();
            major = parser.ParseByte();
            info.AppVersion = new Version(major, minor);

            return info;
        }

        public class VersionInfo
        {
            public int Compatibility { get; set; }
            public int BootBranch { get; set; }
            public Version BootVersion { get; set; }
            public int FirmwareKeyType { get; set; }
            public byte BranchNum { get; set; }
            /// <summary>
            /// 'K' = Keyboard, 'C' = CDC
            /// </summary>
            public char BranchChar { get; set; }
            public Version FirmwareVersion { get; set; }
            /// <summary>
            /// e.g. "STD", "STDC", "PRS" = Simple Protocol
            /// </summary>
            public byte[] AppChars { get; set; }
            public Version AppVersion { get; set; }
        }

        // Not supported: SYSFUNC(API_SYS,16, bool ReadInfoValue(int Index, int FilterType, int* Type, int* Length, byte* Value, int MaxLength))
        // Not supported: SYSFUNC(API_SYS,17, bool WriteInfoValue(int Type, int Length,const byte* Value))
        // Not supported: SYSFUNC(API_SYS,18, bool GetCustomKeyID(byte* CustomKeyID, int* Length, int MaxLength))
        // Not supported: SYSFUNC(API_SYS,19, bool GetParameters(const byte* Types,int TypeCount,byte* TLVBytes,int* TLVByteCount,int TLVMaxByteCount))

        #endregion

        #region API_PERIPH / Periphery Functions

        public static readonly byte API_PERIPH = 4;

        /// <summary>
        ///     Use this function to configure one or several GPIOs as output. Each output can be configured to have an
        ///     integrated pull-up or pull-down resistor.The output driver characteristic is either Push-Pull or Open Drain.
        /// </summary>
        /// <param name="bits">Specify the GPIOs that shall be configured for output. Several GPIOs can
        ///     be configured simultaneously by using the bitwise or-operator (|).</param>
        /// <param name="pullUpDown">Specify the behaviour of the internal weak pull-up/down resistor.</param>
        /// <param name="outputType">Specify the output driver characteristic.</param>
        /// <returns></returns>
        public async Task GpioConfigureOutputsAsync(Gpios bits, GpioPullType pullUpDown, GpioOutputType outputType)
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 0, (byte)bits, (byte)pullUpDown, (byte)outputType });
        }

        /// <summary>
        ///     Use this function to configure one or several GPIOs as input. Each output can be configured to have an
        ///     integrated pull-up or pull-down resistor, alternatively it can be left floating.
        /// </summary>
        /// <param name="bits">Specify the GPIOs that shall be configured for input. Several GPIOs can
        ///     be configured simultaneously by using the bitwise or-operator (|).</param>
        /// <param name="pullUpDown">Specify the behaviour of the internal weak pull-up/down resistor.</param>
        /// <returns></returns>
        public async Task GpioConfigureInputsAsync(Gpios bits, GpioPullType pullUpDown)
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 1, (byte)bits, (byte)pullUpDown });
        }

        /// <summary>
        ///     Use this function to set one or several GPIOs to logical high level.
        ///     The respective ports must have been configured to output in advance.
        /// </summary>
        /// <param name="bits">Specify the GPIOs that shall be set to a logical level. Several GPIOs can
        ///     be handled simultaneously by using the bitwise or-operator (|).</param>
        /// <returns></returns>
        public async Task GpioSetBitsAsync(Gpios bits)
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 2, (byte)bits });
        }

        /// <summary>
        ///     Use this function to set one or several GPIOs to logical low level.
        ///     The respective ports must have been configured to output in advance.
        /// </summary>
        /// <param name="bits">Specify the GPIOs that shall be set to a logical level. Several GPIOs can
        ///     be handled simultaneously by using the bitwise or-operator (|).</param>
        /// <returns></returns>
        public async Task GpioClearBitsAsync(Gpios bits)
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 3, (byte)bits });
        }

        /// <summary>
        ///     Use this function to toggle the logical level of one or several GPIOs.
        ///     The respective ports must have been configured to output in advance.
        /// </summary>
        /// <param name="bits">Specify the GPIOs that shall be set to a logical level. Several GPIOs can
        ///     be handled simultaneously by using the bitwise or-operator (|).</param>
        /// <returns></returns>
        public async Task GpioToggleBitsAsync(Gpios bits)
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 4, (byte)bits });
        }

        /// <summary>
        ///     Use this function to generate a pulse-width modulated square waveform with constant frequency on one
        ///     or several GPIOs. The respective ports must have been configured to output in advance.
        /// </summary>
        /// <param name="bits">Specify the GPIOs that shall generate the waveform.</param>
        /// <param name="timeHi">Specify the duration for logical high level in milliseconds.</param>
        /// <param name="timeLo">Specify the duration for logical low level in milliseconds.</param>
        /// <returns></returns>
        public async Task GpioBlinkBitsAsync(Gpios bits, ushort timeHi, ushort timeLo)
        {
            List<byte> bytes = new List<byte> { API_PERIPH, 5 };
            bytes.Add((byte)bits);
            bytes.AddUInt16(timeHi);
            bytes.AddUInt16(timeLo);
            await CallFunctionAsync(bytes.ToArray());
        }

        /// <summary>
        /// Use this function to read the logical level of one GPIO that has been configured as input.
        /// </summary>
        /// <param name="bit">Specify the GPIO that shall be read.</param>
        /// <returns>If the GPIO has logical high level, the return value is 1, otherwise it is 0.</returns>
        public async Task<bool> GpioTestBitAsync(Gpios bit)
        {
            var parser = await CallFunctionAsync(new byte[] { API_PERIPH, 6, (byte)bit });
            var result = parser.ParseBool();
            return result;
        }

        /// <summary>
        /// Play a beep on the device.
        /// </summary>
        /// <param name="volume">Specify the volume in percent from 0 to 100.</param>
        /// <param name="frequency">Specify the frequency in Hertz from 500 to 10000.</param>
        /// <param name="onTime">Specify the duration of the beep in milliseconds from 0 to 10000000.</param>
        /// <param name="offTime">Specify the length of the pause after the beep in milliseconds from 0 to
        ///     10000000. This is useful for generating melodies.If this is not required, the
        ///     parameter may have the value 0.</param>
        /// <returns></returns>
        public async Task BeepAsync(byte volume, ushort frequency, ushort onTime, ushort offTime)
        {
            List<byte> bytes = new List<byte> { API_PERIPH, 7 };
            bytes.Add(volume);
            bytes.AddUInt16(frequency);
            bytes.AddUInt16(onTime);
            bytes.AddUInt16(offTime);
            await CallFunctionAsync(bytes.ToArray());
        }

        public async Task DiagLedOnAsync()
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 8 });
        }

        public async Task DiagLedOffAsync()
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 9 });
        }

        public async Task DiagLedToggleAsync()
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 10 });
        }

        public async Task<bool> DiagLedIsOnAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_PERIPH, 11 });
            var result = parser.ParseBool();
            return result;
        }

        // TODO: SYSFUNC(API_PERIPH,12, void SendWiegand(int GPIOData0, int GPIOData1, int PulseTime, int IntervalTime,const byte* Bits,int BitCount))
        // TODO: SYSFUNC(API_PERIPH,13, void SendOmron(int GPIOClock, int GPIOData, int T1, int T2, int T3,const byte* Bits,int BitCount))
        // Not supported: SYSFUNC(API_PERIPH,14, bool GPIOPlaySequence(const int* NewSequence,int ByteCount))
        // Not supported: SYSFUNC(API_PERIPH,15, void GPIOStopSequence(void))

        /// <summary>
        /// Use this function to initialize the respective GPIOs to drive LEDs.
        /// </summary>
        /// <param name="leds">Specify the GPIOs that shall be configured for LED operation.</param>
        /// <returns></returns>
        public async Task LedInitAsync()
        {
            await LedInitAsync(Leds.All);
        }

        /// <summary>
        /// Use this function to initialize the respective GPIOs to drive LEDs.
        /// </summary>
        /// <param name="leds">Specify the GPIOs that shall be configured for LED operation.</param>
        /// <returns></returns>
        public async Task LedInitAsync(Leds leds)
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 16, (byte)leds });
        }

        /// <summary>
        /// Use this function to set one or several LEDs on.
        /// </summary>
        /// <param name="leds">Specify the LEDs that shall be set on.</param>
        /// <returns></returns>
        public async Task LedOnAsync(Leds leds)
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 17, (byte)leds });
        }

        /// <summary>
        /// Use this function to set one or several LEDs off.
        /// </summary>
        /// <param name="leds">Specify the LEDs that shall be set off.</param>
        /// <returns></returns>
        public async Task LedOffAsync(Leds leds)
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 18, (byte)leds });
        }

        /// <summary>
        /// Use this function to toggle one or several LEDs.
        /// </summary>
        /// <param name="leds">Specify the LEDs that shall be toggled.</param>
        /// <returns></returns>
        public async Task LedToggleAsync(Leds leds)
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 19, (byte)leds });
        }

        /// <summary>
        /// Use this function to let one or several LEDs blink.
        /// </summary>
        /// <param name="leds">Specify the LEDs that shall blink.</param>
        /// <param name="onTime">Specify the on-time in milliseconds.</param>
        /// <param name="offTime">Specify the off-time in milliseconds.</param>
        /// <returns></returns>
        public async Task LedBlinkAsync(Leds leds, ushort onTime, ushort offTime)
        {
            List<byte> bytes = new List<byte> { API_PERIPH, 20 };
            bytes.Add((byte)leds);
            bytes.AddUInt16(onTime);
            bytes.AddUInt16(offTime);
            await CallFunctionAsync(bytes.ToArray());
        }

        // Not supported: SYSFUNC(API_PERIPH,21,bool GPIOConfigureInterrupt(int GPIOBits,bool Enable,int Edge))

        /// <summary>
        /// Turn on beep with infinite length.
        /// </summary>
        /// <param name="volume">Specify the volume in percent from 0 to 100.</param>
        /// <param name="frequency">Specify the frequency in Hertz from 500 to 10000.</param>
        /// <returns></returns>
        public async Task BeepOnAsync(byte volume, ushort frequency)
        {
            List<byte> bytes = new List<byte> { API_PERIPH, 22 };
            bytes.Add(volume);
            bytes.AddUInt16(frequency);
            await CallFunctionAsync(bytes.ToArray());
        }

        /// <summary>
        /// Turn off beep.
        /// </summary>
        /// <returns></returns>
        public async Task BeepOffAsync()
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 23 });
        }

        // Not supported: SYSFUNC(API_PERIPH,24,void PlayMelody(const byte *Melody,int MelodyLength))

        #endregion

        #region API_RF

        public static readonly byte API_RF = 5;

        /// <summary>
        ///     Use this function to search a transponder in the reading range of TWN4. TWN4 is searching for all types
        ///     of transponders, which have been specified via function SetTagTypes. If a transponder has been found,
        ///     tag type, length of ID and ID data itself are returned.
        /// </summary>
        /// <returns>A SearchTagResult or <see langword="null" /> if no tag was detected.</returns>
        public async Task<SearchTagResult> SearchTagAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_RF, 0, /* maxIDBytes */ byte.MaxValue });
            var found = parser.ParseBool();
            if (found)
            {
                var tag = new SearchTagResult();
                tag.ChipType = (ChipType)parser.ParseByte();
                tag.IDBitCount = parser.ParseByte();
                tag.IDBytes = parser.ParseVarByteArray();
                return tag;
            }
            return null;
        }

        public class SearchTagResult
        {
            /// <summary>
            /// Property is called TagType in the API.
            /// </summary>
            public ChipType ChipType { get; set; }
            public byte IDBitCount { get; set; }
            public byte[] IDBytes { get; set; }

            public string IDHexString
            {
                get
                {
                    return ByteArrayConverter.GetStringFrom(IDBytes);
                }
            }
        }

        /// <summary>
        ///     Turn off RF field. If no further operations are required on a transponder found via function SearchTag you
        ///     may use this command to minimize power consumption of TWN4.
        /// </summary>
        /// <returns></returns>
        public async Task SetRFOffAsync()
        {
            await CallFunctionAsync(new byte[] { API_RF, 1 });
        }

        /// <summary>
        /// Use this function to configure the transponders, which are searched by function SearchTag.
        /// </summary>
        /// <param name="lfTagTypes"></param>
        /// <param name="hfTagTypes"></param>
        /// <returns></returns>
        public async Task SetTagTypesAsync(LFTagTypes lfTagTypes, HFTagTypes hfTagTypes)
        {
            List<byte> bytes = new List<byte> { API_RF, 2 };
            bytes.AddUInt32((uint)lfTagTypes);
            bytes.AddUInt32((uint)hfTagTypes);
            await CallFunctionAsync(bytes.ToArray());
        }

        /// <summary>
        ///     This function returns the transponder types currently being searched for by function SearchTag separated
        ///     by frequency (LF and HF).
        /// </summary>
        /// <returns>Tag types.</returns>
        public async Task<GetTagTypesResult> GetTagTypesAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_RF, 3 });
            var lf = parser.ParseUInt32();
            var hf = parser.ParseUInt32();

            return new GetTagTypesResult() { LFTagTypes = (LFTagTypes)lf, HFTagTypes = (HFTagTypes)hf };
        }

        public class GetTagTypesResult
        {
            public LFTagTypes LFTagTypes { get; internal set; }
            public HFTagTypes HFTagTypes { get; internal set; }
        }


        /// <summary>
        ///     This function returns the transponder types, which are actually supported by the individual TWN4 separated
        ///     by frequency (LF and HF). Also the P-option is taken into account. This means, if the specific TWN4
        ///     has no option P, the appropriate transponders are not returned as supported type of transponder.
        /// </summary>
        /// <returns>Tag types.</returns>
        public async Task<GetSupportedTagTypesResult> GetSupportedTagTypesAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_RF, 4 });
            var lf = parser.ParseUInt32();
            var hf = parser.ParseUInt32();

            return new GetSupportedTagTypesResult() { LFTagTypes = (LFTagTypes)lf, HFTagTypes = (HFTagTypes)hf };
        }

        public class GetSupportedTagTypesResult
        {
            public LFTagTypes LFTagTypes { get; internal set; }
            public HFTagTypes HFTagTypes { get; internal set; }
        }

        #endregion

        #region API_MIFARECLASSIC / Mifare Classic Functions

        public static readonly byte API_MIFARECLASSIC = 11;

        public static readonly byte MIFARE_CLASSIC_LOGIN = 0;
        public static readonly byte MIFARE_CLASSIC_READBLOCK = 1;
        public static readonly byte MIFARE_CLASSIC_WRITEBLOCK = 2;

        /// <summary>
        /// Login to a Mifare Classic single Sector.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="keyType"></param>
        /// <param name="sectorNumber"></param>
        /// <returns></returns>
        /// <exception cref="ReaderException"></exception>
        public async Task MifareClassic_LoginAsync(string key, byte keyType, byte sectorNumber)
        {
            List<byte> bytes = new List<byte> { API_MIFARECLASSIC, MIFARE_CLASSIC_LOGIN };
            bytes.AddRange(ByteArrayConverter.GetBytesFrom(key));
            bytes.Add(keyType);
            bytes.Add(sectorNumber);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Read Data from Classic Chip
        /// </summary>
        /// <param name="blockNumber">DataBlock Number</param>
        /// <returns>DATA</returns>
        /// <exception cref="ReaderException"></exception>
        public async Task<byte[]> MifareClassic_ReadBlockAsync(byte blockNumber)
        {
            List<byte> bytes = new List<byte> { API_MIFARECLASSIC, MIFARE_CLASSIC_READBLOCK };
            bytes.Add(blockNumber);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (success)
            {
                return parser.ParseFixByteArray(16);
            }
            else
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Write Data to Classic Chip
        /// </summary>
        /// <param name="data">16 Bytes of Data to Write</param>
        /// <param name="blockNumber">DataBlock Number</param>
        /// <returns></returns>
        /// <exception cref="ReaderException"></exception>
        public async Task MifareClassic_WriteBlockAsync(byte[] data, byte blockNumber)
        {
            List<byte> bytes = new List<byte> { API_MIFARECLASSIC, MIFARE_CLASSIC_WRITEBLOCK, blockNumber };
            bytes.AddRange(data);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        // TODO: SYSFUNC(API_MIFARECLASSIC, 3, bool MifareClassic_ReadValueBlock(int Block, int* Value))
        // TODO: SYSFUNC(API_MIFARECLASSIC, 4, bool MifareClassic_WriteValueBlock(int Block, int Value))
        // TODO: SYSFUNC(API_MIFARECLASSIC, 5, bool MifareClassic_IncrementValueBlock(int Block, int Value))
        // TODO: SYSFUNC(API_MIFARECLASSIC, 6, bool MifareClassic_DecrementValueBlock(int Block, int Value))
        // TODO: SYSFUNC(API_MIFARECLASSIC, 7, bool MifareClassic_CopyValueBlock(int SourceBlock, int DestBlock))


        #endregion

        #region API_MIFAREULTRALIGHT / Mifare Ultralight Functions

        public static readonly byte API_MIFAREULTRALIGHT = 12;

        /// <summary>
        ///     Though the page size of this transponder family is 4 bytes, the transponder always returns 16 bytes of data.
        ///     This is achieved by reading four consecutive data pages, e.g. if page 4 is to be read, the transponder also
        ///     returns the content of page 5, 6 and 7. The transponder incorporates an integrated roll-back mechanism
        ///     if reading is done beyond the last physical available page address.E.g., in case of reading page 14 of
        ///     MIFARE Ultralight this would result in reading page 14, 15, 0, 1.
        /// </summary>
        /// <param name="page">Specify the address of the page to be read. The valid range of this parameter
        ///     is between 0 and 15 (Ultralight) or 0 and 43 (Ultralight C).</param>
        /// <returns></returns>
        public async Task<byte[]> MifareUltralight_ReadPageAsync(byte page)
        {
            var parser = await CallFunctionAsync(new byte[] { API_MIFAREULTRALIGHT, 0, page });
            var success = parser.ParseBool();
            if (success)
            {
                var result = parser.ParseFixByteArray(16);
                return result;
            }
            return null;
        }

        // TODO: SYSFUNC(API_MIFAREULTRALIGHT, 1, bool MifareUltralight_WritePage(int Page, const byte* Data))
        // TODO: SYSFUNC(API_MIFAREULTRALIGHT, 2, bool MifareUltralightC_Authenticate(const byte* Key))
        // TODO: SYSFUNC(API_MIFAREULTRALIGHT, 3, bool MifareUltralightC_SAMAuthenticate(int KeyNo, int KeyVersion, const byte* DIVInput, int DIVByteCnt))
        // TODO: SYSFUNC(API_MIFAREULTRALIGHT, 4, bool MifareUltralightC_WriteKeyFromSAM(int KeyNo, int KeyVersion, const byte* DIVInput, int DIVByteCnt))

        /// <summary>
        /// The Fast Read function reads a number of pages beginning at a starting page from the transponder.
        /// </summary>
        /// <param name="startPage">Specify the address of the starting page.</param>
        /// <param name="numberOfPages">Specify the number of pages to be read.</param>
        /// <returns></returns>
        public async Task<byte[]> MifareUltralightEV1_FastReadAsync(byte startPage, byte numberOfPages)
        {
            var parser = await CallFunctionAsync(new byte[] { API_MIFAREULTRALIGHT, 5, startPage, numberOfPages });
            var success = parser.ParseBool();
            if (success)
            {
                var result = parser.ParseVarByteArray();
                return result;
            }
            return null;
        }

        // TODO: SYSFUNC(API_MIFAREULTRALIGHT, 6, bool MifareUltralightEV1_IncCounter(int CounterAddr, int IncrValue))
        // TODO: SYSFUNC(API_MIFAREULTRALIGHT, 7, bool MifareUltralightEV1_ReadCounter(int CounterAddr, int* CounterValue))
        // TODO: SYSFUNC(API_MIFAREULTRALIGHT, 8, bool MifareUltralightEV1_ReadSig(byte* ECCSig))
        // TODO: SYSFUNC(API_MIFAREULTRALIGHT, 9, bool MifareUltralightEV1_GetVersion(byte* Version))
        // TODO: SYSFUNC(API_MIFAREULTRALIGHT, 10, bool MifareUltralightEV1_PwdAuth(const byte* Password, const byte* PwdAck))
        // TODO: SYSFUNC(API_MIFAREULTRALIGHT, 11, bool MifareUltralightEV1_CheckTearingEvent(int CounterAddr, byte* ValidFlag))

        #endregion

        #region API_MIFAREDESFIRE / Mifare Desfire Functions

        public static readonly byte API_MIFAREDESFIRE = 15;

        private static readonly byte CRYPTO_ENV = 0;
        private static readonly byte DESFIRE_KEYLENGTH = 0x10;
        private static readonly byte DESFIRE_MAX_FILEIDS = 0xFF;

        private static readonly byte MIFARE_DESFIRE_GETAPPIDS = 0;
        private static readonly byte MIFARE_DESFIRE_CREATEAPP = 1;
        private static readonly byte MIFARE_DESFIRE_DELETEAPP = 2;
        private static readonly byte MIFARE_DESFIRE_SELECTAPP = 3;
        private static readonly byte MIFARE_DESFIRE_AUTH = 4;
        private static readonly byte MIFARE_DESFIRE_GETKEYSETTINGS = 5;
        private static readonly byte MIFARE_DESFIRE_GETFILEIDS = 6;
        private static readonly byte MIFARE_DESFIRE_GETFILESETTINGS = 7;
        private static readonly byte MIFARE_DESFIRE_READDATA = 8;
        private static readonly byte MIFARE_DESFIRE_WRITEDATA = 9;
        private static readonly byte MIFARE_DESFIRE_GETFREEMEMORY = 14;
        private static readonly byte MIFARE_DESFIRE_FORMATTAG = 15;
        private static readonly byte MIFARE_DESFIRE_CREATE_STDDATAFILE = 16;
        private static readonly byte MIFARE_DESFIRE_GETVERSION = 18;
        private static readonly byte MIFARE_DESFIRE_DELETEFILE = 19;
        private static readonly byte MIFARE_DESFIRE_CHANGEKEYSETTINGS = 24;
        private static readonly byte MIFARE_DESFIRE_CHANGEKEY = 25;

        /// <summary>
        /// Retrieve the Available Application IDs after selecing PICC (App 0), Authentication is needed - depending on the security config
        /// </summary>
        /// <returns>a uint32[] of the available appids with 4bytes each, null if no apps are available or on error</returns>
        public async Task<UInt32[]> MifareDesfire_GetAppIDsAsync()
        {
            return await MifareDesfire_GetAppIDsAsync(28);
        }

        /// <summary>
        /// Retrieve the Available Application IDs after selecing PICC (App 0), Authentication is needed - depending on the security config
        /// </summary>
        /// <param name="maxAppIDCnt"></param>
        /// <returns>a uint32[] of the available appids with 4bytes each, null if no apps are available or on error</returns>
        /// <exception cref="ReaderException"></exception>
        public async Task<UInt32[]> MifareDesfire_GetAppIDsAsync(byte maxAppIDCnt)
        {
            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_GETAPPIDS , CRYPTO_ENV , maxAppIDCnt};

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (success)
            {
                var appIDCnt = parser.ParseByte();

                var appids = new UInt32[appIDCnt];

                for (var i = 0; i < appIDCnt; i++)
                {
                    appids[i] = parser.ParseUInt32();
                }

                return appids;
            }
            else
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Creates a new Application
        /// </summary>
        /// <param name="_keySettingsTarget">byte: KS_CHANGE_KEY_WITH_MK = 0, KS_ALLOW_CHANGE_MK = 1, KS_FREE_LISTING_WITHOUT_MK = 2, KS_FREE_CREATE_DELETE_WITHOUT_MK = 4, KS_CONFIGURATION_CHANGEABLE = 8, KS_DEFAULT = 11, KS_CHANGE_KEY_WITH_TARGETED_KEYNO = 224, KS_CHANGE_KEY_FROZEN = 240</param>
        /// <param name="_keyTypeTargetApplication">byte: 0 = 3DES, 1 = 3K3DES, 2 = AES</param>
        /// <param name="_maxNbKeys">int max. number of keys</param>
        /// <param name="_appID">int application id</param>
        /// <returns></returns>
        /// <exception cref="ReaderException"></exception>
        public async Task MifareDesfire_CreateApplicationAsync(DESFireAppAccessRights keySettingsTarget, DESFireKeyType keyTypeTargetApplication, int maxNbKeys, int appID)
        {
            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_CREATEAPP, CRYPTO_ENV };
            bytes.AddUInt32((UInt32)appID);
            bytes.Add((byte)keySettingsTarget);
            bytes.AddUInt32((UInt32)maxNbKeys);
            bytes.AddUInt32((UInt32)keyTypeTargetApplication);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Select a Desfire Application
        /// </summary>
        /// <param name="appID"></param>
        /// <returns></returns>
        /// <exception cref="ReaderException"></exception>
        public async Task MifareDesfire_DeleteApplicationAsync(uint appID)
        {
            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_DELETEAPP, CRYPTO_ENV };
            bytes.AddUInt32((UInt32)appID);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Select a Mifare Desfire Application
        /// </summary>
        /// <param name="appID">The Application ID to select</param>
        /// <returns>true if Application could be selected, false otherwise</returns>
        /// <exception cref="ReaderException"></exception>
        public async Task MifareDesfire_SelectApplicationAsync(uint appID)
        {          
            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_SELECTAPP, CRYPTO_ENV };
            bytes.AddUInt32((UInt32)appID);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Authenticate to a previously selected desfire application
        /// </summary>
        /// <param name="key">string: a 16 bytes key e.g. 00000000000000000000000000000000</param>
        /// <param name="keyNo">byte: the keyNo to use</param>
        /// <param name="keyType">byte: 0 = 3DES, 1 = 3K3DES, 2 = AES</param>
        /// <param name="authMode">byte: 1 = EV1 Mode, 0 = EV0 Mode</param>
        /// <returns></returns>
        /// <exception cref="ReaderException"></exception>
        public async Task MifareDesfire_AuthenticateAsync(string key, byte keyNo, byte keyType, byte authMode)
        {
            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_AUTH, CRYPTO_ENV , keyNo, DESFIRE_KEYLENGTH};
            bytes.AddRange(ByteArrayConverter.GetBytesFrom(key));
            bytes.Add(keyType);
            bytes.Add(authMode);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Get the KeySettings (Properties: KeySettings, NumberOfKeys, KeyType) of the selected Application. Authentication is needed - depending on the security config
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ReaderException"></exception>
        public async Task<DESFireKeySettings> MifareDesfire_GetKeySettingsAsync()
        {
            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_GETKEYSETTINGS, CRYPTO_ENV};

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (success)
            {
                var keySettings = new DESFireKeySettings();

                keySettings.AccessRights = (DESFireAppAccessRights)parser.ParseByte();
                keySettings.NumberOfKeys = parser.ParseUInt32();
                keySettings.KeyType = (DESFireKeyType)parser.ParseUInt32();

                return keySettings;
            }
            else
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Retrieve the available file IDs after selecing app and authenticating to app
        /// </summary>
        /// <returns>byte[] array of available file ids</returns>
        /// <exception cref="ReaderException"></exception>
        public async Task<byte[]> MifareDesfire_GetFileIDsAsync()
        {
            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_GETFILEIDS, CRYPTO_ENV , DESFIRE_MAX_FILEIDS};

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (success)
            {
                var filesCount = parser.ParseByte();
                var fids = new byte[filesCount];

                for (var i = 0; i < filesCount; i++)
                {
                    fids[i] = parser.ParseByte();
                }

                return fids;
            }
            else
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Get the filesettings of a fileid
        /// </summary>
        /// <param name="fileNo">ID of the desired file</param>
        /// <returns><see cref="DESFireFileSettings"/></returns>
        /// <exception cref="ReaderException"></exception>
        public async Task<DESFireFileSettings> MifareDesfire_GetFileSettingsAsync(byte fileNo)
        {
            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_GETFILESETTINGS, CRYPTO_ENV, fileNo };

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (success)
            {
                var fileSettings = new DESFireFileSettings();

                fileSettings.FileType = (DESFireFileType)parser.ParseByte();
                fileSettings.ComSett = parser.ParseByte();

                var ar = parser.ParseUInt16();

                fileSettings.accessRights.ReadKeyNo = (byte)(ar & 0x000F);
                fileSettings.accessRights.WriteKeyNo = (byte)((ar & 0x00F0) >> 4);
                fileSettings.accessRights.ReadWriteKeyNo = (byte)((ar & 0x0F00) >> 8);
                fileSettings.accessRights.ChangeKeyNo = (byte)((ar & 0xF000) >> 12);

                switch (fileSettings.FileType)
                {
                    case DESFireFileType.DF_FT_STDDATAFILE:
                    case DESFireFileType.DF_FT_BACKUPDATAFILE:
                        fileSettings.DataFileSetting = fileSettings.DataFileSetting ?? new DataFileSetting();
                        fileSettings.DataFileSetting.FileSize = parser.ParseUInt32();
                        break;

                    case DESFireFileType.DF_FT_VALUEFILE:
                        fileSettings.ValueFileSetting.LowerLimit = parser.ParseUInt32();
                        fileSettings.ValueFileSetting.UpperLimit = parser.ParseUInt32();
                        fileSettings.ValueFileSetting.LimitedCreditValue = parser.ParseUInt32();
                        fileSettings.ValueFileSetting.LimitedCreditEnabled = parser.ParseByte();
                        fileSettings.ValueFileSetting.FreeGetValue = parser.ParseByte();
                        fileSettings.ValueFileSetting.RFU = parser.ParseByte();
                        break;

                    case DESFireFileType.DF_FT_CYCLICRECORDFILE:
                    case DESFireFileType.DF_FT_LINEARRECORDFILE:
                        fileSettings.RecordFileSetting = fileSettings.RecordFileSetting ?? new RecordFileSetting();
                        fileSettings.RecordFileSetting.RecordSize = parser.ParseUInt32();
                        fileSettings.RecordFileSetting.MaxNumberOfRecords = parser.ParseUInt32();
                        fileSettings.RecordFileSetting.CurrentNumberOfRecords = parser.ParseUInt32();
                        break;

                    default:

                        break;
                }

                return fileSettings;
            }
            else
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Read out Data on a Desfire
        /// </summary>
        /// <param name="fileNo">byte: filenumber: 0x00 - 0x14</param>
        /// <param name="length">int: filesize to read</param>
        /// <param name="comSet">byte: 0 = Plain, 1 = CMAC, 2 = Encrypted</param>
        /// <returns>byte[] of data</returns>
        /// <exception cref="ReaderException"></exception>
        public async Task<byte[]> MifareDesfire_ReadDataAsync(byte fileNo, int length, EncryptionMode mode)
        {
            var data = new byte[length];
            var iterations = (length / 0xFF) == 0 ? 1 : (length / 0xFF); // more than one byte?
            var dataLengthToRead = length;

            for (var i = 0; i < iterations; i++)
            {
                List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_READDATA, CRYPTO_ENV, fileNo };

                bytes.AddUInt16((UInt16)(i * 0xFF));
                bytes.Add((byte)(dataLengthToRead >= 0xFF ? 0xFF : length));
                bytes.Add((byte)mode);

                var parser = await CallFunctionAsync(bytes.ToArray());
                var success = parser.ParseBool();

                if (success)
                {
                    Array.Copy(parser.ParseVarByteArray(), 0, data, (i * 0xFF), (dataLengthToRead >= 0xFF ? 0xFF : length));
                }
                else
                {
                    throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
                }
            }

            return data;
        }

        /// <summary>
        /// Write Data to a Desfire File
        /// </summary>
        /// <param name="fileNo">The file number to read</param>
        /// <param name="data"></param>
        /// <param name="mode"><see cref="EncryptionMode"/></param>
        /// <returns></returns>
        /// <exception cref="ReaderException"></exception>
        public async Task MifareDesfire_WriteDataAsync(byte fileNo, byte[] data, EncryptionMode mode)
        {
            var iterations = (data.Length / 0xFF) == 0 ? 1 : (data.Length / 0xFF); // more than one byte?
            var lengthToWrite = data.Length;
            var success = false;

            for (var i = 0; i < iterations; i++)
            {
                List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_WRITEDATA, CRYPTO_ENV, fileNo };

                lengthToWrite = lengthToWrite >= 0xFF ? 0xFF : lengthToWrite; // more data?

                var dataToWrite = new byte[(lengthToWrite >= 0xFF ? 0xFF : lengthToWrite)];
                Array.Copy(data, (i * 0xFF), dataToWrite, 0, (lengthToWrite >= 0xFF ? 0xFF : lengthToWrite));
                
                bytes.AddUInt16((UInt16)(i * 0xFF));
                bytes.Add((byte)lengthToWrite);
                bytes.AddRange(data);
                bytes.Add((byte)mode);

                var parser = await CallFunctionAsync(bytes.ToArray());
                success = parser.ParseBool();

                if (!success)
                {
                    throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
                }
            }

            if (!success)
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        //TODO: SYSFUNC(API_DESFIRE,10, bool DESFire_GetValue(int CryptoEnv, int FileNo, int* Value, int CommSet))
        //TODO: SYSFUNC(API_DESFIRE,11, bool DESFire_Credit(int CryptoEnv, int FileNo, const int Value, int CommSet))
        //TODO: SYSFUNC(API_DESFIRE,12, bool DESFire_Debit(int CryptoEnv, int FileNo, const int Value, int CommSet))
        //TODO: SYSFUNC(API_DESFIRE,13, bool DESFire_LimitedCredit(int CryptoEnv, int FileNo, const int Value, int CommSet))

        /// <summary>
        /// Get the free Memory of a desfire. 
        /// </summary>
        /// <returns>a uint32 of the available memory if supported</returns>
        /// <exception cref="ReaderException"></exception>
        public async Task<UInt16> MifareDesfire_GetFreeMemoryAsync()
        {
            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_GETFREEMEMORY, CRYPTO_ENV };

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (success)
            {
                return parser.ParseUInt16();
            }
            else
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Format a Chip
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ReaderException"></exception>
        public async Task MifareDesfire_FormatTagAsync()
        {
            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_FORMATTAG, CRYPTO_ENV };

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Create a std data file on a desfire
        /// </summary>
        /// <param name="fileNo"></param>
        /// <param name="fileType"><see cref="DESFireFileType"/></param>
        /// <param name="mode"><see cref="EncryptionMode"/></param>
        /// <param name="accessRights"><see cref="DESFireFileAccessRights"/></param>
        /// <param name="fileSize"></param>
        /// <returns></returns>
        /// <exception cref="ReaderException"></exception>
        public async Task MifareDesfire_CreateStdDataFileAsync(byte fileNo, DESFireFileType fileType, EncryptionMode mode, DESFireFileAccessRights accessRights, UInt32 fileSize)
        {
            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_CREATE_STDDATAFILE, CRYPTO_ENV, fileNo, (byte)fileType, (byte)mode };

            UInt16 fileAccessRights = 0;

            fileAccessRights |= accessRights.ReadKeyNo;
            fileAccessRights |= (byte)(accessRights.WriteKeyNo << 4);
            fileAccessRights |= (byte)(accessRights.ReadWriteKeyNo << 8);
            fileAccessRights |= (byte)(accessRights.ChangeKeyNo << 12);

            bytes.AddUInt16(fileAccessRights);
            bytes.AddUInt32(fileSize);
            bytes.AddRange(new byte[] { 0,0,0,0, 0,0,0,0, 0,0,0,0 });

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        //TODO: SYSFUNC(API_DESFIRE,17, bool DESFire_CreateValueFile(int CryptoEnv, int FileNo, const TDESFireFileSettings* FileSettings))

        /// <summary>
        /// Get version of a desfire.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ReaderException"></exception>
        public async Task<byte[]> MifareDesfire_GetVersionAsync()
        {
            {
                List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_GETVERSION, CRYPTO_ENV };

                var parser = await CallFunctionAsync(bytes.ToArray());
                var success = parser.ParseBool();

                if (success)
                {
                    return parser.ParseVarByteArray();
                }
                else
                {
                    throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.NotSupported), null);
                }
            }
        }

        /// <summary>
        /// Delete a file in a desfire app
        /// </summary>
        /// <param name="fileNo">byte: Filenumber to delete</param>
        /// <returns>true if the Operation was successful, false otherwise</returns>
        /// <exception cref="ReaderException"></exception>
        public async Task MifareDesfire_DeleteFileAsync(byte fileNo)
        {
            {
                List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_DELETEFILE, CRYPTO_ENV, fileNo };

                var parser = await CallFunctionAsync(bytes.ToArray());
                var success = parser.ParseBool();

                if (!success)
                {
                    throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
                }
            }
        }

        //TODO: SYSFUNC(API_DESFIRE,20, bool DESFire_CommitTransaction(int CryptoEnv))
        //TODO: SYSFUNC(API_DESFIRE,21, bool DESFire_AbortTransaction(int CryptoEnv))
        //TODO: SYSFUNC(API_DESFIRE,22, bool DESFire_GetUID(int CryptoEnv, byte* UID, int* Length, int BufferSize))
        //TODO: SYSFUNC(API_DESFIRE,23, bool DESFire_GetKeyVersion(int CryptoEnv, int KeyNo, byte* KeyVer))

        /// <summary>
        /// 
        /// </summary>
        /// <param name="keySettings"></param>
        /// <param name="numberOfKeys"></param>
        /// <param name="keyType"></param>
        /// <returns></returns>
        public async Task MifareDesfire_ChangeKeySettingsAsync(DESFireAppAccessRights keySettings, UInt32 numberOfKeys, DESFireKeyType keyType)
        {
            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_CHANGEKEYSETTINGS, CRYPTO_ENV};

            bytes.Add((byte)keySettings);
            bytes.AddUInt32(numberOfKeys);
            bytes.AddUInt32((byte)keyType);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Changes a Key
        /// </summary>
        /// <param name="oldKey"></param>
        /// <param name="newKey"></param>
        /// <param name="keyVersion"></param>
        /// <param name="accessRights"></param>
        /// <param name="keyNo"></param>
        /// <param name="numberOfKeys"></param>
        /// <param name="keyType">The Type of the new Key</param>
        /// <returns></returns>
        public async Task MifareDesfire_ChangeKeyAsync(string oldKey, string newKey, byte keyVersion, byte accessRights, byte keyNo, UInt32 numberOfKeys, DESFireKeyType keyType)
        {
            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_CHANGEKEY, CRYPTO_ENV, keyNo };
            bytes.Add(DESFIRE_KEYLENGTH);
            bytes.AddRange(ByteArrayConverter.GetBytesFrom(oldKey));
            bytes.Add(DESFIRE_KEYLENGTH);
            bytes.AddRange(ByteArrayConverter.GetBytesFrom(newKey));
            bytes.Add(keyVersion);
            bytes.Add(accessRights);
            bytes.AddUInt32(numberOfKeys);
            bytes.AddUInt32((byte)keyType);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        //TODO: SYSFUNC(API_DESFIRE,26, bool DESFire_ChangeFileSettings(int CryptoEnv, int FileNo, int NewCommSet, int OldAccessRights, int NewAccessRights))
        //TODO: SYSFUNC(API_DESFIRE,27, bool DESFire_DisableFormatTag(int CryptoEnv))
        //TODO: SYSFUNC(API_DESFIRE,28, bool DESFire_EnableRandomID(int CryptoEnv))
        //TODO: SYSFUNC(API_DESFIRE,29, bool DESFire_SetDefaultKey(int CryptoEnv, const byte* Key, int KeyByteCount, byte KeyVersion))
        //TODO: SYSFUNC(API_DESFIRE,30, bool DESFire_SetATS(int CryptoEnv, const byte* ATS, int Length))
        //TODO: SYSFUNC(API_DESFIRE,31, bool DESFire_CreateRecordFile(int CryptoEnv, int FileNo, const TDESFireFileSettings* FileSettings))
        //TODO: SYSFUNC(API_DESFIRE,32, bool DESFire_ReadRecords(int CryptoEnv, int FileNo, byte* RecordData, int* RecDataByteCnt, int Offset, int NumberOfRecords, int RecordSize, int CommSet))
        //TODO: SYSFUNC(API_DESFIRE,33, bool DESFire_WriteRecord(int CryptoEnv, int FileNo, const byte* Data, int Offset, int Length, int CommSet))
        //TODO: SYSFUNC(API_DESFIRE,34, bool DESFire_ClearRecordFile(int CryptoEnv, int FileNo))

        #endregion

        #region API_ISO14443 / ISO14443 Transparent Transponder Access Functions

        public static readonly byte API_ISO14443 = 18;

        /// <summary>
        /// This function delivers the ATS (Answer To Select) of a ISO14443A layer 4 transponder.
        /// </summary>
        /// <returns>The ATS if one is found, otherwise null.</returns>
        public async Task<byte[]> ISO14443A_GetAtsAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_ISO14443, 0, /* maxBytes */ byte.MaxValue });
            var success = parser.ParseBool();
            if (success)
            {
                var result = parser.ParseVarByteArray();
                return result;
            }
            return null;
        }

        /// <summary>
        ///     This function delivers the ATQB (Answer To Request TypeB) of the last detected ISO14443B compliant transponder.<br/>
        ///     Note: This function cannot be called on TWN4 MultiTech Legic.
        /// </summary>
        /// <returns>The ATQB if one is found, otherwise null.</returns>
        public async Task<byte[]> ISO14443B_GetAtqbAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_ISO14443, 1, /* maxBytes */ byte.MaxValue });
            var success = parser.ParseBool();
            if (success)
            {
                var result = parser.ParseVarByteArray();
                return result;
            }
            return null;
        }

        /// <summary>
        ///     This function can be used to probe if a ISO14443-4 transponder is still in reading range. The internal state
        ///     of the transponder remains unchanged. <br/>
        ///     Note: This function cannot be called on TWN4 MultiTech Legic.
        /// </summary>
        /// <returns></returns>
        public async Task<bool> ISO14443_4_CheckPresenceAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_ISO14443, 2 });
            var result = parser.ParseBool();
            return result;
        }

        /// <summary>
        ///     This function can be used for transparent exchange of data between reader and ISO14443-4 transponders.
        ///     All framing of layer 4 subset is already done by the reader, so only the payload needs to be passed
        ///     to the function.
        /// </summary>
        /// <param name="TX">Data that shall be transmitted to the transponder.</param>
        /// <returns>The response of the transponder.</returns>
        public async Task<byte[]> ISO14443_4_TdxAsync(byte[] TX)
        {
            List<byte> bytes = new List<byte> { API_ISO14443, 3 };
            bytes.Add((byte)TX.Length);
            bytes.AddRange(TX);
            bytes.Add(byte.MaxValue); // MaxRXByteCnt
            
            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();
            if (success)
            {
                var result = parser.ParseVarByteArray();
                return result;
            }
            return null;
        }

        /// <summary>
        ///     This function delivers the ATQA (Answer To Request TypeA) of the last detected ISO14443A compliant transponder.
        ///     The ATQA consists of two bytes, parsed in LSB-first order.
        /// </summary>
        /// <returns>The ATQA if one is found, otherwise null.</returns>
        public async Task<ushort?> ISO14443A_GetAtqaAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_ISO14443, 4 });
            var success = parser.ParseBool();
            if (success)
            {
                var result = parser.ParseUInt16();
                return result;
            }
            return null;
        }

        /// <summary>
        /// This function delivers the SAK (Select Acknowledge) of the last detected ISO14443A compliant transponder.
        /// </summary>
        /// <returns>The SAK if one is found, otherwise null.</returns>
        public async Task<byte?> ISO14443A_GetSakAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_ISO14443, 5 });
            var success = parser.ParseBool();
            if (success)
            {
                var result = parser.ParseByte();
                return result;
            }
            return null;
        }

        /// <summary>
        ///     This function delivers the transponder’s answer to the ATTRIB command, which is sent automatically
        ///     during selection process by the reader. <br/>
        ///     Note: This function cannot be called on TWN4 MultiTech Legic.
        /// </summary>
        /// <returns>The response of the transponder.</returns>
        public async Task<byte[]> ISO14443B_GetAnswerToAttribAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_ISO14443, 6, /* maxBytes */ byte.MaxValue });
            var success = parser.ParseBool();
            if (success)
            {
                var result = parser.ParseVarByteArray();
                return result;
            }
            return null;
        }

        /// <summary>
        ///     This function can be used for transparent exchange of data between reader and ISO14443-3 transponders.
        ///     The function does not calculate any CRC or other overhead by itself, so if necessary this has to be
        ///     conducted on host side.
        /// </summary>
        /// <param name="TX">Data that shall be transmitted to the transponder.</param>
        /// <param name="timeout">Response timeout in milliseconds.</param>
        /// <returns>The response of the transponder.</returns>
        public async Task<byte[]> ISO14443_3_TdxAsync(byte[] TX, ushort timeout)
        {
            List<byte> bytes = new List<byte> { API_ISO14443, 7 };
            bytes.Add((byte)TX.Length);
            bytes.AddRange(TX);
            bytes.Add(byte.MaxValue); // MaxRXByteCnt
            bytes.AddUInt16(timeout);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();
            if (success)
            {
                var result = parser.ParseVarByteArray();
                return result;
            }
            return null;
        }

        /// <summary>
        /// Use this function to search the RF field for ISO14443A transponders. The result is a list of the UID of the respective transponders.
        /// </summary>
        /// <returns>A list containing the UIDs of all transponders.</returns>
        public async Task<List<byte[]>> ISO14443A_SearchMultiTagAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_ISO14443, 8, /* maxIDBytes */ byte.MaxValue });
            var tagList = new List<byte[]>();

            var found = parser.ParseBool();
            if (found)
            {
                var count = parser.ParseByte();
                parser.ParseByte(); // Total number of bytes. We don't need this.
                for (int i = 0; i < count; i++)
                {
                    var tag = parser.ParseVarByteArray();
                    tagList.Add(tag);
                }
            }

            return tagList;
        }


        /// <summary>
        /// Use this function to select one of the discovered transponders for further operations.
        /// IMPORTANT: This does not work on Legic capable TWN4 Mutitec Readers. Use SearchTag instead. 
        /// </summary>
        /// <param name="uid">Specify the UID of the transponder to be selected.</param>
        /// <returns>If the operation was successful, the return value is true, otherwise it is false.</returns>
        public async Task<bool> ISO14443A_SelectTagAsync(byte[] uid)
        {
            List<byte> bytes = new List<byte> { API_ISO14443, 9 };
            bytes.Add((byte)uid.Length);
            bytes.AddRange(uid);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();
            return success;
        }

        // TODO: SYSFUNC(API_ISO14443, 10, bool preISO14443B_GetATR(byte* ATR, int* ATRByteCnt, int MaxATRByteCnt))

        /// <summary>
        /// Reselect a transponder.
        /// </summary>
        /// <returns></returns>
        /// <remarks>SYSFUNC(API_ISO14443, 11, bool ISO14443A_Reselect(void))<br/>
        ///     This internal method is not documented in TWN4 API reference.</remarks>
        public async Task<bool> ISO14443A_Reselect()
        {
            var parser = await CallFunctionAsync(new byte[] { API_ISO14443, 11 });
            var result = parser.ParseBool();
            return result;
        }

        #endregion

        #endregion

        #region High Level APIs

        /// <summary>
        /// Play a melody on the device. 
        /// </summary>
        /// <param name="tempo">Specify the BPM.</param>
        /// <param name="song">Specify the song as List of Tones <see cref="Tone"/>.<</param>
        /// <returns></returns>
        public async Task PlayMelody(int tempo, List<Tone> song)
        {
            foreach (Tone tone in song)
            {
                ushort onTime, offTime;
                ushort duration = (ushort)(1000 * 1 / tempo * 60 / (16 / tone.Value));

                if (tone.Pitch > 0)
                {
                    onTime = duration;
                    offTime = 0;
                }
                else // Play a PAUSE
                {
                    onTime = 0;
                    offTime = duration;
                }

                if (tone.IsStaccato)    
                {
                    onTime = (ushort)(duration / 2);
                    offTime = (ushort)(duration / 2);
                }

                await BeepAsync(tone.Volume, (ushort)tone.Pitch, onTime, offTime);
            }
        }

        /// <summary>
        /// Each tone contains a volume, a Pitch <see cref="NotePitch"/> and a velue. Contains the optional IsStaccato Property which Produces a Tone with a Pulsewidth of 50%.  
        /// Value is calculated as 1 / tempo * 60 (length of a cadence) * 1 / (ToneValue / 16). Defaults to Volume = 60, and Value = 16
        /// Where Value is the Tone Length with a Base 16. So 16 is a whole note, 8 is a half note, 4 is a quarter note, 2 a quaver and 1 a semiquaver.
        /// The dotted Notes are then 12 for dotted half, 6 for dotted quarter and 3 for a dotted quaver
        /// </summary>
        public class Tone
        {
            public Tone()
            {
                Volume = 60;
                Value = 16;
            }
            public int Value    {       get; set;   }
            public byte Volume  {       get; set;   }
            public NotePitch Pitch  {   get; set;   }
            public bool IsStaccato  {   get; set;   }
        }

        #endregion

        #region Common

        /// <summary>
        /// Get a single chip which is currently in the reading range of the device.
        /// If multiple chips are present, only the first one is returned.
        /// Use <see cref="ISO14443A_SearchMultiTagAsync"/> if you need to work with multiple chips.
        /// </summary>
        /// <returns>Depending on the ChipType a BaseChip or specialized class is returned, e.g. MifareChip.</returns>
        public async Task<BaseChip> GetSingleChipAsync()
        {
            var tag = await SearchTagAsync();
            if (tag != null)
            {
                var chip = await GetTypedChipInstance(tag.ChipType, tag.IDBytes);
                return chip;
            }

            return null;
        }

        private async Task<BaseChip> GetTypedChipInstance(ChipType chipType, byte[] uid)
        {
            BaseChip chip;
            switch (chipType)
            {
                case ChipType.MIFARE:
                    var mifareChip = new MifareChip(chipType, uid);
                    await DetectMifareSubType(mifareChip);
                    chip = mifareChip;
                    break;
                case ChipType.LEGIC:
                    chip = new BaseChip(chipType, uid);
                    break;
                default:
                    chip = new BaseChip(chipType, uid);
                    break;
            }
            
            return chip;
        }

        private async Task DetectMifareSubType(MifareChip currentChip)
        {
            //Start Mifare Identification Process as of NXP AN10833

            /*
             * IMPORTANT: Selecting a Tag is unsupported when using a TWN4 Multitec HF LF 2 Legic capable Reader.
             * The elatec support said, this is not documented. Use SearchTag instead.
             * 
             * Original Version:
            // If multiple tags were detected, select one.
            bool success = await ISO14443A_SelectTagAsync(currentChip.UID);
            if (!success)
            {
                var errorNumber = await GetLastErrorAsync();
                // returned error numbers are not properly documented, like 0x10000001. But the code execution usually continues successfully with GetAtqa.
                return;
            }
            */

            var atqa = await ISO14443A_GetAtqaAsync();
            if (atqa.HasValue)
            {
                currentChip.ATQA = atqa.Value;
            }
            
            var sakResult = await ISO14443A_GetSakAsync();
            if (sakResult.HasValue)
            {
                currentChip.SAK = sakResult.Value;
            }

            var atsResult = await ISO14443A_GetAtsAsync();
            if (atsResult != null)
            {
                currentChip.ATS = atsResult;
            }

            // Start MIFARE identification
            if ((currentChip.SAK & 0x02) == 0x02)
            {
                currentChip.SubType = MifareChipSubType.Unspecified;
            } // RFU bit set (RFU = Reserved for Future Use)

            else
            {
                if ((currentChip.SAK & 0x08) == 0x08)
                {
                    if ((currentChip.SAK & 0x10) == 0x10)
                    {
                        if ((currentChip.SAK & 0x01) == 0x01)
                        {
                            currentChip.SubType = MifareChipSubType.Mifare2K;
                        } // // SAK b1 = 1 ? >> Mifare Classic 2K
                        else
                        {
                            if ((currentChip.SAK & 0x20) == 0x20)
                            {
                                currentChip.SubType = MifareChipSubType.SmartMX_Mifare_4K;
                            } // SAK b6 = 1 ?  >> SmartMX Classic 4K
                            else
                            {
                                //Get ATS - Switch to L4 ?

                                if (currentChip.ATS != null)
                                {
                                    if (ByteArrayConverter.SearchBytePattern(currentChip.ATS, new byte[] { 0xC1, 0x05, 0x2F, 0x2F, 0x00, 0x35, 0xC7 }) != 0) //MF PlusS 4K in SL1
                                    {
                                        currentChip.SubType = MifareChipSubType.MifarePlus_SL1_4K;
                                    }

                                    else if (ByteArrayConverter.SearchBytePattern(currentChip.ATS, new byte[] { 0xC1, 0x05, 0x2F, 0x2F, 0x01, 0xBC, 0xD6 }) != 0) //MF PlusX 4K in SL1
                                    {
                                        currentChip.SubType = MifareChipSubType.MifarePlus_SL1_4K;
                                    }

                                } // Mifare Plus S / Plus X 4K

                                else
                                {
                                    currentChip.SubType = MifareChipSubType.Mifare4K;
                                } //Error on ATS = Mifare Classic 4K
                            }
                        }
                    } // SAK b5 = 1 ?
                    else
                    {
                        if ((currentChip.SAK & 0x01) == 0x01)
                        {
                            currentChip.SubType = MifareChipSubType.MifareMini;
                        } // // SAK b1 = 1 ? >> Mifare Mini
                        else
                        {
                            if ((currentChip.SAK & 0x20) == 0x20)
                            {
                                currentChip.SubType = MifareChipSubType.SmartMX_Mifare_1K;
                            } // // SAK b6 = 1 ? >> SmartMX Classic 1K
                            else
                            {
                                if (currentChip.ATS != null)
                                {
                                    if (ByteArrayConverter.SearchBytePattern(currentChip.ATS, new byte[] { 0xC1, 0x05, 0x2F, 0x2F, 0x00, 0x35, 0xC7 }) != 0) //MF PlusS 4K in SL1
                                    {
                                        currentChip.SubType = MifareChipSubType.MifarePlus_SL1_2K;
                                    }

                                    else if (ByteArrayConverter.SearchBytePattern(currentChip.ATS, new byte[] { 0xC1, 0x05, 0x2F, 0x2F, 0x01, 0xBC, 0xD6 }) != 0) //MF PlusX 4K in SL1
                                    {
                                        currentChip.SubType = MifareChipSubType.MifarePlus_SL1_2K;
                                    }

                                    else if (ByteArrayConverter.SearchBytePattern(currentChip.ATS, new byte[] { 0xC1, 0x05, 0x21, 0x30, 0x00, 0xF6, 0xD1 }) != 0) //MF PlusSE 1K
                                    {
                                        currentChip.SubType = MifareChipSubType.MifarePlus_SL0_1K;
                                    }

                                    else
                                    {
                                        currentChip.SubType = MifareChipSubType.MifarePlus_SL1_1K;
                                    }
                                } // Mifare Plus S / Plus X 4K

                                else
                                {
                                    currentChip.SubType = MifareChipSubType.Mifare1K;
                                } //Error on ATS = Mifare Classic 1K
                            } // Mifare Plus; Historical Bytes ?
                        }
                    }
                } // SAK b4 = 1 ?
                else
                {
                    if ((currentChip.SAK & 0x10) == 0x10)
                    {
                        if ((currentChip.SAK & 0x01) == 0x01) // 
                        {
                            currentChip.SubType = MifareChipSubType.MifarePlus_SL2_4K;
                        } // Mifare Plus 4K in SL2
                        else
                        {
                            currentChip.SubType = MifareChipSubType.MifarePlus_SL2_2K;
                        } // Mifare Plus 2K in SL2
                    }
                    else
                    {
                        if ((currentChip.SAK & 0x01) == 0x01) // SAK b1 = 1 ?
                        {

                        } // Chip is "TagNPlay"
                        else
                        {
                            if ((currentChip.SAK & 0x20) == 0x20)
                            {
                                //Get Version in L4 ?
                                byte[] version = null;
                                try
                                {
                                    //for some reason MifareDesfire_GetVersionAsync does not work
                                    version = await ISO14443_4_TdxAsync(new byte[] { 0x60 });
                                    currentChip.VersionL4 = version;
                                }
                                //TODO: Check reader behaviour
                                catch { }

                                if (version != null && version?[0] == 0xAF)
                                {
                                    // Mifare Plus EV1/2 || DesFire || NTAG
                                    if (version?[2] == 0x01) // DESFIRE
                                    {
                                        switch (version?[4] & 0x0F) // Desfire(Sub)Type by lower Nibble of Major Version
                                        {
                                            case 0: //DESFIRE EV0
                                                switch (version?[6])
                                                {
                                                    case 0x10:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV0_256; // DESFIRE 256B
                                                        break;
                                                    case 0x16:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV0_1K; // DESFIRE 2K
                                                        break;
                                                    case 0x18:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV0_4K; // 4K
                                                        break;
                                                    default:
                                                        currentChip.SubType = MifareChipSubType.Unspecified;
                                                        break;
                                                } // Size ?
                                                break;

                                            case 1: // DESFIRE EV1
                                                switch (version?[6])
                                                {
                                                    case 0x10:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV1_256; //DESFIRE 256B
                                                        break;
                                                    case 0x16:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV1_2K; // DESFIRE 2K
                                                        break;
                                                    case 0x18:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV1_4K; // 4K
                                                        break;
                                                    case 0x1A:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV1_8K; // 8K
                                                        break;
                                                    default:
                                                        currentChip.SubType = MifareChipSubType.Unspecified;
                                                        break;
                                                } // Size ?
                                                break;

                                            case 2: // EV2
                                                switch (version?[6])
                                                {
                                                    case 0x16:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV2_2K; // DESFIRE 2K
                                                        break;
                                                    case 0x18:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV2_4K; // 4K
                                                        break;
                                                    case 0x1A:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV2_8K; // 8K
                                                        break;
                                                    case 0x1C:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV2_16K; // 16K
                                                        break;
                                                    case 0x1E:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV2_32K; // 32K
                                                        break;
                                                    default:
                                                        currentChip.SubType = MifareChipSubType.Unspecified;
                                                        break;
                                                } // SIZE ?
                                                break;

                                            case 3: // EV3
                                                switch (version?[6])
                                                {
                                                    case 0x16:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV3_2K; // DESFIRE 2K
                                                        break;
                                                    case 0x18:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV3_4K; // 4K
                                                        break;
                                                    case 0x1A:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV3_8K; // 8K
                                                        break;
                                                    case 0x1C:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV3_16K; // 16K
                                                        break;
                                                    case 0x1E:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV3_32K; // 32K
                                                        break;
                                                    default:
                                                        currentChip.SubType = MifareChipSubType.Unspecified;
                                                        break;
                                                } // SIZE ?
                                                break;

                                            default:
                                                currentChip.SubType = MifareChipSubType.Unspecified;

                                                break;
                                        }
                                    }
                                    if (version?[2] == 0x81) // Emulated e.g. SmartMX
                                    {
                                        switch (version?[4] & 0x0F) // Desfire(Sub)Type by lower Nibble of Major Version
                                        {
                                            case 0: //DESFIRE EV0
                                                switch (version?[6])
                                                {
                                                    case 0x10:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFireEV0_256; // DESFIRE 256B
                                                        break;
                                                    case 0x16:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFireEV0_2K; // DESFIRE 2K
                                                        break;
                                                    case 0x18:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFireEV0_4K; // 4K
                                                        break;
                                                    default:
                                                        currentChip.SubType = MifareChipSubType.Unspecified;
                                                        break;
                                                } // Size ?
                                                break;

                                            case 1: //DESFIRE EV1
                                                switch (version?[6])
                                                {
                                                    case 0x10:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFireEV1_256; //DESFIRE 256B
                                                        break;
                                                    case 0x16:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFireEV1_2K; // DESFIRE 2K
                                                        break;
                                                    case 0x18:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFireEV1_4K; // 4K
                                                        break;
                                                    case 0x1A:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFireEV1_8K; // 8K
                                                        break;
                                                    default:
                                                        break;
                                                } // Size ?
                                                break;

                                            case 2: // EV2
                                                switch (version?[6])
                                                {
                                                    case 0x16:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFireEV2_2K; // DESFIRE 2K
                                                        break;
                                                    case 0x18:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFireEV2_4K; // 4K
                                                        break;
                                                    case 0x1A:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFireEV2_8K; // 8K
                                                        break;
                                                    case 0x1C:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFireEV2_16K; // 16K
                                                        break;
                                                    case 0x1E:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFireEV2_32K; // 32K
                                                        break;
                                                    default:
                                                        break;
                                                } // SIZE ?
                                                break;

                                            case 3: // EV3
                                                switch (version?[6])
                                                {
                                                    case 0x16:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFireEV3_2K; // DESFIRE 2K
                                                        break;
                                                    case 0x18:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFireEV3_4K; // 4K
                                                        break;
                                                    case 0x1A:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFireEV3_8K; // 8K
                                                        break;
                                                    case 0x1C:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFireEV3_16K; // 16K
                                                        break;
                                                    case 0x1E:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFireEV3_32K; // 32K
                                                        break;
                                                    default:
                                                        break;
                                                } // SIZE ?
                                                break;

                                            default:
                                                currentChip.SubType = MifareChipSubType.Unspecified;

                                                break;
                                        }
                                    }
                                } // Get Version L4 Failed

                                else
                                {
                                    if (currentChip.ATS != null)
                                    {
                                        if (ByteArrayConverter.SearchBytePattern(currentChip.ATS, new byte[] { 0xC1, 0x05, 0x2F, 0x2F, 0x00, 0x35, 0xC7 }) != 0) //MF PlusS 4K in SL1
                                        {
                                            currentChip.SubType = MifareChipSubType.MifarePlus_SL3_4K;
                                        }

                                        else if (ByteArrayConverter.SearchBytePattern(currentChip.ATS, new byte[] { 0xC1, 0x05, 0x2F, 0x2F, 0x01, 0xBC, 0xD6 }) != 0) //MF PlusX 4K in SL1
                                        {
                                            currentChip.SubType = MifareChipSubType.MifarePlus_SL3_4K;
                                        }
                                        else
                                        {
                                            currentChip.SubType = MifareChipSubType.Unspecified;
                                        }
                                    }
                                    else
                                    {
                                        currentChip.SubType = MifareChipSubType.SmartMX_Mifare_4K;
                                    }
                                } // Mifare Plus
                            } // SAK b6 = 1 ?
                            else
                            {
                                currentChip.SubType = MifareChipSubType.MifareUltralight;
                            } // Ultralight || NTAG
                        }
                    } // SAK b5 = 1 ?
                } // SAK b5 = 1 ?
            }



        }

        #endregion

        #region Reader Communication

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task<bool> ConnectAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    if (twnPort == null)
                    {
                        twnPort = new SerialPort();
                    }

                    // Initialize serial port
                    twnPort.PortName = PortName;
                    twnPort.BaudRate = 9600;
                    twnPort.DataBits = 8;
                    twnPort.StopBits = System.IO.Ports.StopBits.One;
                    twnPort.Parity = System.IO.Ports.Parity.None;
                    // NFC functions are known to take less than 2 second to execute.
                    twnPort.ReadTimeout = 2000;
                    twnPort.WriteTimeout = 2000;
                    twnPort.NewLine = "\r";
                    twnPort.ErrorReceived += TXRXErr;
                    // Open TWN4 com port
                    twnPort.Open();
                }
                catch (Exception e)
                {
                    //Port Busy? Try Again
                    if (e is UnauthorizedAccessException)
                    {
                        if (twnPort.IsOpen)
                        {
                            twnPort.Close();
                        }

                        throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.NotOpen), null);
                    }

                    if (e is IOException)
                    {
                        instance = DeviceManager.GetAvailableReaders();
                    }

                    throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.NotInit), null);
                }
            }).ConfigureAwait(false);

            var version = await GetVersionStringAsync();
            return version.StartsWith("TWN4");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task<bool> DisconnectAsync()
        {
            var wasDisconnected = await Task.Run(() =>
            {
                try
                {
                    if (twnPort == null)
                    {
                        return true;
                    }

                    if (twnPort.IsOpen)
                    {
                        twnPort.DiscardInBuffer();
                        twnPort.DiscardOutBuffer();
                        twnPort.Close();
                        return true;
                    }

                    return true;
                }
                catch (Exception e)
                {
                    //Port Busy? Try Again
                    if (e is UnauthorizedAccessException)
                    {
                        throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.NotOpen), null);
                    }

                    if (e is IOException)
                    {
                        throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.NotInit), null);
                    }

                    throw;
                }
            }).ConfigureAwait(false);

            return wasDisconnected;
        }

        #region Tools for Simple Protocol

        private byte[] GetByteArrayfromPRS(string PRSString)
        {
            // Is string length = 0?
            if (PRSString.Length < 1)
            {
                // Yes, return null
                return null;
            }
            // Initialize byte array, half string length
            byte[] buffer = new byte[PRSString.Length / 2];
            // Get byte array from PRS string
            for (int i = 0; i < (buffer.Length); i++)
            {
                // Convert PRS Chars to byte array buffer
                buffer[i] = byte.Parse(PRSString.Substring((i * 2), 2), NumberStyles.HexNumber);
            }
            // Return byte array
            return buffer;
        }// End of PRStoByteArray

        private static string GetPRSfromByteArray(byte[] PRSStream)
        {
            // Is length of PRS stream = 0
            if (PRSStream.Length < 1)
            {
                // Yes, return null
                return null;
            }
            // Iinitialize PRS buffer
            string buffer = null;
            // Convert byte to PRS string
            for (int i = 0; i < PRSStream.Length; i++)
            {
                // convert byte to characters
                buffer += PRSStream[i].ToString("X2");
            }
            return buffer;
        }// End of GetPRSfromByteArray

        #endregion

        /// <summary>
        /// Call a function of the TWN device in raw format. Sends the bytes provided by CMD and returns the response.
        /// The caller is responsible for providing the correct input sequence, parsing the result and handling the errorCode.
        /// 
        /// Example: 
        /// <code>
        ///     byte[] result = await CallFunctionRawAsync(new byte[] { API_SYS, 3 });
        ///     byte errorCode = result[0];
        /// </code>
        /// </summary>
        /// <param name="CMD">Command to send to the device.</param>
        /// <returns>The response of the device.</returns>
        public async Task<byte[]> CallFunctionRawAsync(byte[] CMD)
        {
            return await DoTXRXAsync(CMD);
        }

        /// <summary>
        /// Call a function of the TWN device in raw format. Sends the bytes provided by CMD and returns a ResponseParser wrapping the response.
        /// The caller is responsible for providing the correct input sequence and handling errors.
        /// 
        /// Example: 
        /// <code>
        ///     var parser = await CallFunctionParserAsync(new byte[] { API_SYS, 3 });
        ///     var errorCode = parser.ParseResponseError();
        ///     if (errorCode != ResponseError.None)
        ///         uint ticks = parser.ParseLong();
        /// </code>
        /// </summary>
        /// <param name="CMD">Command to send to the device.</param>
        /// <returns>A <see cref="ResponseParser"/> wrapping the response of the device.</returns>
        public async Task<ResponseParser> CallFunctionParserAsync(byte[] CMD)
        {
            var result = await CallFunctionRawAsync(CMD);
            var parser = new ResponseParser(result.ToList());
            return parser;
        }

        /// <summary>
        /// Call a function of the TWN device in raw format. Sends the bytes provided by CMD and returns a ResponseParser wrapping the response.
        /// The caller is responsible for providing the correct input sequence.
        /// If the device returns an error, it is thrown as a TwnException. The caller must NOT treat the first parser byte as an error code, as it has already been parsed.
        /// 
        /// Example: 
        /// <code>
        ///   try {
        ///     var parser = await CallFunctionAsync(new byte[] { API_SYS, 3 });
        ///     uint ticks = parser.ParseLong();
        ///   } catch (TwnException e) {...}
        /// </code>
        /// </summary>
        /// <param name="CMD">Command to send to the device.</param>
        /// <returns>A <see cref="ResponseParser"/> wrapping the response of the device.</returns>
        /// <exception cref="TwnException">Is thrown if a communication error occurs.</exception>
        public async Task<ResponseParser> CallFunctionAsync(byte[] CMD)
        {
            var parser = await CallFunctionParserAsync(CMD);
            var errorCode = parser.ParseResponseError();
            if (errorCode != ResponseError.None)
            {
                throw new TwnException("Reader error: " + errorCode, errorCode);
            }
            return parser;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="CMD"></param>
        /// <returns></returns>
        private async Task<byte[]> DoTXRXAsync(byte[] CMD)
        {
            try
            {
                byte[] ret = null;

                return await Task.Run(() => 
                {
                    if (!twnPort.IsOpen)
                    {
                        twnPort.Open();
                    }

                    else if (twnPort.IsOpen)
                    {
                        // Discard com port inbuffer
                        twnPort.DiscardInBuffer();

                        // Generate simple protocol string and send command
                        twnPort.WriteLine(GetPRSfromByteArray(CMD));

                        // Read simple protocoll string and convert to byte array
                        ret = GetByteArrayfromPRS(twnPort.ReadLine());

                        return ret;
                    }

                    return ret;

                }).ConfigureAwait(false);
            }

            catch
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.NotOpen), null);
            }

        }// End of DoTXRXAsync

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TXRXErr(object sender, EventArgs e)
        {
            throw new ReaderException("Call was not successfull, error " + e.ToString(), null);
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// Return the number of Connected Readers
        /// </summary>
        public int AvailableReadersCount => DeviceManager.GetAvailableReaders().Count;

        public bool IsConnected
        {
            get
            {
                if (twnPort != null)
                {
                    return twnPort.IsOpen;
                }

                return false;
            }
        }

        public string PortName
        {
            get; internal set;
        }

        public bool IsTWN4LegicReader
        {
            get; internal set;
        }

        #endregion

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose any managed objects
                    if (twnPort != null)
                    {
                        if (twnPort.IsOpen)
                        {
                            twnPort.Close();
                        }

                        twnPort.Dispose();
                        twnPort = null;
                    }
                }

                Thread.Sleep(20);
                _disposed = true;
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Dispose(true);
        }
    }


}
