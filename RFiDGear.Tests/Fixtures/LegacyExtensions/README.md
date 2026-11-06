# Legacy extension fixtures

Two prebuilt extension DLLs that were compiled against **RFiDGear.exe as it was before the extension SDK was split
off**, when `UiExtensionAttribute`, `HostPlacement`, `IUIExtension` and `IUIExtensionDetails` lived inside the
executable. They prove that extensions built that way keep working, without being rebuilt, now that those types live
in `RFiDGear.ExtensionSdk` and the executable only forwards them.

**Never regenerate these files from current sources.** A rebuild would bind to the SDK and test nothing. They are
pinned by SHA-256 in `LegacyExtensionCompatibilityTests`.

## Provenance

| File | Built from | SHA-256 |
|---|---|---|
| `RFiDGear.Extensions.LegacyDesfireSample.dll` | the sources of `RFiDGear.Extensions/DesfirePluginSample` at commit `a076231`, **assembly name changed** (see below) | `d1428043a548e425a58df32f313d1dd1ee15fda5d6444b34eaf1133496977847` |
| `RFiDGear.Extensions.LegacyAppFixture.dll` | the source below, referencing `RFiDGear.csproj` at commit `a076231` | `83295450d586bb7cc02deafbab81a6a128be7daa7e68bd1e6cbf6eb27c8167b3` |

Both were built with .NET SDK 9.0.311, target `net8.0-windows`, `Release`, in a throwaway `git worktree` at `a076231`
(the sample's sources were copied into a scratch folder whose project file only changes `AssemblyName` and points the
`ProjectReference` at that worktree's `RFiDGear.csproj`):

```
dotnet build RFiDGear.Extensions.LegacyDesfireSample.csproj -c Release
dotnet build RFiDGear.Extensions.LegacyAppFixture.csproj    -c Release     # DebugType none
```

Release with `DebugType none` keeps local paths out of the binaries.

## Why the sample was renamed

The test project also references the real `RFiDGear.Extensions.DesfirePluginSample`. WPF resolves
`pack://application:,,,/Name;component/...` by **simple name across every assembly in the process, including ones in
other load contexts**. A second assembly with the same name, loaded first, makes WPF find the wrong copy and breaks the
resource-dictionary tests. A distinct name avoids that and keeps the fixture loadable with an ordinary
`Assembly.LoadFrom`, which is how MEF's `AssemblyCatalog(path)` loads a real extension. The *content* is still the
sample compiled against the old executable; only the name differs, so the attribute's `Uri` string still names the
original sample.

## Why two fixtures

* `LegacyDesfireSample` is the real card-task extension. It sets name, category, sort order and URI, and leaves
  `HostPlacement` unset, so it must resolve to `CardTask`.
* `LegacyAppFixture` is an application-placement extension. Its attribute carries a `HostPlacement` **enum named
  argument**, which the attribute blob stores by assembly-qualified type name (`...HostPlacement, RFiDGear`). Only this
  fixture proves that such a name resolves through the type forwarder.

## Source of `RFiDGear.Extensions.LegacyAppFixture` (kept here, not as a `.cs`, so it is not compiled into the tests)

```csharp
using RFiDGear.UI.UIExtensions;
using RFiDGear.UI.UIExtensions.Interfaces;

namespace RFiDGear.Extensions.LegacyAppFixture
{
    [UiExtension(
        Name = "Legacy App",
        SortOrder = 50,
        Uri = "pack://application:,,,/RFiDGear.Extensions.LegacyAppFixture;component/Missing.xaml",
        HostPlacement = HostPlacement.Application)]
    public class LegacyAppFixtureExtension : IUIExtension
    {
    }
}
```

## What cannot be unit-tested

Rendering, because the fixture's own pack URIs name the original sample. Check it by hand with a real older build:
copy a pre-split `RFiDGear.Extensions.DesfirePluginSample.dll` into `%ProgramData%\RFiDGear\Extensions`, start RFiDGear,
open a DESFire task and look at the Plugins tab.
