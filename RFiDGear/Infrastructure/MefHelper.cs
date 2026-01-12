using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Reflection;
using Serilog;

// Template version 1.2.0.2. Code developed for framework v2.0.50727.3074
// This code is copyright (c) 2009 Computer DJ. All rights reserved.

/// <summary>
/// Managed Extensibility Framework Helper Class
/// </summary>
/// <remarks>
/// Singleton object
/// -- Change Log --------------------------------------------------------
/// 4/5/2009 11:55:06 PM by Rick - Initial Creation
/// 7/15/2009 10:00:00 PM by Rick - Changed to MEF Preview 6 (Net 4.0)
/// 
/// </remarks>
public sealed class MefHelper : IDisposable
{
    private const int SolutionRootDepth = 5;
    private const string ExtensionAssemblySearchPattern = "RFiDGear.Extensions*.dll";

    #region Constructor / Destructor

    /// <summary>
    /// The starting point for this class
    /// </summary>
    /// <remarks></remarks>
    private MefHelper()
    {
        var programDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var assembly = Assembly.GetEntryAssembly();
        var applicationName = assembly?.GetName().Name;

        _ExtensionsPath = BuildExtensionsPath(programDataPath, applicationName);
        EnsureExtensionsDirectoryExists(_ExtensionsPath);
    }

    #region IDisposable Members

    void IDisposable.Dispose()
    {
        _Container.Dispose();
        _Container = null;
    }

    #endregion

    #endregion

    #region Public

    #region Public Property Declarations

    private CompositionContainer _Container = null;
    public CompositionContainer Container => _Container;

    private string _ExtensionsPath = "";
    public string ExtensionsPath
    {
        get => _ExtensionsPath;
        set
        {
            if (!(_ExtensionsPath == value))
            {
                _ExtensionsPath = value;
            }
        }
    }

    #endregion

    #region Public Static Property Declarations

    // Properties
    public static MefHelper Instance
    {
        get
        {
            if ((MefHelper._instance == null))
            {
                lock (MefHelper.syncRoot)
                {
                    if ((MefHelper._instance == null))
                    {
                        MefHelper._instance = new MefHelper();
                    }
                }
            }

            return MefHelper._instance;
        }
    }

    // Fields
    //ModReq(IsVolatile)
    private static MefHelper _instance;

    private static object syncRoot = new object();
    #endregion

    #region Public Methods

    /// <summary>
    /// Composes the application and all MEF attributed components
    /// </summary>
    /// <remarks></remarks>

    public void Compose()
    {
        AggregateCatalog Catalog = new AggregateCatalog();

        // Add This assembly's catalog parts
        System.Reflection.Assembly ass = System.Reflection.Assembly.GetEntryAssembly();
        Catalog.Catalogs.Add(new AssemblyCatalog(ass));

        foreach (var catalogPath in GetExtensionCatalogPaths(AppDomain.CurrentDomain.BaseDirectory, ExtensionsPath))
        {
            TryAddDirectoryCatalog(Catalog, catalogPath);
        }

        _Container = new CompositionContainer(Catalog);
    }

    /// <summary>
    /// Ensures that the MEF container has been composed exactly once.
    /// </summary>
    public void EnsureCompose()
    {
        if (_Container == null)
        {
            Compose();
        }
    }

    /// <summary>
    /// Returns extension catalog paths that should be scanned for UI extensions.
    /// </summary>
    /// <param name="baseDirectory">The base directory of the running application.</param>
    /// <param name="extensionsPath">The configured extensions directory.</param>
    /// <returns>A list of existing directories to scan for extensions.</returns>
    internal static IReadOnlyList<string> GetExtensionCatalogPaths(string baseDirectory, string extensionsPath)
    {
        var paths = new List<string>();

        if (!string.IsNullOrWhiteSpace(extensionsPath) && Directory.Exists(extensionsPath))
        {
            paths.Add(extensionsPath);
        }

#if DEBUG
        var devPath = FindDevelopmentExtensionsPath(baseDirectory);
        if (!string.IsNullOrWhiteSpace(devPath))
        {
            paths.Add(devPath);
        }
#endif

        return paths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Attempts to locate the VCNEditor build output path during local development.
    /// </summary>
    /// <param name="baseDirectory">The base directory of the running application.</param>
    /// <returns>The path to the VCNEditor output directory, or <c>null</c> if not found.</returns>
    internal static string FindDevelopmentExtensionsPath(string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            return null;
        }

        var solutionRoot = GetSolutionRoot(baseDirectory, SolutionRootDepth);
        if (string.IsNullOrWhiteSpace(solutionRoot))
        {
            return null;
        }

        var baseOutputPath = Path.Combine(solutionRoot, "VCNEditor", "bin", "Debug");
        var candidates = new[]
        {
            Path.Combine(baseOutputPath, "net8.0-windows"),
            baseOutputPath
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Builds the default extensions directory path for the application.
    /// </summary>
    /// <param name="programDataPath">The ProgramData path where shared data is stored.</param>
    /// <param name="applicationName">The application name used for the extensions folder.</param>
    /// <returns>The full path to the extensions folder.</returns>
    internal static string BuildExtensionsPath(string programDataPath, string applicationName)
    {
        if (string.IsNullOrWhiteSpace(programDataPath) || string.IsNullOrWhiteSpace(applicationName))
        {
            return string.Empty;
        }

        return Path.Combine(programDataPath, applicationName, "Extensions");
    }

    private static string GetSolutionRoot(string baseDirectory, int depth)
    {
        var current = new DirectoryInfo(baseDirectory);

        for (var i = 0; i < depth; i++)
        {
            current = current?.Parent;
            if (current == null)
            {
                break;
            }
        }

        return current?.FullName;
    }

    private static void EnsureExtensionsDirectoryExists(string extensionsPath)
    {
        if (string.IsNullOrWhiteSpace(extensionsPath))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(extensionsPath);
        }
        catch (Exception e)
        {
            Log.ForContext<MefHelper>()
                .Error(e, "Failed to create extensions directory at {ExtensionsPath}", extensionsPath);
        }
    }

    private static void TryAddDirectoryCatalog(AggregateCatalog catalog, string catalogPath)
    {
        try
        {
            foreach (var assemblyPath in GetExtensionAssemblyPaths(catalogPath))
            {
                catalog.Catalogs.Add(new AssemblyCatalog(assemblyPath));
            }
        }
        catch (Exception e)
        {
            Log.ForContext<MefHelper>()
                .Error(e, "Failed to compose extensions from {ExtensionsPath}", catalogPath);
        }
    }

    /// <summary>
    /// Returns extension assembly files from a directory that match the extension naming convention.
    /// </summary>
    /// <param name="catalogPath">The directory path to scan for extension assemblies.</param>
    /// <returns>A list of extension assembly file paths.</returns>
    internal static IReadOnlyList<string> GetExtensionAssemblyPaths(string catalogPath)
    {
        if (string.IsNullOrWhiteSpace(catalogPath) || !Directory.Exists(catalogPath))
        {
            return Array.Empty<string>();
        }

        return Directory.EnumerateFiles(catalogPath, ExtensionAssemblySearchPattern, SearchOption.TopDirectoryOnly)
            .ToList();
    }

    #endregion

    #endregion

}
