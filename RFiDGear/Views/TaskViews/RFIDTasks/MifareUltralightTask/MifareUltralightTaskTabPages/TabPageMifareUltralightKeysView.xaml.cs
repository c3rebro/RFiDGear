using System.Windows.Controls;

namespace RFiDGear.View
{
    /// <summary>
    /// The keys of the authentication of a task of the Ultralight family: the key of an Ultralight C, and the password and the PACK
    /// of an Ultralight EV1 or an NTAG. Each key is typed into the task or named by a reference to a secure key provider.
    /// </summary>
    public partial class TabPageMifareUltralightKeysView : UserControl
    {
        /// <summary>Creates the tab.</summary>
        public TabPageMifareUltralightKeysView()
        {
            InitializeComponent();
        }
    }
}
