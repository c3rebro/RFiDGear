using GemBox.Pdf;

using Log4CSharp;

using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;

namespace RFiDGear.DataAccessLayer
{
    /// <summary>
    /// 
    /// </summary>
    public class ReportReaderWriter : IDisposable
    {
        #region fields
        private static readonly string FacilityName = "RFiDGear";

        private readonly Version Version = Assembly.GetExecutingAssembly().GetName().Version;

        private const string reportTemplateTempFileName = "temptemplate.pdf";
        private readonly string appDataPath;
        public string ReportOutputPath { get; set; }
        public string ReportTemplatePath { get; set; }

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
                LogWriter.CreateLogEntry(string.Format("{0}; {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
                return;
            }
        }

        public ObservableCollection<string> GetReportFields()
        {
            try
            {
                var temp = new ObservableCollection<string>();

                using (var pdfDoc = PdfDocument.Load(ReportTemplatePath))
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
                        LogWriter.CreateLogEntry(e, FacilityName);
                    }
                }

                return temp;

            }
            catch (XmlException e)
            {
                LogWriter.CreateLogEntry(e, FacilityName);
                return null;
            }
        }

        public void SetReportField(string _field, string _value)
        {
            if (!String.IsNullOrWhiteSpace(ReportOutputPath))
            {
                try
                {
                    using (var pdfDoc = PdfDocument.Load(ReportTemplatePath)) // (new PdfReader(ReportTemplatePath), new PdfWriter(ReportOutputPath)))
                    {
                        try
                        {
                            ReportTemplatePath = System.IO.Path.Combine(appDataPath, reportTemplateTempFileName);

                            var form = pdfDoc.Form;

                            pdfDoc.Form.Fields[_field].Hidden = false;
                            pdfDoc.Form.Fields[_field].ReadOnly = false;
                            pdfDoc.Form.Fields[_field].Value = _value;

                            pdfDoc.Save(ReportOutputPath);
                            pdfDoc.Close();

                            File.Copy(ReportOutputPath, System.IO.Path.Combine(appDataPath, reportTemplateTempFileName), true);
                        }
                        catch (Exception e)
                        {
                            LogWriter.CreateLogEntry(e, FacilityName);
                        }
                    }
                }
                catch (XmlException e)
                {
                    LogWriter.CreateLogEntry(e, FacilityName);
                }
            }

        }

        public void ConcatReportField(string _field, string _value)
        {
            if (!String.IsNullOrWhiteSpace(ReportOutputPath))
            {
                try
                {
                    ReportTemplatePath = System.IO.Path.Combine(appDataPath, reportTemplateTempFileName);

                    using (var pdfDoc = PdfDocument.Load(ReportTemplatePath))
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
                            LogWriter.CreateLogEntry(e, FacilityName);
                        }
                    }
                }
                catch (XmlException e)
                {
                    LogWriter.CreateLogEntry(e, FacilityName);
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
                        LogWriter.CreateLogEntry(e, FacilityName);
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