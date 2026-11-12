# Developer Notes

## One-attempt rule
Operations that change card state (authentication, file creation, key rotation, etc.) must only be attempted once per request. The provider layer should rely on the caller to orchestrate retries rather than automatically repeating failed calls. Automatic retries can leave a reader or card in an undefined state and mask the original failure reason. When a command fails, return the appropriate error code and let the orchestrator decide whether to re-run the task.

## Normalized provider error codes
The provider layer now returns a small, normalized set of error codes to make downstream handling consistent. These codes are surfaced by `ReaderDevice` and its implementations and should be propagated without collapsing distinct conditions into a generic catch-all:

- `AuthFailure`: authentication or key negotiation failed.
- `PermissionDenied`: access was explicitly refused by policy or configuration.
- `ProtocolConstraint`: the requested operation violated protocol constraints (e.g., resource already exists, limits exceeded).
- `TransportError`: communication problems such as unavailable readers or timeouts.
- `Unknown`: unexpected state or unclassified errors that do not map to the categories above.
- `KeyUnavailable` (set by a task command, not by a reader): a key slot is bound to a secure key provider and the key could not be obtained. The command made no card call and used no other key.

When you add or update provider logic, choose the most specific code that matches the condition so that calling layers can react accurately (e.g., prompting for different credentials versus retrying the connection).

## The Windows Credential Manager as a secure key provider
RFiDGear brings one secure key provider of its own, `windows-credential-manager` (`WindowsCredentialKeyProvider`). A task slot that is bound to it holds a reference instead of a key, and the key is read from the Credential Manager when the task runs.

- The id is stored in project files and never changes. Extensions cannot take it over.
- A reference is the name of a generic credential below the fixed prefix `RFiDGear/key/`. Whatever a project says, the name that reaches the vault starts with that prefix, so a project cannot read the credential of another application.
- The credential holds the hexadecimal digits of the key as UTF-16 text, which is how `cmdkey` stores a password: 12 digits for a MIFARE Classic key, 32 for a DES or AES key, 48 for a 3K3DES key. Without a tool of ours, a key is stored with `cmdkey /generic:RFiDGear/key/<reference> /user:RFiDGear /pass`, which asks for the digits at a prompt. Do not write them on the command line (`/pass:<digits>`): other programs can read a command line, and shell histories and process logs keep it. The prompt reads from the console, so a test cannot drive it; a test stores a dummy value with `/pass:<digits>` to pin the format that the tool writes, and reads it back.
- What it protects against is a key that ends up in a project file, a backup or an e-mail. It does not protect against a program that runs as the same user: the Credential Manager gives the secret to every process of that user.
- The conversion from the stored text to the key works on the bytes and never makes a string, because a string cannot be erased. The copies of the secret that the vault hands out are erased, and the arrays that hold one are on the pinned object heap, so the garbage collector never leaves an old copy behind.
- `ICredentialStore` is the seam in front of the vault. The tests use a dictionary; one test uses the real vault with a name of its own and removes it.

## Key slot editors in the task dialogs
Every box in which a card key is typed is wrapped by `KeySlotEditor` (`Views/KeySlotEditor.xaml`): the sixteen key boxes of the DESFire card mastering, application mastering and data explorer tabs and of the MIFARE Classic key tab. The wrapper lets a person name the key by a reference to a secure key provider instead of typing it.

```xml
<ctl:KeySlotEditor Grid.Row="0" Grid.Column="1" Host="{Binding}" SlotId="desfire.read">
    <TextBox Text="{Binding DesfireReadKeyCurrent, UpdateSourceTrigger=PropertyChanged, Mode=TwoWay}" ... />
</ctl:KeySlotEditor>
```

- The wrapped box keeps everything it had: its style, its tool tip, its validity colours, its `MaxLength`. Only the grid placement and the `Visibility` move to the wrapper. `SlotId` is an id of `SecureKeySlots`; `Host` is the task and has to implement `IKeySlotHost`, which both setup view models do.
- What the editor decides is in `KeySlotEditorState`, with the questions to the person behind `IKeySlotEditorDialogs`, so that it is tested without a window. Switching a slot that holds a key to a reference asks first (a slot without a key is switched without a question). The binding is written first and the key is cleared second, so the slot is never left with neither.
- The box for the reference has the state as its data context, never the task: nothing in it can reach the setter of a key.
- "Store key..." (only for the Credential Manager) takes the digits in a password box, reads them with `KeyEntry` without making a string, and stores them with `WindowsCredentialKeyProvider.Store`. The key is not shown again.
- While the keys of a command are obtained from their providers (`IsFetchingKeys`), the tabs of the dialog take no input (`AreInputsEnabled`): what a person changes meanwhile would no longer be what the keys were asked for.
- A new key box needs the wrapper and a slot id. `KeySlotEditorViewTests` lists the slots of every tab and fails when a key has no editor.
- `x:Static` cannot reach a constant of an internal class: such a value is set in the code-behind.

## Moving the keys of an existing project into the Credential Manager
File > "Move keys of a project to the Credential Manager..." takes a project whose tasks have their keys typed in, stores each key in the Credential Manager and saves a new project that names the keys instead. The project it starts from is only read, and a file is never overwritten.

- `KeyMigration` is the engine, `KeyMigrationFlow` the dialogs (behind `IKeyMigrationDialogs`, so the flow is tested with a person that answers as told). The files of a run go through `IKeyMigrationProjects`; the real one, `DatabaseKeyMigrationProjects`, has a `ProjectManager` with a temporary folder of its own, because creating a `DatabaseReaderWriter` deletes `*.tmp`, `taskdatabase.xml` and `*.rfprj` in the root of its `ProjectManager` and must not do that to the profile of the running application.
- The steps: read the project; collect the typed keys of every task through `IKeyMigrationHost` (`EmbeddedKeySlots` lists the slots the task would write to the project, so a copy that the task only keeps for its own convenience is not moved); store each different key once under a random reference (`migrated/<16 hex digits>`; a slot with the same key shares the credential; a reference that is taken is never overwritten) and read it back; bind the slots in memory; write the new project; read it again.
- A project of a newer version is refused (`IsSupportedVersion`, as in `ProjectHostImpl`): it would be written again without the parts this version does not know. The source is hashed before and after it is read.
- The new project is created, never replaced: `WriteDatabase(..., replaceExisting: false)` puts it in place with a move that fails when a file exists, also one that appeared after the check. It has to pass the check: no key in its text (as written, and as pairs separated by a space), every slot bound to the reference of the run, no key typed into a bound slot, and every reference gives the key of its slot. If a step fails, or the person cancels, the credentials the run stored are removed and the new project is deleted. A run never removes a credential it did not store. What could not be removed is in the report (`UnremovedReferences`, `UndeletedTarget`) and the dialog names it; "nothing is left" is said only when it is true.
- A typed value that is not a key of the length its slot needs (the setters of a task accept anything) is not moved. It is reported and stays in the new project as it was typed.
- Deleting the old project is a second, separate question. `DeleteOriginalAsync` reads the original and the new project again and compares the key the original holds for each moved slot, byte for byte, with what the reference of the new project resolves to (every slot has to name the Credential Manager), because the person may have changed a credential since, also to another key of the same length. It deletes nothing for a failed run, when both reports name the same file, or when the original is no longer the file that was read (its hash, looked at last, right before the delete; a few milliseconds remain between that look and the delete).
- `KeyMigrationReport` has task numbers, slot ids, references, counts and paths, and no key; it is what the person sees and what the logs may hold. The logs of the run have the type of an exception and never its message.
- A project is now written straight into its archive. An earlier version wrote `taskdatabase.xml` into `%LocalAppData%\RFiDGear\` first and zipped that copy; it held every typed key in plain text. A `DatabaseReaderWriter` removes that file from the folder when it is created.

## Hardware cases for the secure key provider
`RFiDGear.HIL.Tests/SecureKeyWorkflowTests` runs bound DESFire tasks on a real card (the reserved test application, AES and DES). They are gated by the setting `secureKeys` in `hil.settings.json`, which is off in the sample, because they change a key on the card and put test credentials into the user's Credential Manager. `SECURE-KEYS.md` next to them lists the cases, the checklist for what needs a person (the PICC master key, a card pulled during a key change, MIFARE Classic and MAD, saving during a run, automatic mode, Format card, the migration) and how to clean up after an interrupted run.

- A setting has to be added in two places: `HilSettings` (the tests) and `Run-HilTests.ps1`, which reads the file with a parser of its own and refuses a name it does not know. `HilSettingsTests` fails when they drift apart.
- The cases have a fixture and a collection of their own (`HilSecureKeyFixture`, `HILSecureKeys`), because xunit creates the fixture of a collection also when every test in it is skipped. `HilFixture` would open the reader and delete the test application from the card then, even with `secureKeys` off. The gate does nothing at all unless the setting asks for it (`HilSecureKeyFixtureTests`); the collections of the project run one after the other.
- The HIL project sees the internals of the host (`InternalsVisibleTo`), so that the cases store their test keys with `WindowsCredentialKeyProvider.Store`, the code the application uses. `HilVault` keeps what it stored under `hil/<run>/...` and removes exactly that when it is disposed.
- The keys typed into a bound slot are decoys on purpose: they are the ones the card would accept where the vault key is refused, or the other way round, so a task that used the typed key could not pass.
- `HilSecureKeyScenarioSelfCheckTests` runs the same task builders and the same vault helper against a scripted reader and a dictionary, so a mistake in the set-up of a case shows up without a card.

## Trying an extension in RFiDGear while you develop it
RFiDGear reads its extensions from `%ProgramData%\RFiDGear\Extensions` once, at start-up. After a build, the extension and the support libraries it needs (`RFiDGear.ExtensionLibraries.*`) can be copied there for you, so you only restart RFiDGear.

- **Off unless asked for**, because a build is not always your own: test runs, clean checkouts and tools that build modified code would otherwise put their output into a real RFiDGear. Turn it on with the value `true` in one of these (the command line beats the local file, which beats the environment):
  - `dotnet build RFiDGear.Extensions/CardCheckAssistant -c Debug -p:DeployExtensionsToRFiDGear=true`
  - `RFiDGear.Extensions/deploy.local.props` (not in the repository, and only seen by the checkout that holds it; a second clone or worktree needs its own), containing `<Project><PropertyGroup><DeployExtensionsToRFiDGear>true</DeployExtensionsToRFiDGear></PropertyGroup></Project>`
  - the environment variable `RFIDGEAR_DEPLOY_EXTENSIONS=true`. With a local props file present it cannot switch the step off again; use `-p:DeployExtensionsToRFiDGear=false` for that.
- Only the extension, its libraries and their symbols are copied; never the contract, the SDK or the host. The last build replaces what is there, even an older build from another checkout; files are skipped only when they are identical. Nothing is deleted.
- The extension is copied first. If it cannot be replaced (RFiDGear is running and holds it), that is a warning and nothing else is copied, so the folder keeps one older set that belongs together; the last message says the deploy is not complete. Close RFiDGear and build again. It is an error only if your build treats warnings as errors.
- The copy has the platform of the build: an x64 build does not load into an x86 RFiDGear and the other way round; an AnyCPU build loads into both. `-p:RFiDGearExtensionsFolder=<folder>` copies somewhere else.
- The database driver that the order store will need is not copied; nothing opens the order database yet.
