using iText.Forms;
using iText.Forms.Fields;
using iText.IO.Source;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using RFiDGear.Model;
using RFiDGear.ViewModel;
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

        PdfDocument pdfDoc;
        PdfAcroForm form;

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
                //PdfDocument pdfDoc = new PdfDocument(new PdfReader(reportTemplatePath));
                //PdfAcroForm form = PdfAcroForm.GetAcroForm(pdfDoc, true);

                // Being set as true, this parameter is responsible to generate an appearance Stream
                // while flattening for all form fields that don't have one. Generating appearances will
                // slow down form flattening, but otherwise Acrobat might render the pdf on its own rules.
                //form.SetGenerateAppearance(true);
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}; {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
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


                //PdfFont font = PdfFontFactory.CreateFont(FONT, PdfEncodings.IDENTITY_H);
                //form.GetField("test").SetValue(VALUE, font, 12f);
                //form.GetField("test2").SetValue(VALUE, font, 12f);

                //form.GetField("Strasse_1").SetValue("1232test");

                //pdfDoc.Close();

            }
            catch (XmlException e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}; {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
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
                        if (!_fieldValue.IsReadOnly())
                        {
                            temp.Add(_form.Key);
                        }
                    }

                }
                //pdfDoc.Close();

                return temp;

            }
            catch (XmlException e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}; {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
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
                    //pdfDoc = pdfDoc ?? new PdfDocument(new PdfReader(ReportTemplatePath), new PdfWriter(ReportOutputPath));
                    //form = form ?? PdfAcroForm.GetAcroForm(pdfDoc, true);
                    //form ??
                    //PdfFont font = PdfFontFactory.CreateFont(FONT, PdfEncodings.IDENTITY_H);
                    //form.GetField("test").SetValue(VALUE, font, 12f);
                    //form.GetField("test2").SetValue(VALUE, font, 12f);

                    form.GetField(_field).SetValue(_value);
                    form.GetField(_field).SetReadOnly(true);
                    
                    //pdfDoc.Close();

                }
                catch (XmlException e)
                {
                    LogWriter.CreateLogEntry(string.Format("{0}; {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
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
                LogWriter.CreateLogEntry(string.Format("{0}; {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
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
                        LogWriter.CreateLogEntry(string.Format("{0}; {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
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