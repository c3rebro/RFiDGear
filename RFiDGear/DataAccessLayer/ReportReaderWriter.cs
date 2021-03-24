using iText.Forms;
using iText.Forms.Fields;
using iText.IO.Source;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using RFiDGear.Model;
using RFiDGear.ViewModel;
using System;
using System.Collections.ObjectModel;
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
    public class ReportReaderWriter
    {
        #region fields

        private readonly Version Version = Assembly.GetExecutingAssembly().GetName().Version;

        private const string reportTemplateFileName = "reporttemplate.pdf";
        private const string taskDatabaseFileName = "taskdatabase.xml";
        private string appDataPath;
        private string reportTemplatePath;

        public ObservableCollection<RFiDChipParentLayerViewModel> treeViewModel;
        public ChipTaskHandlerModel setupModel;

        #endregion fields

        public ReportReaderWriter()
        {
            try
            {
                // Combine the base folder with the specific folder....
                appDataPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RFiDGear");

                // Check if folder exists and if not, create it
                if (!Directory.Exists(appDataPath))
                    Directory.CreateDirectory(appDataPath);

                if (File.Exists(System.IO.Path.Combine(appDataPath, reportTemplateFileName)))
                {
                    reportTemplatePath = System.IO.Path.Combine(appDataPath, reportTemplateFileName);
                }

                else
                {
                    throw new Exception("report template not found");
                }
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
        /// <param name="device"></param>
        /// <param name="_path"></param>
        public void CreateReport(RFiDDevice device, string _path = "")
        {
            try
            {
                PdfDocument pdfDoc = new PdfDocument(new PdfReader(reportTemplatePath), new PdfWriter(_path));
                PdfAcroForm form = PdfAcroForm.GetAcroForm(pdfDoc, true);

                // Being set as true, this parameter is responsible to generate an appearance Stream
                // while flattening for all form fields that don't have one. Generating appearances will
                // slow down form flattening, but otherwise Acrobat might render the pdf on its own rules.
                form.SetGenerateAppearance(true);

                //PdfFont font = PdfFontFactory.CreateFont(FONT, PdfEncodings.IDENTITY_H);
                //form.GetField("test").SetValue(VALUE, font, 12f);
                //form.GetField("test2").SetValue(VALUE, font, 12f);

                form.GetField("Strasse_1").SetValue("1232test");

                pdfDoc.Close();

            }
            catch (XmlException e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}; {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));
                Environment.Exit(0);
            }
        }

        public void DeleteDatabase()
        {
            File.Delete(System.IO.Path.Combine(appDataPath, reportTemplateFileName));
        }
    }
}