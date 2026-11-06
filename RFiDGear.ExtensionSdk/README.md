---
title: RFiDGear.ExtensionSdk
---

RFiDGear.ExtensionSdk is the MEF-facing extension ABI: the small set of
types an extension uses to be discovered and placed by RFiDGear. Host services
(project, execution, reader, culture) are a separate assembly,
`RFiDGear.Contracts`. An extension references this SDK and
`RFiDGear.Contracts`, and never `RFiDGear.exe`.

**Types:**

- `UiExtensionAttribute` (`RFiDGear.UI.UIExtensions`): `[UiExtension(Name =
  ..., HostPlacement = ...)]` exports an extension and its metadata.
- `HostPlacement` (`RFiDGear.UI.UIExtensions`): where the host shows it,
  `CardTask = 0` (the default) or `Application = 1`.
- `IUIExtension` (`RFiDGear.UI.UIExtensions.Interfaces`): the export contract,
  a marker interface.
- `IUIExtensionDetails` (`RFiDGear.UI.UIExtensions.Interfaces`): the metadata
  view the host imports as `Lazy<IUIExtension, IUIExtensionDetails>`.
- `IApplicationUIExtension` (`RFiDGear.UI.UIExtensions.Interfaces`): the page
  factory of an application extension. `CreateContent(IRFiDGearExtensionHost
  host)` runs on the UI thread each time the user opens the extension and
  returns a new control for its window. The host disposes that content, and a
  different `IDisposable` data context, once when the window closes.

**Rules:**

- Versioned on its own. The assembly version is `1.0.0.0` and changes only for
  a breaking change, never with the application version, so an extension
  compiled once keeps binding to it.
- The surface is frozen by a snapshot,
  `RFiDGear.Tests/ApiSurface/RFiDGear.ExtensionSdk.api.approved`. Any change
  fails `ExtensionSdkApiSurfaceTests` and shows up as a one-file diff. To
  accept an intended change set `RFIDGEAR_UPDATE_API_SNAPSHOT=1`, run that
  test once, review the diff and commit it.
- The types used to live in `RFiDGear.exe`, which still forwards them
  (`RFiDGear/Properties/ExtensionSdkTypeForwarders.cs`), so extensions built
  against the old location keep working without being rebuilt. Do not remove a
  forwarder and do not declare these types in the executable again.
  `RFiDGear.Tests/Fixtures/LegacyExtensions` holds prebuilt binaries that
  prove it.
- Nullable annotations are off in this project on purpose: the moved types
  are nullable-oblivious and their public shape must not change.
- `HostPlacement` values are persisted in compiled extensions as numbers.
  Never renumber them.

**For extension authors:**

- Extensions are discovered once, at start-up, from
  `%ProgramData%\RFiDGear\Extensions\RFiDGear.Extensions*.dll`. There is no
  hot loading: adding or removing an extension needs an application restart.
- `RFiDGear.Contracts.dll`, `RFiDGear.ExtensionSdk.dll` and the MEF runtime
  are owned by the host. An extension must not carry private copies of them.
- Do not `[Import]` `IRFiDGearExtensionHost`. The service does not exist yet
  when MEF composes the extensions, so it is not exported, and MEF leaves out
  an extension with an import it cannot satisfy without any error. Take the
  service from the argument of `CreateContent`.
- The exported class is created on the first click, not at start-up, and is
  shared afterwards: keep it stateless and put per-window state in the
  content. A constructor or `CreateContent` that throws is logged and shown to
  the user, and the next click tries again.
- An extension that only needs a page can keep naming it with `Uri` and
  implement just `IUIExtension`; it opens in a frame as before and its class
  is never created. So do not set `Uri` on an extension that implements
  `IApplicationUIExtension`: the page wins and `CreateContent` is not called.
