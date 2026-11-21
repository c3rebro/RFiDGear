using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using GemBox.Pdf;
using GemBox.Pdf.Forms;

using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.ViewModel.TaskSetupViewModels;

namespace RFiDGear.Tests
{
    /// <summary>
    /// What a test needs to run the real report task on a real (small) PDF form: a template with text fields, the real report
    /// writer on a folder of its own, and the fields of the report that came out. The writer keeps its work files in that
    /// folder, so nothing is written to the profile of whoever runs the tests.
    /// </summary>
    internal sealed class ReportTestKit : IDisposable
    {
        private readonly string _folder = Directory.CreateTempSubdirectory("ReportTestKit").FullName;

        /// <summary>The folder everything of the kit is in.</summary>
        public string Folder => _folder;

        /// <summary>Makes a one-page template with a text field for each name.</summary>
        /// <param name="fields">The names of the fields.</param>
        /// <returns>The path of the template.</returns>
        public string Template(params string[] fields)
        {
            ComponentInfo.SetLicense("FREE-LIMITED-KEY");
            using var document = new PdfDocument();
            var page = document.Pages.Add();
            var top = 700.0;

            foreach (var name in fields)
            {
                document.Form.Fields.AddText(page, 50, top, 200, 20).Name = name;
                top -= 30;
            }

            var path = Path.Combine(_folder, "template.pdf");
            document.Save(path);

            return path;
        }

        /// <summary>Runs the report task with the real writer and reads the report back.</summary>
        /// <param name="task">The report task, with its checkpoints.</param>
        /// <param name="template">The template of <see cref="Template"/>.</param>
        /// <param name="outputName">The file name of the report in the folder of the kit.</param>
        /// <returns>The value of each field of the report that was written (empty when no report was written).</returns>
        public async Task<Dictionary<string, string?>> RunAsync(CommonTaskViewModel task, string template, string outputName = "report.pdf")
        {
            var output = Path.Combine(_folder, outputName);

            using (var writer = new ReportReaderWriter(new ProjectManager(Path.Combine(_folder, "work")))
            {
                ReportTemplateFile = template,
                ReportOutputPath = output
            })
            {
                await task.WriteReportCommand.ExecuteAsync(writer);
            }

            return Read(output);
        }

        /// <summary>Reads the fields of a report.</summary>
        /// <param name="path">The report.</param>
        /// <returns>The value of each field, or nothing when there is no such file.</returns>
        public static Dictionary<string, string?> Read(string path)
        {
            if (!File.Exists(path))
            {
                return new Dictionary<string, string?>();
            }

            ComponentInfo.SetLicense("FREE-LIMITED-KEY");
            using var document = PdfDocument.Load(path);

            return document.Form.Fields.ToDictionary(f => f.Name, f => f.Value as string);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_folder, recursive: true);
            }
            catch (IOException)
            {
                // A file of the writer may still be held for a moment; the system cleans the temp folder up.
            }
            catch (UnauthorizedAccessException)
            {
                // Same.
            }
        }
    }
}
