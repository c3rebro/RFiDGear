using Xunit;

// Tests run one at a time because they share process-wide and machine-wide state that the
// per-class [Collection] attributes do not fully cover:
//  - WPF's Application.Current singleton. Some tests create one on a throwaway STA thread whose
//    dispatcher is then shut down, so later tests see a non-null Application with a dead dispatcher.
//  - %LocalAppData%\RFiDGear\settings.xml, reached through the many parameterless
//    `new SettingsReaderWriter()` / ResourceLoader.GetResource call sites that cannot be injected yet.
// Project load/save tests are already isolated through an injected ProjectManager root, so they do not
// depend on this. Re-enable parallelism only once the settings access above is injectable.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
