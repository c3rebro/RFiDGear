using System;
using System.Globalization;
using System.Threading.Tasks;
using RFiDGear.DataAccessLayer;
using RFiDGear;
using RFiDGear.Model;
using RFiDGear.Services.Interfaces;

namespace RFiDGear.Services
{
    public class ProjectBootstrapper : IProjectBootstrapper
    {
        public async Task BootstrapAsync(ProjectBootstrapRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            try
            {
                using (var settings = new SettingsReaderWriter())
                {
                    await settings.ReadSettingsAsync();

                    settings.InitUpdateFile();

                    request.SetCurrentReader?.Invoke(string.IsNullOrWhiteSpace(settings.DefaultSpecification.DefaultReaderName)
                        ? Enum.GetName(typeof(ReaderTypes), settings.DefaultSpecification.DefaultReaderProvider)
                        : settings.DefaultSpecification.DefaultReaderName);

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

                    if (autoLoadLastUsedDB && request.CreateSplashScreen != null && request.AddDialog != null)
                    {
                        request.AddDialog(request.CreateSplashScreen());
                    }

                    if (autoLoadLastUsedDB && request.OpenProjectAsync != null)
                    {
                        await request.OpenProjectAsync(request.ProjectFilePath);
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
                System.Diagnostics.Trace.TraceError(ex.Message);
            }
        }
    }
}
