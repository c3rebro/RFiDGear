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
