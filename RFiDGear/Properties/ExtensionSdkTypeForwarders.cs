using System.Runtime.CompilerServices;

using RFiDGear.UI.UIExtensions;
using RFiDGear.UI.UIExtensions.Interfaces;

// These four types used to live in RFiDGear.exe and now live in RFiDGear.ExtensionSdk. Extensions that were compiled
// against the executable still refer to them as "[RFiDGear]RFiDGear.UI.UIExtensions.*"; the runtime follows these
// forwarders to the SDK, so such extensions keep working without being rebuilt. Do not remove them, and do not
// declare any of these types in this assembly again (that would be a duplicate definition).
[assembly: TypeForwardedTo(typeof(UiExtensionAttribute))]
[assembly: TypeForwardedTo(typeof(HostPlacement))]
[assembly: TypeForwardedTo(typeof(IUIExtension))]
[assembly: TypeForwardedTo(typeof(IUIExtensionDetails))]
