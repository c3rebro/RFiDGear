using System;

namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>Which kind of chip a report template is written for.</summary>
    public enum TemplateFamily
    {
        /// <summary>A MIFARE Classic template (<c>MC...</c>); its need is a number of free sectors.</summary>
        Classic,

        /// <summary>A MIFARE DESFire template (<c>MD...</c>); its need is a number of free bytes.</summary>
        Desfire,
    }

    /// <summary>
    /// One report template the lab can use for a chip, with what it needs from the chip.
    /// </summary>
    public sealed record LsmTemplate
    {
        /// <summary>Creates a template description.</summary>
        /// <param name="name">The template's name, for example <c>MD4000L_AV</c>; must not be empty.</param>
        /// <param name="displayText">The text shown to the operator, including the memory need.</param>
        /// <param name="family">The kind of chip it is for.</param>
        /// <param name="sizeInBytes">Free bytes a DESFire chip needs; zero for a Classic template.</param>
        /// <param name="sizeInFreeSectors">Free sectors a Classic chip needs; zero for a DESFire template.</param>
        /// <exception cref="ArgumentException"><paramref name="name"/> is null, empty or whitespace.</exception>
        public LsmTemplate(string name, string displayText, TemplateFamily family, int sizeInBytes, int sizeInFreeSectors)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A template needs a name.", nameof(name));
            }

            Name = name;
            DisplayText = displayText ?? name;
            Family = family;
            SizeInBytes = sizeInBytes;
            SizeInFreeSectors = sizeInFreeSectors;
        }

        /// <summary>The template's name, for example <c>MD4000L_AV</c>.</summary>
        public string Name { get; }

        /// <summary>The text shown to the operator, including the memory need.</summary>
        public string DisplayText { get; }

        /// <summary>The kind of chip it is for.</summary>
        public TemplateFamily Family { get; }

        /// <summary>Free bytes a DESFire chip needs; zero for a Classic template.</summary>
        public int SizeInBytes { get; }

        /// <summary>Free sectors a Classic chip needs; zero for a DESFire template.</summary>
        public int SizeInFreeSectors { get; }
    }
}
