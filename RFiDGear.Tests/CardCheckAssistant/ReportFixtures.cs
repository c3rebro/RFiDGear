using GemBox.Pdf;
using GemBox.Pdf.Forms;

namespace RFiDGear.Tests.CardCheckAssistant
{
    /// <summary>The reports a check run writes, as small PDFs the tests build themselves.</summary>
    internal static class ReportFixtures
    {
        /// <summary>
        /// Writes a first report with the form fields the check project fills in: a check box for each of the two
        /// flags, a text for the free memory, the chip type and the notes.
        /// </summary>
        /// <param name="path">Where to write it; an existing file is replaced.</param>
        /// <param name="supported">The value of the "supported" check box.</param>
        /// <param name="programmable">The value of the "can be programmed" check box.</param>
        /// <param name="freeMemory">The free-memory text.</param>
        /// <param name="chipType">The chip type text.</param>
        /// <param name="notes">The notes text.</param>
        public static void WriteFirstReport(
            string path,
            string supported = "Yes",
            string programmable = "Yes",
            string freeMemory = "1760",
            string chipType = "MIFARE DESFire EV2",
            string notes = "notes")
        {
            ComponentInfo.SetLicense("FREE-LIMITED-KEY");
            using var document = new PdfDocument();
            var page = document.Pages.Add();

            AddCheckBox(document, page, "CheckBox_isChipSuppYes", supported, 700);
            AddCheckBox(document, page, "CheckBox_ChipCanUseYes", programmable, 670);
            AddText(document, page, "TextBox_Detail_FreeMem_1", freeMemory, 640);
            AddText(document, page, "TextBox_ChipType", chipType, 610);
            AddText(document, page, "TextBox_Notes_Site_2", notes, 580);

            document.Save(path);
        }

        private static void AddCheckBox(PdfDocument document, PdfPage page, string name, string value, double top)
        {
            var field = document.Form.Fields.AddCheckBox(page, 50, top, 14, 14);
            field.Name = name;
            field.Value = value;
        }

        private static void AddText(PdfDocument document, PdfPage page, string name, string value, double top)
        {
            var field = document.Form.Fields.AddText(page, 50, top, 200, 20);
            field.Name = name;
            field.Value = value;
        }
    }
}
