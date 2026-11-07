using System;
using System.Collections.Generic;

namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>
    /// What the first report means for the rest of the check: the verdict, the memory it found, the templates that
    /// fit and the one that is preselected. Built only by <see cref="TemplateRules.Select"/>, so the parts always
    /// agree with each other.
    /// </summary>
    public sealed record TemplateSelection
    {
        internal TemplateSelection(ChipVerdict verdict, FreeMemory memory, IReadOnlyList<LsmTemplate> available, LsmTemplate? defaultTemplate)
        {
            Verdict = verdict;
            Memory = memory ?? throw new ArgumentNullException(nameof(memory));
            Available = available ?? throw new ArgumentNullException(nameof(available));
            Default = defaultTemplate;
        }

        /// <summary>What the report says about the chip.</summary>
        public ChipVerdict Verdict { get; }

        /// <summary>The free memory the report states.</summary>
        public FreeMemory Memory { get; }

        /// <summary>The templates that fit the chip, in display order; empty unless the chip is programmable.</summary>
        public IReadOnlyList<LsmTemplate> Available { get; }

        /// <summary>The preselected template, or <c>null</c> when none fits.</summary>
        public LsmTemplate? Default { get; }
    }
}
