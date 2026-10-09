# Releasing

Package identifier: `kud3n013.ProtonPassCliExtension`. Installers are built with Inno Setup, hosted on GitHub Releases
and published to the Windows Package Manager community repo
([microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs)).

Related files: `installer/ProtonPassCliExtension.iss`, `scripts/` (`check-version.ps1`, `build-installer.ps1`,
`new-winget-manifest.ps1`), `.github/workflows/release.yml`.

## One-time setup

- [ ] Install the tools for local builds: `winget install JRSoftware.InnoSetup`
- [ ] Fork `microsoft/winget-pkgs` (only needed if you submit by hand; `wingetcreate` can fork for you)
- [ ] Later, once the package exists in winget: create a classic GitHub personal access token with the `public_repo`
      scope and add it as the repository secret `WINGET_TOKEN` (Settings > Secrets and variables > Actions).
      Without it the `winget-update` job is skipped.

## Cutting a release

1. **Bump the version.** The single source of truth is `<Version>` in `ProtonPassCliExtension/Directory.Build.props`.
   Also set the same version (as `x.y.z.0`) in `ProtonPassCliExtension/ProtonPassCliExtension/Package.appxmanifest`
   (`Identity Version`) and `app.manifest` (`assemblyIdentity version`).
   `./scripts/check-version.ps1` fails if any of them disagree (it also checks the CLSID is the same everywhere).
2. **Build and test locally.**
   ```powershell
   dotnet test ProtonPassCliExtension/ProtonPassCliExtension.sln -p:Platform=x64
   ./scripts/build-installer.ps1 -Platform all      # writes installers + SHA256SUMS.txt to artifacts/
   ```
   Install the x64 installer silently, run **Reload** in Command Palette, check the extension works, then uninstall
   and confirm the folder and `HKCU\Software\Classes\CLSID\{c312420f-...}` are gone:
   ```powershell
   .\artifacts\ProtonPassCliExtension-<version>-x64.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
   & "$env:LOCALAPPDATA\Programs\ProtonPassCliExtension\unins000.exe" /VERYSILENT /SUPPRESSMSGBOXES
   ```
   If you have the loose-layout dev package registered (`Add-AppxPackage -Register`), remove it first
   (`Get-AppxPackage ProtonPassCliExtension | Remove-AppxPackage`) so the two registrations don't compete.
3. **Commit** the version bump.
4. **Tag and push** (this is what triggers the workflow):
   ```powershell
   git tag v<version>
   git push origin main v<version>
   ```
   You can rehearse without publishing: Actions > Release > *Run workflow* builds everything and produces dry-run
   manifests but creates no release.
5. **The workflow** (`release.yml`) fails if the tag differs from the project version, runs the tests, builds x64 and
   arm64 installers, creates the GitHub Release with the installers and `SHA256SUMS.txt`, and uploads the
   `winget-manifests` workflow artifact (hashes are of the files as uploaded to the release).
6. **First release only: submit to winget by hand.** Download the `winget-manifests` artifact, then either:
   - copy `k/kud3n013/ProtonPassCliExtension/<version>/` into your fork of winget-pkgs under `manifests/` and open a PR, or
   - run `wingetcreate submit <that folder>`.

   Before submitting: `winget validate --manifest <folder>`. The manifests must keep the tag
   `windows-commandpalette-extension` (that is how Command Palette discovers extensions).
7. **Later releases:** once the package exists in winget-pkgs and `WINGET_TOKEN` is set, the `winget-update` job opens
   the update PR automatically. If it was skipped, run it yourself:
   ```powershell
   wingetcreate update kud3n013.ProtonPassCliExtension --version <version> `
     --urls "<x64-url>|x64" "<arm64-url>|arm64" --submit
   ```
8. Watch the PR (see below), then update the README note once the package is live.

## Handling winget validation results

Bot labels and comments appear on the PR. The policy docs list them all:
<https://learn.microsoft.com/windows/package-manager/package/repository> and
<https://learn.microsoft.com/windows/package-manager/package/windows-package-manager-policies>.

| Label / symptom | What it means and what to do |
|---|---|
| `Validation-Hash-Mismatch` | The SHA256 in the manifest differs from the file at the URL. Never re-upload a changed file under an existing release. Regenerate the manifest with `scripts/new-winget-manifest.ps1 -Version <v>` (it hashes the downloaded release assets), push it to the PR branch, and re-run validation. If the installer really changed, cut a new patch version instead. |
| `Validation-Unattended-Failed` | The installer did not finish silently. Reproduce locally: `installer.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART`. Make sure nothing prompts (the `InfoBeforeFile` page is skipped when silent) and that `InstallerType: inno` is set so winget passes the Inno switches. |
| `Validation-Installation-Error` / `Validation-Uninstall-Error` | The install or uninstall failed or left files behind in the validation VM. Re-test install, uninstall and reinstall over an existing install; check the `{AppId}_is1` uninstall key exists and that the uninstaller removes `{app}` and both `HKCU\Software\Classes\CLSID\{...}` keys. The manifest `ProductCode` must equal `{2A158EE8-5834-42CE-9837-A00C5987E3D6}_is1`. |
| `Validation-Defender-Error` (Defender flags the installer) | Almost certainly a false positive on an unsigned installer. Submit the file at <https://www.microsoft.com/wdsi/filesubmission> as "software developer" with the release URL and hashes, then comment on the PR with the submission ID. Don't change the file to evade detection. Code-signing the installer reduces these. |
| `Manifest-Validation-Error` / schema errors | Run `winget validate --manifest <folder>` and fix what it reports. |
| `Needs-Author-Feedback` | A moderator asked a question. Reply in the PR; unanswered PRs are closed after a while. |
| Dependency not found | `Proton.ProtonPass.CLI` must exist in winget (check with `winget show Proton.ProtonPass.CLI`). |

If a version is already merged and found to be broken, publish a fixed higher version rather than editing the old one.
