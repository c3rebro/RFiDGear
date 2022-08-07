using iText.Forms;
using iText.Forms.Fields;
using iText.IO.Source;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Colors;

using RFiDGear.Model;
using RFiDGear.ViewModel;

using Log4CSharp;

using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace RFiDGear.DataAccessLayer
{
    /// <summary>
    /// 
    /// </summary>
    public class ReportReaderWriter : IDisposable
    {
        #region fields
        private static readonly string FacilityName = "RFiDGear";

        PdfDocument pdfDoc;
        PdfAcroForm form = null;

        private readonly Version Version = Assembly.GetExecutingAssembly().GetName().Version;

        private const string reportTemplateTempFileName = "temptemplate.pdf";
        private const string taskDatabaseFileName = "taskdatabase.xml";
        private readonly string appDataPath;
        public string ReportOutputPath { get; set; }
        public string ReportTemplatePath { get; set; }

        #endregion fields

        public ReportReaderWriter()
        {
            try
            {
                appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

                appDataPath = System.IO.Path.Combine(appDataPath, "RFiDGear");

                if (!Directory.Exists(appDataPath))
                    Directory.CreateDirectory(appDataPath);

                if (File.Exists(System.IO.Path.Combine(appDataPath, reportTemplateTempFileName)))
                    File.Delete(System.IO.Path.Combine(appDataPath, reportTemplateTempFileName));
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}; {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
                return;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="_path"></param>
        public void OpenReport(string _path = "")
        {
            try
            {
                if(pdfDoc == null)
                {
                    if(!string.IsNullOrEmpty(ReportOutputPath))
                    {
                        pdfDoc = new PdfDocument(new PdfReader(ReportTemplatePath ?? _path), new PdfWriter(ReportOutputPath));
                    }
                    
                    else
                        pdfDoc = new PdfDocument(new PdfReader(ReportTemplatePath ?? _path));

                    form = PdfAcroForm.GetAcroForm(pdfDoc, true);

                    // Being set as true, this parameter is responsible to generate an appearance Stream
                    // while flattening for all form fields that don't have one. Generating appearances will
                    // slow down form flattening, but otherwise Acrobat might render the pdf on its own rules.
                    form.SetGenerateAppearance(true);
                }
            }
            catch (XmlException e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}; {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
                Environment.Exit(0);
            }
        }

        public ObservableCollection<string> GetReportFields()
        {
            try
            {
                ObservableCollection<string> temp = new ObservableCollection<string>(); // = new ObservableCollection<string>(form.GetFormFields().Keys);

                if (form != null)
                {
                    foreach (KeyValuePair<string, PdfFormField> _form in form.GetFormFields())
                    {
                        PdfFormField _fieldValue = _form.Value;
                        temp.Add(_form.Key);
                    }

                }

                return temp;

            }
            catch (XmlException e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}; {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
                return null;
            }
        }

        public void SetReportField(string _field, string _value)
        {
            if(!String.IsNullOrWhiteSpace(ReportOutputPath))
            {
                try
                {
                    ReportTemplatePath = System.IO.Path.Combine(appDataPath, reportTemplateTempFileName);

                    OpenReport();

                    try
                    {
                        //form.GetField(_field).SetReadOnly(false);
                        form.GetField(_field).SetBorderWidth(1);

                        form.GetField(_field).SetVisibility(PdfFormField.VISIBLE);
                        
                        form.GetField(_field).SetValue(_value);
                        //form.GetField(_field).SetReadOnly(true);

                        if(form.GetField(_field) is PdfButtonFormField)
                        {
                            (form.GetField(_field) as PdfButtonFormField).SetBorderColor(ColorConstants.BLACK);
                            (form.GetField(_field) as PdfButtonFormField).SetBorderWidth(1);
                        }
                    }
                    catch (Exception e)
                    {
                        LogWriter.CreateLogEntry(string.Format("{0}; {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
                    }
                }
                catch (XmlException e)
                {
                    LogWriter.CreateLogEntry(string.Format("{0}; {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
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

                    OpenReport();

                    try
                    {
                        //form.GetField(_field).SetReadOnly(false);
                        form.GetField(_field).SetBorderWidth(1);

                        form.GetField(_field).SetVisibility(PdfFormField.VISIBLE);

                        form.GetField(_field).SetValue(string.Format("{0}{1}", form.GetField(_field).GetValueAsString(), _value) ) ;
                        //form.GetField(_field).SetReadOnly(true);

                        if (form.GetField(_field) is PdfButtonFormField)
                        {
                            (form.GetField(_field) as PdfButtonFormField).SetBorderColor(ColorConstants.BLACK);
                            (form.GetField(_field) as PdfButtonFormField).SetBorderWidth(1);
                        }
                    }
                    catch (Exception e)
                    {
                        LogWriter.CreateLogEntry(string.Format("{0}; {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
                    }
                }
                catch (XmlException e)
                {
                    LogWriter.CreateLogEntry(string.Format("{0}; {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
                }
            }

        }

        public void CloseReport()
        {
            try
            {
                pdfDoc.Close();

                File.Copy(ReportOutputPath, System.IO.Path.Combine(appDataPath, reportTemplateTempFileName), true);

                form = null;
                pdfDoc = null;
            }

            catch (XmlException e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}; {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
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
                        if (form != null)
                        {
                            pdfDoc.Close();
                            form = null;
                            pdfDoc = null;
                        }


                        // Dispose any managed objects
                        // ...
                    }

                    catch (XmlException e)
                    {
                        LogWriter.CreateLogEntry(string.Format("{0}; {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), FacilityName);
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