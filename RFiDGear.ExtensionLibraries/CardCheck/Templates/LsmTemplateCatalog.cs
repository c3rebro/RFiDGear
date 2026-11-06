using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>
    /// The report templates of the lab's card check, in the order the operator sees them. The entries were taken
    /// from the old assistant's table by a script, not retyped; the display texts are the German texts of that table.
    /// The old table also held a placeholder row "N/A", which is not a template and is left out.
    /// <para>
    /// One deliberate difference: MD10000L_AV needs 3232 bytes, as its own text says. The old table carried 1760
    /// there (a copy of the row above), which offered this template to chips with too little memory.
    /// </para>
    /// </summary>
    internal static class LsmTemplateCatalog
    {
        private static readonly LsmTemplate[] Entries =
        {
            new LsmTemplate("MCBasic", "MCBasic (Speicherbedarf: 1 Sektor)", TemplateFamily.Classic, 0, 1),
            new LsmTemplate("MC1200L", "MC1200L (Speicherbedarf: 4 Sektoren)", TemplateFamily.Classic, 0, 4),
            new LsmTemplate("MC3800L", "MC3800L (Speicherbedarf: 11 Sektoren)", TemplateFamily.Classic, 0, 11),
            new LsmTemplate("MC1000L_AV", "MC1000L_AV (Speicherbedarf: 11 Sektoren)", TemplateFamily.Classic, 0, 11),
            new LsmTemplate("MC2400L_AV", "MC2400L_AV (Speicherbedarf: 19 Sektoren)", TemplateFamily.Classic, 0, 19),
            new LsmTemplate("MC8000L_AV", "MC8000L_AV (Speicherbedarf: 43 Sektoren / 32 + 3)", TemplateFamily.Classic, 0, 43),
            new LsmTemplate("MDBasic", "MDBasic (Speicherbedarf: 48 + 176 = 224 Bytes)", TemplateFamily.Desfire, 224, 0),
            new LsmTemplate("MD1200L", "MD1200L (Speicherbedarf: 192 + 160 = 352 Bytes)", TemplateFamily.Desfire, 352, 0),
            new LsmTemplate("MD3800L", "MD3800L (Speicherbedarf: 528 + 176 = 704 Bytes)", TemplateFamily.Desfire, 704, 0),
            new LsmTemplate("MD2500L_AV", "MD2500L_AV (Speicherbedarf: 1024 + 160 = 1184 Bytes)", TemplateFamily.Desfire, 1184, 0),
            new LsmTemplate("MD4000L_AV", "MD4000L_AV (Speicherbedarf: 1600 + 160 = 1760 Bytes)", TemplateFamily.Desfire, 1760, 0),
            new LsmTemplate("MD10000L_AV", "MD10000L_AV (Speicherbedarf: 3048 + 184 = 3232 Bytes)", TemplateFamily.Desfire, 3232, 0),  // legacy table says 1760, the text says 3232
            new LsmTemplate("MD32000L_AV", "MD32000L_AV (Speicherbedarf: 7000 + 168 = 7168 Bytes)", TemplateFamily.Desfire, 7168, 0),
        };

        /// <summary>Every template, in display order.</summary>
        internal static readonly IReadOnlyList<LsmTemplate> All = new ReadOnlyCollection<LsmTemplate>(Entries);
    }
}
