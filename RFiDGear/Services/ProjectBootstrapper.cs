using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using RFiDGear;
using RFiDGear.Models;
using RFiDGear.Services.Interfaces;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.FileAccess;
using Serilog;

namespace RFiDGear.Services
{
    public class ProjectBootstrapper : IProjectBootstrapper
    {
        private static readonly ILogger Logger = Log.ForContext<ProjectBootstrapper>();
        private readonly Func<SettingsReaderWriter> settingsFactory;

        public ProjectBootstrapper()
            : this(() => new SettingsReaderWriter())
        {
        }

        public ProjectBootstrapper(Func<SettingsReaderWriter> settingsFactory)
        {
            this.settingsFactory = settingsFactory ?? throw new ArgumentNullException(nameof(settingsFactory));
        }

        public async Task BootstrapAsync(ProjectBootstrapRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            try
            {
                using (var settings = settingsFactory())
                {
                    await settings.ReadSettingsAsync();

                    settings.InitUpdateFile();

                    request.SetCurrentReader?.Invoke(string.IsNullOrWhiteSpace(settings.DefaultSpecification.DefaultReaderName)
                        ? Enum.GetName(typeof(ReaderTypes), settings.DefaultSpecification.DefaultReaderProvider)
                        : settings.DefaultSpecification.DefaultReaderName);

                    // This is the authoritative settings read immediately before AUTORUN.
                    // Keep the static provider in sync, not just the UI label.
                    request.SetReaderProvider?.Invoke(settings.DefaultSpecification.DefaultReaderProvider);

                    if (int.TryParse(settings.DefaultSpecification.LastUsedComPort, out var portNumber))
                    {
                        request.SetReaderPort?.Invoke(portNumber);
                    }
                    else
                    {
                        request.SetReaderPort?.Invoke(0);
                    }

                    request.SetCulture?.Invoke(settings.DefaultSpecification.DefaultLanguage == "german"
                        ? new CultureInfo("de-DE")
                        : new CultureInfo("en-US"));

                    var autoLoadLastUsedDB = settings.DefaultSpecification.AutoLoadProjectOnStart;
                    var shouldOpenProject = ShouldOpenProject(request, autoLoadLastUsedDB);

                    if (ShouldShowSplash(request, shouldOpenProject))
                    {
                        request.AddDialog(request.CreateSplashScreen());
                    }

                    if (shouldOpenProject && request.OpenProjectAsync != null)
                    {
                        await OpenProjectAsync(request);
                        request.RemoveSplash?.Invoke();
                    }

                    _ = Task.Run(async () =>
                    {
                        while (true)
                        {
                            await Task.Delay(300).ConfigureAwait(false);
                            request.UpdateDateTime?.Invoke(string.Format("{0}", DateTime.Now));
                        }
                    });

                    if (request.ResetTaskStatusAsync != null)
                    {
                        await request.ResetTaskStatusAsync();
                    }

                    if (request.Autorun && request.ReadChipAsync != null && request.WriteOnceAsync != null)
                    {
                        await request.ReadChipAsync();
                        await request.WriteOnceAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Bootstrap failed");
            }
        }

        private static bool ShouldOpenProject(ProjectBootstrapRequest request, bool autoLoadLastUsedDB)
        {
            return autoLoadLastUsedDB || request.Autorun || !string.IsNullOrWhiteSpace(request.ProjectFilePath);
        }

        private static bool ShouldShowSplash(ProjectBootstrapRequest request, bool shouldOpenProject)
        {
            return shouldOpenProject && request.CreateSplashScreen != null && request.AddDialog != null;
        }

        private static async Task OpenProjectAsync(ProjectBootstrapRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.ProjectFilePath) && !File.Exists(request.ProjectFilePath))
            {
                Logger.Warning("Project file not found at path {ProjectFilePath}", request.ProjectFilePath);
                return;
            }

            if (string.IsNullOrWhiteSpace(request.ProjectFilePath))
            {
                Logger.Warning("Project file path not provided, opening the last used project instead.");
            }

            try
            {
                await request.OpenProjectAsync(request.ProjectFilePath);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to open project file {ProjectFilePath}", request.ProjectFilePath);
            }
        }
    }
}
