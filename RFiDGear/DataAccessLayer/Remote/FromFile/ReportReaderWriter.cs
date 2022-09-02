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
                ObservableCollection<string> temp = new ObservableCollection<string>();

                using (PdfDocument pdfDoc = new PdfDocument(new PdfReader(ReportTemplatePath)))
                {
                    PdfAcroForm form = PdfAcroForm.GetAcroForm(pdfDoc, true);
                    // Being set as true, this parameter is responsible to generate an appearance Stream
                    // while flattening for all form fields that don't have one. Generating appearances will
                    // slow down form flattening, but otherwise Acrobat might render the pdf on its own rules.
                    form.SetGenerateAppearance(true);
                    //form.SetNeedAppearances(true);

                    try
                    {
                        if (form != null)
                        {
                            foreach (KeyValuePair<string, PdfFormField> _form in form.GetFormFields())
                            {
                                temp.Add(_form.Key);
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
                    using (PdfDocument pdfDoc = new PdfDocument(new PdfReader(ReportTemplatePath), new PdfWriter(ReportOutputPath)))
                    {
                        ReportTemplatePath = System.IO.Path.Combine(appDataPath, reportTemplateTempFileName);

                        PdfAcroForm form = PdfAcroForm.GetAcroForm(pdfDoc, true);
                        // Being set as true, this parameter is responsible to generate an appearance Stream
                        // while flattening for all form fields that don't have one. Generating appearances will
                        // slow down form flattening, but otherwise Acrobat might render the pdf on its own rules.
                        form.SetGenerateAppearance(true);
                        //form.SetNeedAppearances(true);

                        try
                        {
                            if (form.GetField(_field) is PdfButtonFormField)
                            {
                                _ = (form.GetField(_field) as PdfButtonFormField)?.SetBorderWidth(1);
                                _ = (form.GetField(_field) as PdfButtonFormField)?.SetBorderColor(ColorConstants.BLACK);
                            }

                            _ = form.GetField(_field)?.SetVisibility(PdfFormField.VISIBLE);
                            _ = form.GetField(_field)?.SetReadOnly(false);
                            _ = form.GetField(_field)?.SetValue(_value);
                        }
                        catch (Exception e)
                        {
                            LogWriter.CreateLogEntry(e, FacilityName);
                        }

                        pdfDoc.Close();
                    }

                    File.Copy(ReportOutputPath, System.IO.Path.Combine(appDataPath, reportTemplateTempFileName), true);
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

                    using (PdfDocument pdfDoc = new PdfDocument(new PdfReader(ReportTemplatePath), new PdfWriter(ReportOutputPath)))
                    {
                        PdfAcroForm form = PdfAcroForm.GetAcroForm(pdfDoc, true);
                        // Being set as true, this parameter is responsible to generate an appearance Stream
                        // while flattening for all form fields that don't have one. Generating appearances will
                        // slow down form flattening, but otherwise Acrobat might render the pdf on its own rules.
                        form.SetGenerateAppearance(true);
                        //form.SetNeedAppearances(true);

                        try
                        {
                            form.GetField(_field)?.SetVisibility(PdfFormField.VISIBLE);
                            form.GetField(_field)?.SetValue(string.Format("{0}{1}", form.GetField(_field)?.GetValueAsString(), _value));
                        }
                        catch (Exception e)
                        {
                            LogWriter.CreateLogEntry(e, FacilityName);
                        }

                        pdfDoc.Close();
                    }

                    File.Copy(ReportOutputPath, System.IO.Path.Combine(appDataPath, reportTemplateTempFileName), true);

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