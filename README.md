# Proton Pass (unofficial) for PowerToys Command Palette

> **Unofficial. Not affiliated with, endorsed by, or supported by Proton AG.**
> "Proton" and "Proton Pass" are trademarks of their owners. This project contains no Proton
> logos or assets and does not bundle or redistribute the `pass-cli` binary.

A [PowerToys Command Palette](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/overview)
extension that wraps the [Proton Pass CLI](https://protonpass.github.io/pass-cli/) (`pass-cli`) so you
can search your vault and copy passwords, usernames and TOTP codes without leaving the keyboard.

## What it does

- **Proton Pass** command: a searchable list of every active item across all your vaults
  (title, vault name, item type).
- **Enter** copies the password (logins). Notes and other item types copy their `pass://` reference.
- **Context menu** (`Ctrl+K`): copy username (falls back to email), copy TOTP code, open URL,
  copy `pass://` reference, refresh items.
- **Clipboard auto-clear** after a configurable delay (default 20 s), only if the clipboard still
  contains what the extension put there. Copies are flagged so Windows keeps them out of clipboard
  history (Win+V) and cloud clipboard sync.
- **Status messages** for: `pass-cli` not found, not logged in, timeout, empty vault, and failed commands.

Rows deliberately show no username: `pass-cli item list` returns metadata only, and fetching usernames
would mean loading secrets (or one CLI call per item) when the list opens. Usernames are fetched when you
invoke an action.

## Prerequisites

- Windows 11 (or Windows 10 19041+)
- [PowerToys](https://github.com/microsoft/PowerToys) with **Command Palette enabled**
  (developed against the Command Palette SDK package `Microsoft.CommandPalette.Extensions` 0.9.260303001; the
  API is in preview and may change)
- Developer Mode enabled in Windows settings (needed to install an unsigned dev package)
- The Proton Pass CLI, installed yourself:

  ```powershell
  winget install --id Proton.ProtonPass.CLI --exact
  pass-cli login
  ```

  If `pass-cli` is not on `PATH`, add its folder to `PATH` or set the full path in the extension's settings.
  (winget normally places it under `%LOCALAPPDATA%\Microsoft\WinGet\Packages`.)

## Build and install

Requires the .NET 10 SDK and the Windows 11 SDK (10.0.26100):

```powershell
winget install --id Microsoft.DotNet.SDK.10 --exact
winget install --id Microsoft.WindowsSDK.10.0.26100 --exact
```

**Visual Studio (recommended):** open `ProtonPassCliExtension/ProtonPassCliExtension.sln`, choose the
**Debug** configuration and the **x64** platform with the **(Package)** launch profile, then use
**Build > Deploy ProtonPassCliExtension**. Building alone does not register the package.

**Command line** (build verified; the `Add-AppxPackage` registration step has **not been tested** by the author, so prefer Visual Studio's Deploy if it fails; `Remove-AppxPackage` undoes it):

```powershell
cd ProtonPassCliExtension
dotnet build ProtonPassCliExtension.sln -p:Platform=x64 -c Debug
Add-AppxPackage -Register ProtonPassCliExtension\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\AppxManifest.xml
```

**After every redeploy, open Command Palette and run `Reload` ("Reload Command Palette extensions").**
Command Palette does not notice a re-deployed package on its own.

## Usage

1. Open Command Palette and run **Proton Pass**.
2. Type to filter. `Enter` copies the password. `Ctrl+K` opens the other actions.
3. Settings (Command Palette settings > Extensions > Proton Pass): path to `pass-cli`, clipboard clear
   delay in seconds (`0` disables), and item-list cache lifetime in seconds.

The first open loads items from every vault, which can take a few seconds. Use **Refresh items** after
changing your vault.

## Tests

```powershell
cd ProtonPassCliExtension
dotnet test ProtonPassCliExtension.Tests/ProtonPassCliExtension.Tests.csproj -p:Platform=x64
```

The tests mock the process layer and use fake fixtures in `tests/fixtures/`; they never call the real CLI.

## Security notes

- Secrets are fetched only at the moment you invoke an action, never when the list loads.
- Only item metadata (title, vault name, type, IDs) is cached, in memory. Nothing secret is cached or written to disk.
- Secrets are never logged and never appear in status messages or error objects. `pass-cli` is invoked with
  separate arguments (no shell, no string-built command lines) and secrets never travel on a command line.
- JSON for `item view` / `item totp` is deserialised into models that omit password and TOTP-seed fields.
- The clipboard clear compares a SHA-256 hash and the clipboard sequence number, so the plaintext is not
  kept around while the timer runs.
- Limitation: .NET strings cannot be reliably zeroed, so a copied secret exists in this process's memory until
  garbage collected, and the clipboard is visible to any app while it holds the secret.

## Status / not verified

- "Not logged in" detection is a loose text match on stderr. It was checked against pass-cli 2.4.2 run with an
  empty `PROTON_PASS_SESSION_DIR` ("This operation requires an authenticated client", exit code 1); other
  versions or an expired (rather than missing) session may word it differently.
- Win32 clipboard code was checked with a dummy string from a console app (set, read back, `Get-Clipboard`,
  clear). That the "exclude from clipboard history / cloud sync" flags are honoured by Win+V has not been checked.
- Behaviour inside Command Palette itself (list display, context menu, toasts, status messages, settings
  persistence via the toolkit's `JsonSettingsManager`, clipboard clear) is built against the toolkit API but has not been verified end to end.

## License

MIT. See [LICENSE](LICENSE).
