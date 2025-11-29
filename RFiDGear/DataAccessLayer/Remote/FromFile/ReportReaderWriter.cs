using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
using GemBox.Pdf;
using Serilog;

namespace RFiDGear.DataAccessLayer
{
    /// <summary>
    /// 
    /// </summary>
    public class ReportReaderWriter : IDisposable
    {
        #region fields
        private readonly Version Version = Assembly.GetExecutingAssembly().GetName().Version;
        private readonly Serilog.ILogger logger = Log.ForContext<ReportReaderWriter>();
        private readonly ProjectManager projectManager;
        public string ReportOutputPath { get; set; }
        public string ReportTemplateFile { get; set; }

        #endregion fields

        public ReportReaderWriter()
            : this(new ProjectManager())
        {
        }

        public ReportReaderWriter(ProjectManager projectManager)
        {
            try
            {
                this.projectManager = projectManager ?? throw new ArgumentNullException(nameof(projectManager));

                // Set license key to use GemBox.Pdf in Free mode.
                ComponentInfo.SetLicense("FREE-LIMITED-KEY");

                CleanupTemporaryTemplate();
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to initialize report writer");
                return;
            }
        }

        public ObservableCollection<string> GetReportFields()
        {
            try
            {
                var temp = new ObservableCollection<string>();

                using (var pdfDoc = PdfDocument.Load(projectManager.GetReportTemplatePath(ReportTemplateFile)))
                {
                    var form = pdfDoc.Form;

                    try
                    {
                        if (form != null)
                        {
                            foreach (var _form in form.Fields)
                            {
                                temp.Add(_form.Name);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Error(e, "Failed to enumerate fields from {TemplateFile}", ReportTemplateFile);
                    }
                }

                return temp;

            }
            catch (XmlException e)
            {
                logger.Error(e, "Failed to read report fields from {TemplateFile}", ReportTemplateFile);
                return null;
            }
        }

        public async Task SetReportField(string _field, string _value)
        {
            await ApplyReportChanges(_field, pdfDoc =>
            {
                var form = pdfDoc.Form;
                pdfDoc.Info.Title = "RFiDGear Report";
                pdfDoc.Info.Author = "RFiDGear";

                if (form?.Fields.Any(x => x.Name == _field) == true)
                {
                    pdfDoc.Form.Fields[_field].Hidden = false;
                    pdfDoc.Form.Fields[_field].ReadOnly = false;
                    pdfDoc.Form.Fields[_field].Value = _value;
                }
            });
        }

        public async Task ConcatReportField(string _field, string _value)
        {
            await ApplyReportChanges(_field, pdfDoc =>
            {
                var form = pdfDoc.Form;

                if (form?.Fields.Any(x => x.Name == _field) != true)
                {
                    return;
                }

                pdfDoc.Form.Fields[_field].Hidden = false;
                pdfDoc.Form.Fields[_field].ReadOnly = false;
                pdfDoc.Form.Fields[_field].Value = string.Format("{0}{1}", pdfDoc.Form.Fields[_field]?.Value, _value);
            });
        }

        private async Task ApplyReportChanges(string fieldName, Action<PdfDocument> applyChanges)
        {
            if (!HasValidReportPaths())
            {
                return;
            }

            try
            {
                await Task.Run(() =>
                {
                    EnsureTemporaryTemplate();

                    using (var pdfDoc = PdfDocument.Load(projectManager.GetTemporaryReportTemplatePath()))
                    {
                        try
                        {
                            applyChanges(pdfDoc);
                            SaveReport(pdfDoc);
                        }
                        catch (Exception e)
                        {
                            logger.Error(e, "Failed to apply changes to field {FieldName} in {OutputPath}", fieldName, ReportOutputPath);
                        }
                    }
                }).ConfigureAwait(false);
            }
            catch (XmlException e)
            {
                logger.Error(e, "Failed to apply report changes for field {FieldName}", fieldName);
            }
        }

        private void EnsureTemporaryTemplate()
        {
            var tempPath = projectManager.GetTemporaryReportTemplatePath();

            if (File.Exists(tempPath))
            {
                return;
            }

            projectManager.CopyReportTemplateToTemp(ReportTemplateFile);
        }

        private bool HasValidReportPaths()
        {
            return !string.IsNullOrWhiteSpace(ReportOutputPath)
                   && !string.IsNullOrWhiteSpace(ReportTemplateFile);
        }

        private void SaveReport(PdfDocument pdfDoc)
        {
            pdfDoc.Save(ReportOutputPath);
            pdfDoc.Close();

            projectManager.SafeCopy(ReportOutputPath, projectManager.GetTemporaryReportTemplatePath());
        }

        private void CleanupTemporaryTemplate()
        {
            var tempPath = projectManager.GetTemporaryReportTemplatePath();

            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }

        public void DeleteDatabase()
        {
            File.Delete(System.IO.Path.Combine(ReportOutputPath));
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    try
                    {
                        // Dispose any managed objects
                        // ...
                    }

                    catch (XmlException e)
                    {
                        logger.Error(e, "Failed while disposing report writer");
                    }
                }

                // Now disposed of any unmanaged objects
                // ...

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        private bool _disposed;
    }
}