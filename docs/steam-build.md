# Steam authentication and GitHub builds

The Windows workflow downloads only managed DLLs from Steam app **2868860**, depot **2868861**. It defaults to **openbetabranch**. Steam requires an account that owns Card Survival: Fantasy Forest; anonymous access is not available for this depot.

## 1. Create a Steam session locally

Install the **.NET 10 SDK**. Local authentication setup supports **Windows PowerShell 5.1** and **PowerShell 7**; the CI scripts use PowerShell 7. From this repository, run:

```powershell
./tools/Initialize-SteamSession.ps1 -Username 'your-steam-login-name'
```

The script checks the SDK before downloading or logging in. It uses .NET 10 from your `PATH`, or a portable SDK at `obj/steam-tools/dotnet/dotnet.exe`. If `dotnet --version` reports 9.x, install the .NET 10 **SDK** (the runtime alone cannot build the helper), then reopen PowerShell. You can also select an existing portable SDK explicitly:

```powershell
./tools/Initialize-SteamSession.ps1 -Username 'your-steam-login-name' -DotNetPath 'D:/tools/dotnet10/dotnet.exe'
```

The script downloads checksum-pinned DepotDownloader 3.4.0 and starts its interactive login. Enter your Steam password in your local terminal and complete Steam Guard when prompted. It requests the open-beta manifest to check access, without downloading the whole game. A dedicated Steam account with its own game license limits the scope of the CI credential.

After a successful login, the script writes **obj/steam-auth/session.b64**. This ignored file contains the remembered login token and any Steam Guard data, not your password. Base64 is encoding, not encryption: treat the file as an account credential. Do not commit, cache, upload as an artifact, or paste it into chat. The helper restores any pre-existing DepotDownloader isolated-storage file on normal exit.

## 2. Configure GitHub authentication

In the repository's **Settings → Environments**, create an environment named **steam-build**. Restrict its deployment branches/tags to trusted refs, normally branch `master` and release tags `v*`. Add required reviewers if your repository plan supports them. Only allow a development branch for manual builds after reviewing the code on that branch: an authenticated workflow executes that branch's scripts.

Add these two **environment secrets**:

| Secret | Value |
| --- | --- |
| `STEAM_USERNAME` | The Steam login name used in step 1 |
| `STEAM_SESSION_B64` | The complete contents of `obj/steam-auth/session.b64` |

If you already use GitHub CLI and have authenticated it for this repository, you can set them without printing the session to the terminal:

```powershell
Read-Host 'Steam login name' | gh secret set STEAM_USERNAME --env steam-build
Get-Content ./obj/steam-auth/session.b64 -Raw | gh secret set STEAM_SESSION_B64 --env steam-build
```

Check both commands succeeded. After saving the secret, remove your local exported copy:

```powershell
Remove-Item -LiteralPath ./obj/steam-auth/session.b64
```

No Steam password, authenticator shared secret, or beta password needs to be stored in GitHub. Keep Steam Guard enabled. The former `GAMEASSEMBLYDOWNLOADPATH` and `IL2CPP_GAMEASSEMBLYDOWNLOADPATH` secrets are unused and may be removed.

## 3. Run a build

Once this workflow is on the default branch, open **Actions → Build Windows mod → Run workflow**. Choose `openbetabranch` and leave the manifest input blank to download that branch's latest Windows assemblies. Approve the environment deployment if you configured reviewers.

Pushes to `master` and tags matching `v*` also build against the latest open beta. Release tags must match the version in `CSFFCardDetailTooltip.csproj` exactly, for example `v1.0.11`. Other builds use a package version such as `1.0.11-ci.42`, while the BepInEx plugin version remains `1.0.11`.

The artifact contains the mod DLL, README, license, and `game-build.json`. The JSON records the actual Steam manifest ID, game assembly SHA256, branch, commit and mod versions. It contains no game DLLs or Steam authentication data. You can supply its manifest ID in a later manual run to select the same game data, while Steam still makes that manifest available. NuGet and SDK patch updates are not pinned, so this is not a guarantee of byte-identical builds.

The `public` branch option is available for compatibility checks, but this mod targets the open beta. A build against an older public assembly may correctly fail. Static reference checks catch missing game members and Harmony targets; they do not prove tooltip behavior in a running game.

Pull requests run only secret-free script, version and session-serialization checks. They do **not** compile the mod or download licensed game data. Authenticated builds run on trusted pushes, release tags or maintainer dispatches, never through `pull_request_target`.

## Publishing a release

Push an annotated version tag matching the project, such as `v1.0.11`. After its **Build Windows mod** run succeeds, **Publish release** verifies the tag commit and artifact provenance, then publishes `CSFFCardDetailTooltip-1.0.11.zip` on GitHub Releases. The release description contains only commit subjects and hashes since the preceding reachable `v*` tag. It does not use GitHub's generated summaries or contributor sections.

To publish a tag whose build completed before the publishing workflow was installed, manually run **Publish release** with that successful tag build's run ID. This reuses the existing artifact without moving the tag or rebuilding. Expired artifacts require a new successful run of the original tag build. Existing published releases are left intact on retries. No additional secret is required; publishing uses the workflow's `GITHUB_TOKEN`.

## Renewing authentication and troubleshooting

- **Missing secrets:** check the exact environment name and both secret names above. Forks need their own environment and owning account.
- **Waiting for approval:** approve the `steam-build` environment deployment, or check its branch/tag restrictions.
- **Expired or revoked session / Steam asks to log in again:** repeat step 1 and replace `STEAM_SESSION_B64`. The CI helper fails instead of accepting an interactive login; it does not update GitHub secrets automatically.
- **No subscription or access denied:** verify that the selected account owns this game and can access the chosen branch/manifest.
- **Missing game members or failed compilation:** the downloaded game API may have changed. Check the selected branch and update the mod; the workflow does not fall back to stale assemblies.
- **Credential exposure:** revoke the account's affected sessions through Steam's authorized-device controls, remove the exposed GitHub secret, then create a fresh session.

The session is imported only for the download process and removed afterward on normal exit; GitHub-hosted runners are temporary. Do not run this workflow on a shared persistent self-hosted runner without separately managing its credential storage.

References: [DepotDownloader parameters](https://github.com/SteamRE/DepotDownloader#parameters), [its pinned account-storage implementation](https://github.com/SteamRE/DepotDownloader/blob/DepotDownloader_3.4.0/DepotDownloader/AccountSettingsStore.cs), and [GitHub environment and repository secrets](https://docs.github.com/en/actions/how-tos/write-workflows/choose-what-workflows-do/use-secrets).
