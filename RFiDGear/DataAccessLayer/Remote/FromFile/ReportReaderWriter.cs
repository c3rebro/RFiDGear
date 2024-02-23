using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
using GemBox.Pdf;

namespace RFiDGear.DataAccessLayer
{
    /// <summary>
    /// 
    /// </summary>
    public class ReportReaderWriter : IDisposable
    {
        #region fields
        private readonly Version Version = Assembly.GetExecutingAssembly().GetName().Version;
        private readonly EventLog eventLog = new EventLog("Application", ".", Assembly.GetEntryAssembly().GetName().Name);
        private const string reportTemplateTempFileName = "temptemplate.pdf";
        private readonly string appDataPath;
        public string ReportOutputPath { get; set; }
        public string ReportTemplateFile { get; set; }

        #endregion fields

        public ReportReaderWriter()
        {
            try
            {
                // Set license key to use GemBox.Pdf in Free mode.
                ComponentInfo.SetLicense("FREE-LIMITED-KEY");

                appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

                appDataPath = System.IO.Path.Combine(appDataPath, "RFiDGear");

                if (!Directory.Exists(appDataPath))
                {
                    Directory.CreateDirectory(appDataPath);
                }

                if (File.Exists(System.IO.Path.Combine(appDataPath, reportTemplateTempFileName)))
                {
                    File.Delete(System.IO.Path.Combine(appDataPath, reportTemplateTempFileName));
                }
            }
            catch (Exception e)
            {
                eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
                return;
            }
        }

        public ObservableCollection<string> GetReportFields()
        {
            try
            {
                var temp = new ObservableCollection<string>();

                using (var pdfDoc = PdfDocument.Load(ReportTemplateFile))
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
                        eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
                    }
                }

                return temp;

            }
            catch (XmlException e)
            {
                eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
                return null;
            }
        }

        public async Task SetReportField(string _field, string _value)
        {
            if (!String.IsNullOrWhiteSpace(ReportOutputPath))
            {
                try
                {
                    await Task.Run(() =>
                    {
                        using (var pdfDoc = PdfDocument.Load(ReportTemplateFile))
                        {
                            try
                            {
                                ReportTemplateFile = System.IO.Path.Combine(appDataPath, reportTemplateTempFileName);

                                var form = pdfDoc.Form;
                                pdfDoc.Info.Title = "RFiDGear Report";
                                pdfDoc.Info.Author = "RFiDGear";

                                if (pdfDoc.Form.Fields.Any(x => x.Name == _field))
                                {
                                    pdfDoc.Form.Fields[_field].Hidden = false;
                                    pdfDoc.Form.Fields[_field].ReadOnly = false;
                                    pdfDoc.Form.Fields[_field].Value = _value;
                                }

                                pdfDoc.Save(ReportOutputPath);
                                pdfDoc.Close();

                                File.Copy(ReportOutputPath, System.IO.Path.Combine(appDataPath, reportTemplateTempFileName), true);
                            }
                            catch (Exception e)
                            {
                                eventLog.WriteEntry(string.Format(e.Message + "; SetReportField: " + _field), EventLogEntryType.Error);
                            }
                        }
                    }).ConfigureAwait(true);

                    return;
                }
                catch (XmlException e)
                {
                    eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
                }
            }
            return;
        }

        public async Task ConcatReportField(string _field, string _value)
        {
            if (!String.IsNullOrWhiteSpace(ReportOutputPath))
            {
                try
                {
                    await Task.Run(() => 
                    {
                        ReportTemplateFile = System.IO.Path.Combine(appDataPath, reportTemplateTempFileName);

                        using (var pdfDoc = PdfDocument.Load(ReportTemplateFile))
                        {
                            try
                            {
                                var form = pdfDoc.Form;

                                pdfDoc.Form.Fields[_field].Hidden = false;
                                pdfDoc.Form.Fields[_field].ReadOnly = false;
                                pdfDoc.Form.Fields[_field].Value = string.Format("{0}{1}", pdfDoc.Form.Fields[_field]?.Value, _value);

                                pdfDoc.Save(ReportOutputPath);
                                pdfDoc.Close();

                                File.Copy(ReportOutputPath, System.IO.Path.Combine(appDataPath, reportTemplateTempFileName), true);
                            }
                            catch (Exception e)
                            {
                                eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
                            }
                        }

                        return;
                    }).ConfigureAwait(false);

                }
                catch (XmlException e)
                {
                    eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
                }
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
                        eventLog.WriteEntry(e.Message, EventLogEntryType.Error);
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