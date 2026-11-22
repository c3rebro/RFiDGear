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
using RFiDGear.Infrastructure.Editions;
using RFiDGear.Infrastructure.ExtensionPackages;
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

    // The folder of the extensions that the Store package carries, next to the executable.
    private const string PackagedExtensionsFolderName = "Extensions";

    // Null means "the entry assembly", resolved when composing (the singleton is created before it is needed).
    private readonly Assembly _hostAssembly;
    private readonly Func<string, ComposablePartCatalog> _catalogFactory;
    private IReadOnlyList<ExtensionLoadResult> _lastLoadResults = Array.Empty<ExtensionLoadResult>();

    // The folder of the extensions that were installed from a package for this user (see ExtensionPackageLayout); blank for none.
    private readonly string _userExtensionsPath;

    // What was chosen to be loaded, and the file that was loaded for each extension. Read by the thread that resolves an
    // assembly by name, which may not be the one that composes.
    private readonly object _selectionLock = new object();
    private readonly Dictionary<string, string> _loadedPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<SelectedExtension> _selection;

    // The guard of the user's folder of installed extensions, when this process took it (and with it the right to tidy it).
    private ExtensionFolderGuard _folderGuard;

    /// <summary>
    /// The assembly of the application itself, whose exports the container starts with and whose name names the extensions
    /// folder. It is this assembly and not the entry assembly of the process: the two are the same in the application, but when
    /// the code runs under a test project that is an executable (xunit.v3 test projects are), the entry assembly is the test
    /// assembly, whose deliberately broken exports would then be composed into every container.
    /// </summary>
    private static Assembly HostAssembly => typeof(MefHelper).Assembly;

    #region Constructor / Destructor

    /// <summary>
    /// The starting point for this class
    /// </summary>
    /// <remarks></remarks>
    private MefHelper()
        : this(
            EditionPolicy.Current,
            AppContext.BaseDirectory,
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            ExtensionPackageLayout.DefaultUserRoot())
    {
    }

    /// <summary>
    /// Chooses where the extensions come from for an edition. The standard edition scans the machine-wide folder under
    /// <c>%ProgramData%</c> (which it creates) and the user's folder of installed packages. The Store edition scans only the
    /// <c>Extensions</c> folder inside its package, which is read-only and is not created, and has no folder of installed
    /// packages: nothing that was added after the Store signed the package is loaded.
    /// </summary>
    /// <param name="edition">The edition the process runs as.</param>
    /// <param name="baseDirectory">The folder of the executable.</param>
    /// <param name="programDataPath">The <c>%ProgramData%</c> folder.</param>
    /// <param name="userExtensionsRoot">The user's folder of installed extension packages.</param>
    internal MefHelper(IEditionPolicy edition, string baseDirectory, string programDataPath, string userExtensionsRoot)
    {
        if (edition == null)
        {
            throw new ArgumentNullException(nameof(edition));
        }

        _catalogFactory = CreateAssemblyCatalog;

        if (edition.AllowExternalExtensions)
        {
            _ExtensionsPath = BuildExtensionsPath(programDataPath, HostAssembly.GetName().Name);
            _userExtensionsPath = userExtensionsRoot;
            EnsureExtensionsDirectoryExists(_ExtensionsPath);
        }
        else
        {
            _ExtensionsPath = BuildPackagedExtensionsPath(baseDirectory);
            _userExtensionsPath = string.Empty;
        }
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
    /// <param name="userExtensionsPath">
    /// The folder of the extensions installed from a package; none by default, so a test sees only what it puts in the folder
    /// it names.
    /// </param>
    internal MefHelper(string extensionsPath, Assembly hostAssembly, Func<string, ComposablePartCatalog> catalogFactory = null, string userExtensionsPath = null)
    {
        _ExtensionsPath = extensionsPath ?? string.Empty;
        _hostAssembly = hostAssembly ?? throw new ArgumentNullException(nameof(hostAssembly));
        _catalogFactory = catalogFactory ?? CreateAssemblyCatalog;
        _userExtensionsPath = userExtensionsPath ?? string.Empty;
    }

    #region IDisposable Members

    void IDisposable.Dispose()
    {
        _Container?.Dispose();
        _Container = null;

        // The application's helper lives until the process ends; one that is disposed earlier (a test) lets go of the folder.
        _folderGuard?.Dispose();
        _folderGuard = null;
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
        var hostAssembly = _hostAssembly ?? HostAssembly;
        var results = new List<ExtensionLoadResult>();

        var container = BuildContainer(hostAssembly, results);
        var failure = FindExportQueryFailure(container);

        if (failure != null)
        {
            Log.ForContext<MefHelper>()
                .Error(failure, "The loaded extensions break the MEF export queries; continuing without extensions");

            // The copies that the user installed were part of what broke the queries: they are not chosen again, and the next older
            // version or the machine-wide copy comes back at the next start.
            MarkInstalledVersions(results, ExtensionPackageLayout.FailedMarkerFileName, "This version of the extension broke the export queries of RFiDGear; RFiDGear does not choose it again.");

            container.Dispose();
            container = BuildContainer(hostAssembly, null);
            results = results
                .Select(result => result.Loaded ? result with { Loaded = false, Error = failure } : result)
                .ToList();
        }
        else
        {
            // Only now is it known that they work: a version is marked as one to go back to after the whole composition held.
            MarkInstalledVersions(results, ExtensionPackageLayout.LoadedMarkerFileName, "This version of the extension was loaded without a problem.");
        }

        _lastLoadResults = results;
        _Container = container;
    }

    // Leaves a marker in the version folder of each installed copy that was loaded (see ExtensionPackageLayout).
    private static void MarkInstalledVersions(IEnumerable<ExtensionLoadResult> results, string markerFileName, string text)
    {
        foreach (var result in results.Where(result => result.Loaded && result.IsUserInstalled))
        {
            MarkVersionFolder(result.AssemblyPath, markerFileName, text);
        }
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

    /// <summary>
    /// Builds the path of the extensions that a Store package carries.
    /// </summary>
    /// <param name="baseDirectory">The folder of the executable inside the package.</param>
    /// <returns>The full path to the <c>Extensions</c> folder next to it; empty when there is no base directory.</returns>
    internal static string BuildPackagedExtensionsPath(string baseDirectory)
    {
        return string.IsNullOrWhiteSpace(baseDirectory)
            ? string.Empty
            : Path.Combine(baseDirectory, PackagedExtensionsFolderName);
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

        // The extension projects name their framework with and without the platform version.
        yield return Path.Combine(baseOutputPath, "net10.0-windows7.0");
        yield return Path.Combine(baseOutputPath, "net10.0-windows");
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
            TryAddExtensions(catalog, results);
        }

        return new CompositionContainer(catalog);
    }

    private void TryAddExtensions(AggregateCatalog catalog, List<ExtensionLoadResult> results)
    {
        IReadOnlyList<SelectedExtension> selection;

        try
        {
            // Before anything of the user's folder is loaded: what an install that was cut short left, and the old versions. Only
            // the first RFiDGear tidies: a second one that is started meanwhile composes too, before it finds out and exits, and
            // must not take away what the first one uses or is asking about.
            if (Directory.Exists(_userExtensionsPath))
            {
                var guard = ExtensionFolderGuard.For(_userExtensionsPath);
                if (guard.TryTake())
                {
                    _folderGuard = guard;
                    ExtensionPackageInstaller.Tidy(_userExtensionsPath);
                }
            }

            selection = SelectExtensions();
        }
        catch (Exception e)
        {
            Log.ForContext<MefHelper>()
                .Error(e, "Failed to compose extensions from {ExtensionsPath}", ExtensionsPath);
            return;
        }

        // One try/catch per file: a bad extension must not stop the ones after it. An extension that is installed twice is
        // loaded once: the copy that is chosen, and only if that one cannot be loaded the other. That works when the file could
        // not be read at all; an assembly that was loaded and whose parts then failed stays loaded in this run, and another copy
        // of the same assembly cannot take its place. So a copy that the user installed and that failed is marked, and the next
        // start does not choose it again.
        foreach (var extension in selection)
        {
            foreach (var candidate in extension.Candidates)
            {
                var result = TryAddExtensionCatalog(catalog, candidate);
                results.Add(result);
                if (result.Loaded)
                {
                    lock (_selectionLock)
                    {
                        _loadedPaths[Path.GetFileNameWithoutExtension(extension.FileName)] = candidate.AssemblyPath;
                    }

                    break;
                }

                if (candidate.IsUserInstalled)
                {
                    MarkVersionFolder(candidate.AssemblyPath, ExtensionPackageLayout.FailedMarkerFileName, "This version of the extension could not be loaded; RFiDGear does not choose it again.");
                    Log.ForContext<MefHelper>()
                        .Warning("The installed extension {ExtensionAssembly} could not be loaded; this version is skipped from the next start", candidate.AssemblyPath);
                }
            }
        }
    }

    // Leaves a marker file in the version folder of an installed copy (see ExtensionPackageLayout). A folder that cannot be written
    // gets none, and is treated as before.
    private static void MarkVersionFolder(string assemblyPath, string markerFileName, string text)
    {
        try
        {
            var folder = Path.GetDirectoryName(assemblyPath);
            if (!string.IsNullOrEmpty(folder))
            {
                File.WriteAllText(Path.Combine(folder, markerFileName), text);
            }
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            Log.ForContext<MefHelper>().Warning(e, "Could not leave {Marker} in the folder of {ExtensionAssembly}", markerFileName, assemblyPath);
        }
    }

    // What to load: the extensions of the machine-wide folder and the ones installed for the user, one file for each.
    private IReadOnlyList<SelectedExtension> SelectExtensions()
    {
        var selection = ExtensionSelector.Select(GetExtensionAssemblyPaths(ExtensionsPath), _userExtensionsPath, ExtensionPackageLayout.FileVersionOf);
        lock (_selectionLock)
        {
            _selection = selection;
        }

        return selection;
    }

    /// <summary>
    /// Gets the file of an extension assembly by its name: the one that is loaded, or if nothing is loaded yet the one that is
    /// chosen, whether it is in the machine-wide folder or in the user's. The resolver of assemblies asks, so that an extension
    /// that was loaded from the user's folder is not loaded a second time from the machine-wide one.
    /// </summary>
    /// <param name="assemblyName">The simple name of the assembly, for example <c>RFiDGear.Extensions.CardCheckAssistant</c>.</param>
    /// <returns>The full path; <c>null</c> when no such extension is installed.</returns>
    internal string FindExtensionAssembly(string assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            return null;
        }

        IReadOnlyList<SelectedExtension> selection;
        lock (_selectionLock)
        {
            if (_loadedPaths.TryGetValue(assemblyName, out var loaded))
            {
                return loaded;
            }

            selection = _selection;
        }

        try
        {
            selection ??= SelectExtensions();
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            return null;
        }

        return selection
            .FirstOrDefault(extension => string.Equals(Path.GetFileNameWithoutExtension(extension.FileName), assemblyName, StringComparison.OrdinalIgnoreCase))
            ?.Candidates[0].AssemblyPath;
    }

    private ExtensionLoadResult TryAddExtensionCatalog(AggregateCatalog catalog, ExtensionCandidate extension)
    {
        var assemblyPath = extension.AssemblyPath;
        ComposablePartCatalog candidate = null;

        try
        {
            candidate = _catalogFactory(assemblyPath);

            // MEF evaluates parts, metadata and imports lazily, so a broken extension would otherwise surface much
            // later, inside whichever view model happens to compose first. Force it now, before the catalog is added.
            var partCount = ProbeCatalog(candidate);

            catalog.Catalogs.Add(candidate);
            Log.ForContext<MefHelper>()
                .Information(
                    "Loaded extension {ExtensionAssembly} {ExtensionVersion} ({ExtensionSource}) with {PartCount} MEF parts",
                    assemblyPath,
                    extension.Version?.ToString() ?? "unknown",
                    extension.IsUserInstalled ? "installed for this user" : "machine-wide",
                    partCount);

            return new ExtensionLoadResult(assemblyPath, true, partCount, null, extension.IsUserInstalled, extension.Version);
        }
        catch (Exception e)
        {
            TryDispose(candidate);
            Log.ForContext<MefHelper>()
                .Error(e, "Extension {ExtensionAssembly} could not be loaded and was skipped", assemblyPath);

            return new ExtensionLoadResult(assemblyPath, false, 0, e, extension.IsUserInstalled, extension.Version);
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
