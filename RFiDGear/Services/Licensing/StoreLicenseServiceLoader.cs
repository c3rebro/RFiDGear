using System;
using System.IO;
using System.Reflection;

using Serilog;

namespace RFiDGear.Services.Licensing
{
    /// <summary>
    /// Finds the license service of the Store edition. It lives in <c>RFiDGear.Store.dll</c>, an assembly of its own that the
    /// package carries next to the program: it needs a Windows-versioned target framework to reach the Store, and the program
    /// (and its tests and extensions) must not move to that framework with it. The program therefore does not reference it; it
    /// loads it by name when it runs from a package.
    /// </summary>
    public static class StoreLicenseServiceLoader
    {
        /// <summary>The file name of the Store assembly.</summary>
        internal const string AssemblyFileName = "RFiDGear.Store.dll";

        /// <summary>The full name of the license service in that assembly.</summary>
        internal const string ServiceTypeName = "RFiDGear.Store.StoreLicenseService";

        /// <summary>
        /// Loads the Store license service from the folder of the program. Any failure (the file is missing, it cannot be
        /// loaded, the service cannot be made) is logged by type and gives <see langword="null"/>: the program then runs as if
        /// it needed no license (it fails open, as the gate does for a license that cannot be read).
        /// </summary>
        /// <param name="baseDirectory">The folder of the executable.</param>
        /// <returns>The service; <see langword="null"/> when there is none.</returns>
        public static ILicenseService TryLoad(string baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                return null;
            }

            var path = Path.Combine(baseDirectory, AssemblyFileName);

            if (!File.Exists(path))
            {
                Log.ForContext(typeof(StoreLicenseServiceLoader)).Warning("The Store assembly is not in the package");
                return null;
            }

            try
            {
                return CreateFrom(Assembly.LoadFrom(path));
            }
            catch (Exception ex)
            {
                // Only the type: the text of a load failure holds paths.
                Log.ForContext(typeof(StoreLicenseServiceLoader)).Warning("The Store assembly could not be loaded ({ExceptionType})", ex.GetType().Name);
                return null;
            }
        }

        /// <summary>
        /// Makes the license service from an assembly: the type <c>RFiDGear.Store.StoreLicenseService</c> with a constructor
        /// that takes nothing.
        /// </summary>
        /// <param name="assembly">The assembly that holds the service.</param>
        /// <returns>The service; <see langword="null"/> when the assembly has no such type.</returns>
        internal static ILicenseService CreateFrom(Assembly assembly)
        {
            var type = assembly?.GetType(ServiceTypeName, throwOnError: false);

            if (type == null || !typeof(ILicenseService).IsAssignableFrom(type) || type.GetConstructor(Type.EmptyTypes) == null)
            {
                Log.ForContext(typeof(StoreLicenseServiceLoader)).Warning("The Store assembly holds no license service");
                return null;
            }

            return (ILicenseService)Activator.CreateInstance(type);
        }
    }
}
