using Microsoft.Win32;
using System.Collections.Generic;

namespace Elatec.NET
{
    public class DeviceManager
    {

        public static readonly string VendorIdElatec = "09D8";
        public static readonly string ProductIdTWN4MultiTech2 = "0420";
        public static readonly string ServiceUsbSerial = "usbser";

        public static List<TWN4ReaderDevice> GetAvailableReaders()
        {
            try
            {
                var readers = new List<TWN4ReaderDevice>();

                foreach (string deviceInstanceId in FindUsbDevices(ServiceUsbSerial, VendorIdElatec, ProductIdTWN4MultiTech2) ?? new List<string> { "" })
                {
                    var portName = RegQuerySZ($"SYSTEM\\CurrentControlSet\\Enum\\{deviceInstanceId}\\Device Parameters", "PortName");
                    var reader = new TWN4ReaderDevice(portName);
                    readers.Add(reader);
                }

                return readers;
            }
            catch 
            {
                return null;
            }

        }

        /// <summary>
        /// Find USB devices of a specified kind.
        /// </summary>
        /// <param name="service">e.g. "usbser"</param>
        /// <param name="vendorId"></param>
        /// <param name="productId"></param>
        /// <returns>A list of deviceInstanceIds.</returns>
        public static List<string> FindUsbDevices(string service, string vendorId, string productId)
        {
            RegistryKey registryKey = null;
            var devices = new List<string>();
            string devicePath = $"USB\\VID_{vendorId}&PID_{productId}\\";

            try
            {
                registryKey = Registry.LocalMachine.OpenSubKey($"SYSTEM\\CurrentControlSet\\Services\\{service}\\Enum");
                if (registryKey == null)
                {
                    return null;
                }
                string[] valueNames = registryKey.GetValueNames();
                for (int i = 0; i < valueNames.Length; i++)
                {
                    object value = registryKey.GetValue(valueNames[i]);
                    if (value is string device && device.StartsWith(devicePath))
                    {
                        devices.Add(device);
                    }
                }
            }
            finally
            {
                registryKey?.Close();

            }
            return devices;
        }

        private static string RegQuerySZ(string subKeyName, string valueName)
        {
            RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(subKeyName);
            if (registryKey == null)
            {
                return null;
            }
            object value = registryKey.GetValue(valueName);
            registryKey.Close();
            return value?.ToString();
        }
    }
}
