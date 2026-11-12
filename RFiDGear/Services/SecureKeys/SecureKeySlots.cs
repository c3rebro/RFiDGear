#nullable enable
using System.Collections.Generic;

namespace RFiDGear.Services.SecureKeys
{
    /// <summary>
    /// The identifiers of the key slots of the tasks that have keys. They are stored in project files, so they never
    /// change: renaming one would unbind every project that uses it.
    /// </summary>
    internal static class SecureKeySlots
    {
        /// <summary>DESFire: the key that authenticates to the card itself (PICC master key).</summary>
        internal const string DesfirePiccCurrent = "desfire.picc.current";

        /// <summary>DESFire: the key a change of the PICC master key writes.</summary>
        internal const string DesfirePiccNew = "desfire.picc.new";

        /// <summary>DESFire: the key that authenticates to an application.</summary>
        internal const string DesfireAppCurrent = "desfire.app.current";

        /// <summary>DESFire: the key of an application that a key change replaces, when it is not the one that authenticates.</summary>
        internal const string DesfireAppOld = "desfire.app.old";

        /// <summary>DESFire: the key a change of an application key writes.</summary>
        internal const string DesfireAppNew = "desfire.app.new";

        /// <summary>DESFire: the key that reads a file.</summary>
        internal const string DesfireRead = "desfire.read";

        /// <summary>DESFire: the key that writes a file.</summary>
        internal const string DesfireWrite = "desfire.write";

        /// <summary>MIFARE Classic: key A that authenticates.</summary>
        internal const string ClassicACurrent = "classic.a.current";

        /// <summary>MIFARE Classic: key B that authenticates.</summary>
        internal const string ClassicBCurrent = "classic.b.current";

        /// <summary>MIFARE Classic: key A that a write puts into the sector trailer.</summary>
        internal const string ClassicATarget = "classic.a.target";

        /// <summary>MIFARE Classic: key B that a write puts into the sector trailer.</summary>
        internal const string ClassicBTarget = "classic.b.target";

        /// <summary>MIFARE Classic: key A of the MAD sector that authenticates.</summary>
        internal const string ClassicMadACurrent = "classic.mad.a.current";

        /// <summary>MIFARE Classic: key B of the MAD sector that authenticates.</summary>
        internal const string ClassicMadBCurrent = "classic.mad.b.current";

        /// <summary>MIFARE Classic: key A that a write puts into the MAD sector trailer.</summary>
        internal const string ClassicMadATarget = "classic.mad.a.target";

        /// <summary>MIFARE Classic: key B that a write puts into the MAD sector trailer.</summary>
        internal const string ClassicMadBTarget = "classic.mad.b.target";

        /// <summary>Gets every slot of a DESFire task.</summary>
        internal static IReadOnlyList<string> Desfire { get; } = new[]
        {
            DesfirePiccCurrent, DesfirePiccNew, DesfireAppCurrent, DesfireAppOld, DesfireAppNew, DesfireRead, DesfireWrite,
        };

        /// <summary>Gets every slot of a MIFARE Classic task.</summary>
        internal static IReadOnlyList<string> Classic { get; } = new[]
        {
            ClassicACurrent, ClassicBCurrent, ClassicATarget, ClassicBTarget,
            ClassicMadACurrent, ClassicMadBCurrent, ClassicMadATarget, ClassicMadBTarget,
        };
    }
}
