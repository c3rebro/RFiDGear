using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.IO;
using System.Linq;
using System.Reflection;
using RFiDGear.Contracts;
using RFiDGear.ExtensionSdk;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.DI.Interfaces;
using RFiDGear.UI.UIExtensions.Interfaces;
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

    // Null means "the entry assembly", resolved when composing (the singleton is created before it is needed).
    private readonly Assembly _hostAssembly;
    private readonly Func<string, ComposablePartCatalog> _catalogFactory;
    private IReadOnlyList<ExtensionLoadResult> _lastLoadResults = Array.Empty<ExtensionLoadResult>();

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
        _catalogFactory = CreateAssemblyCatalog;
        EnsureExtensionsDirectoryExists(_ExtensionsPath);
    }

    /// <summary>
    /// Creates an instance that composes from an explicit host assembly and extensions folder. This is the test
    /// seam: it never creates the folder and never touches the singleton.
    /// </summary>
    /// <param name="extensionsPath">The folder scanned for <c>RFiDGear.Extensions*.dll</c>.</param>
    /// <param name="hostAssembly">The assembly whose exports stand in for the application's own.</param>
    /// <param name="catalogFactory">
    /// Builds the catalog for one extension file; defaults to loading the assembly from that path.
    /// </param>
    internal MefHelper(string extensionsPath, Assembly hostAssembly, Func<string, ComposablePartCatalog> catalogFactory = null)
    {
        _ExtensionsPath = extensionsPath ?? string.Empty;
        _hostAssembly = hostAssembly ?? throw new ArgumentNullException(nameof(hostAssembly));
        _catalogFactory = catalogFactory ?? CreateAssemblyCatalog;
    }

    #region IDisposable Members

    void IDisposable.Dispose()
    {
        _Container?.Dispose();
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
    /// What happened to each extension assembly during the last <see cref="Compose"/>: loaded, or skipped with the
    /// reason. Empty before the first composition.
    /// </summary>
    internal IReadOnlyList<ExtensionLoadResult> LastLoadResults => _lastLoadResults;

    /// <summary>
    /// Composes the application and all MEF attributed components
    /// </summary>
    /// <remarks>
    /// A broken extension never prevents the others, or the application, from loading. Each extension is probed
    /// before it is added; one that fails is logged and skipped. If the extensions that passed still break the
    /// export queries the host relies on, the container is rebuilt from the host alone.
    /// </remarks>
    public void Compose()
    {
        var hostAssembly = _hostAssembly ?? Assembly.GetEntryAssembly();
        var results = new List<ExtensionLoadResult>();

        var container = BuildContainer(hostAssembly, results);
        var failure = FindExportQueryFailure(container);

        if (failure != null)
        {
            Log.ForContext<MefHelper>()
                .Error(failure, "The loaded extensions break the MEF export queries; continuing without extensions");

            container.Dispose();
            container = BuildContainer(hostAssembly, null);
            results = results
                .Select(result => result.Loaded ? result with { Loaded = false, Error = failure } : result)
                .ToList();
        }

        _lastLoadResults = results;
        _Container = container;
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
        foreach (var devPath in FindDevelopmentExtensionsPaths(baseDirectory))
        {
            paths.Add(devPath);
        }
#endif

        return paths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Attempts to locate extension build output paths during local development.
    /// </summary>
    /// <param name="baseDirectory">The base directory of the running application.</param>
    /// <returns>A list of extension output directories, or an empty list if none are found.</returns>
    internal static IReadOnlyList<string> FindDevelopmentExtensionsPaths(string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            return Array.Empty<string>();
        }

        var solutionRoot = GetSolutionRoot(baseDirectory, SolutionRootDepth);
        if (string.IsNullOrWhiteSpace(solutionRoot))
        {
            return Array.Empty<string>();
        }

        var candidatePaths = new List<string>();

        foreach (var projectRoot in GetDevelopmentExtensionProjectRoots(solutionRoot))
        {
            foreach (var candidate in GetBuildOutputCandidates(projectRoot))
            {
                if (Directory.Exists(candidate))
                {
                    candidatePaths.Add(candidate);
                }
            }
        }

        return candidatePaths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
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

    private static IEnumerable<string> GetDevelopmentExtensionProjectRoots(string solutionRoot)
    {
        var projectRoots = new List<string>();
        var extensionsRoot = Path.Combine(solutionRoot, "RFiDGear.Extensions");
        if (Directory.Exists(extensionsRoot))
        {
            projectRoots.AddRange(Directory.EnumerateDirectories(extensionsRoot));
        }

        return projectRoots;
    }

    private static IEnumerable<string> GetBuildOutputCandidates(string projectRoot)
    {
        var baseOutputPath = Path.Combine(projectRoot, "bin", "Debug");

        yield return Path.Combine(baseOutputPath, "net8.0-windows");
        yield return baseOutputPath;
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

    private static ComposablePartCatalog CreateAssemblyCatalog(string assemblyPath)
    {
        return new AssemblyCatalog(assemblyPath);
    }

    /// <summary>
    /// Builds a container from the host assembly and, when <paramref name="results"/> is given, the extensions.
    /// </summary>
    private CompositionContainer BuildContainer(Assembly hostAssembly, List<ExtensionLoadResult> results)
    {
        var catalog = new AggregateCatalog();
        catalog.Catalogs.Add(new AssemblyCatalog(hostAssembly));

        if (results != null)
        {
            TryAddDirectoryCatalog(catalog, ExtensionsPath, results);
        }

        return new CompositionContainer(catalog);
    }

    private void TryAddDirectoryCatalog(AggregateCatalog catalog, string catalogPath, List<ExtensionLoadResult> results)
    {
        IReadOnlyList<string> assemblyPaths;

        try
        {
            assemblyPaths = GetExtensionAssemblyPaths(catalogPath);
        }
        catch (Exception e)
        {
            Log.ForContext<MefHelper>()
                .Error(e, "Failed to compose extensions from {ExtensionsPath}", catalogPath);
            return;
        }

        // One try/catch per file: a bad extension must not stop the ones after it.
        foreach (var assemblyPath in assemblyPaths)
        {
            results.Add(TryAddExtensionCatalog(catalog, assemblyPath));
        }
    }

    private ExtensionLoadResult TryAddExtensionCatalog(AggregateCatalog catalog, string assemblyPath)
    {
        ComposablePartCatalog candidate = null;

        try
        {
            candidate = _catalogFactory(assemblyPath);

            // MEF evaluates parts, metadata and imports lazily, so a broken extension would otherwise surface much
            // later, inside whichever view model happens to compose first. Force it now, before the catalog is added.
            var partCount = ProbeCatalog(candidate);

            catalog.Catalogs.Add(candidate);
            Log.ForContext<MefHelper>()
                .Information("Loaded extension {ExtensionAssembly} with {PartCount} MEF parts", assemblyPath, partCount);

            return new ExtensionLoadResult(assemblyPath, true, partCount, null);
        }
        catch (Exception e)
        {
            TryDispose(candidate);
            Log.ForContext<MefHelper>()
                .Error(e, "Extension {ExtensionAssembly} could not be loaded and was skipped", assemblyPath);

            return new ExtensionLoadResult(assemblyPath, false, 0, e);
        }
    }

    /// <summary>
    /// Forces everything MEF would otherwise evaluate lazily: the parts, each export's contract and metadata, and
    /// each import's contract. Never instantiates a part.
    /// </summary>
    /// <returns>The number of parts in the catalog.</returns>
    private static int ProbeCatalog(ComposablePartCatalog catalog)
    {
        var parts = catalog.Parts.ToList();

        foreach (var part in parts)
        {
            foreach (var export in part.ExportDefinitions)
            {
                _ = export.ContractName;
                _ = export.Metadata.Count;
            }

            foreach (var import in part.ImportDefinitions)
            {
                _ = import.ContractName;
            }
        }

        return parts.Count;
    }

    /// <summary>
    /// Runs the export queries the host depends on, without instantiating anything.
    /// </summary>
    /// <returns>The exception the queries threw, or null when they work.</returns>
    private static Exception FindExportQueryFailure(CompositionContainer container)
    {
        try
        {
            foreach (var export in container.GetExports<IUIExtension, IUIExtensionDetails>())
            {
                _ = export.Metadata.Name;
            }

            foreach (var export in container.GetExports<object, IViewModelMetadata>("ViewModel"))
            {
                _ = export.Metadata.Name;
            }

            return FindProviderQueryFailure(container);
        }
        catch (Exception e)
        {
            return e;
        }
    }

    /// <summary>
    /// The query for secure key providers. What an extension throws while its provider export is evaluated is code of
    /// that extension, and the host promises never to print what a provider throws: the failure that comes back keeps
    /// the type of the original and nothing else, and does not keep the original as an inner exception.
    /// </summary>
    private static Exception FindProviderQueryFailure(CompositionContainer container)
    {
        try
        {
            foreach (var export in container.GetExports<ISecureKeyProvider, ISecureKeyProviderMetadata>())
            {
                _ = export.Metadata.Id;
            }

            return null;
        }
        catch (Exception e)
        {
            return new InvalidOperationException($"The secure key provider exports could not be listed ({e.GetType().FullName}).");
        }
    }

    private static void TryDispose(ComposablePartCatalog catalog)
    {
        try
        {
            catalog?.Dispose();
        }
        catch (Exception e)
        {
            Log.ForContext<MefHelper>().Warning(e, "Failed to dispose a discarded extension catalog");
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
