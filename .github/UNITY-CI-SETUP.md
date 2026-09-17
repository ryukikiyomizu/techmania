# Setting up Unity CI for this repo

Runbook for `.github/workflows/unity-ci.yml`. Nothing in here passes through a chat
window — every secret below is typed by you, straight into `gh` or the GitHub UI.

> **Status: on hold.** Licensing is wired up (steps 4-5 are now just two secrets). What
> still blocks a green run is **steps 1-3, the FMOD package** — nothing in the project can
> compile without it, so don't spend time on the rest until that archive exists.
> Player builds are not in the workflow yet, but they need no new code — see
> "Adding a player build" below.

## Why this is needed at all

`TECHMANIA/.gitignore` excludes the FMOD integration on purpose:

```
# FMOD - developers should acquire their own license, then download "FMOD for Unity"
# then import into project.
/[Aa]ssets/Plugins/FMOD/
```

`FmodManager.cs`, `FmodChannelWrap.cs` and `FmodSoundWrap.cs` compile against it, and
almost everything audio-related uses those three. So a clean checkout **cannot compile**
until CI is given the folder out-of-band. That is the entire reason for steps 1-3.

## You need to produce

| # | kind | name | value |
|---|---|---|---|
| 1 | repo **variable** | `FMOD_RESTORE_REPO` | `ryukikiyomizu/techmania-ci-deps` |
| 2 | secret | `FMOD_RESTORE_TOKEN` | fine-grained PAT, `Contents: Read-only`, scoped to that one private repo |
| 3 | secret | `UNITY_USERNAME` | the e-mail of your Unity ID |
| 4 | secret | `UNITY_PASSWORD` | that account's password |

Names must match exactly; the workflow reads `vars.FMOD_RESTORE_REPO` and those
`secrets.*`.

There is **no `.ulf` file to deal with any more** — the activation action logs in with the
Unity ID and handles the seat itself, and it registers a `post:` step that returns the
license when the job ends (even on failure), so a crashed run does not strand your
activation.

---

## 1. Get FMOD for Unity 2.03.12

The version this project was last updated against is **2.03.12** (upstream
`dd835e54 "Update FMOD Unity to 2.03.12... though it's fully ignored in git"`).

1. Sign in / register at <https://www.fmod.com/download#fmodforunity> and accept the
   license. Grab **FMOD Studio Unity integration 2.03.12** (the `.unitypackage`).
2. In your local Unity project on the Windows PC, `Assets > Import Package > Custom
   Package...` and import it. Do **not** cherry-pick subfolders — the package's own
   `Assets/Plugins/FMOD/Plugins/<arch>/` native binaries are needed too.
3. Sanity check in the editor: `Window > FMOD > Settings` opens, and the project's
   `Assets/Plugins/FMOD/` contains `.meta` files for everything.
4. Also verify `TECHMANIA/ProjectSettings/EditorBuildSettings.asset` gained no extra
   scene and that the FMOD manager banks didn't get re-authored into `Main.unity` —
   commit nothing from this project back to the public repo except, optionally, the
   `.gitignore` note below.

## 2. Pack it — keep the `.meta` files

The `.meta` files carry the GUIDs Unity already resolved. Shipping them keeps every
run byte-identical to your editor and avoids a reimport of every audio asset.

**No terminal needed.** In File Explorer go to
`...\Techmania source\TECHMANIA\Assets\Plugins`, **right-click the `FMOD` folder
itself** → *Compress to ZIP file* (Win10: *Send to → Compressed (zipped) folder*).
The name doesn't matter — `FMOD.zip` is fine, because the private repo in step 3 holds
nothing else for CI to mistake it for.

The one rule: zip the **folder**, not its contents. Right-clicking `FMOD` and zipping
gives entries like `FMOD/src/...`, which is exactly what the restore step extracts into
`Assets/Plugins`. Opening the folder, Ctrl-A, and zipping gives `src/...` and fails. If
you ever doubt it, double-click the zip: the first thing you see must be the `FMOD`
folder.

Prefer a tarball? It also works, and must be laid out the same way (one top-level
`FMOD/`):

```bash
tar -czf ../fmod-unity-2.03.12.tar.gz -C TECHMANIA/Assets/Plugins FMOD
tar -tzf ../fmod-unity-2.03.12.tar.gz | head -5   # must start with "FMOD/"
```

A failed `tar` can leave an empty `.tar.gz` behind, so if you try this route delete any
half-made archive first — `tar -tzf` on a 0-byte file prints nothing at all, which looks
deceptively like a pass.

Only `src`, `platforms` and `Resources` actually matter for compiling (three files use
`FMOD.*` / `FMODUnity.*` types, all in `Assets/Scripts/Fmod/`), but ship the whole
folder — `Cache/` and `images/` cost little and something always wants them.

## 3. Put it in a private sibling repo

The fork is **public**, so FMOD's SDK must not go in it — its licence is exactly why
`/[Aa]ssets/Plugins/FMOD/` exists. A separate private repo keeps it out of the public
tree, and CI reads it with one read-only token. Five screens, no terminal:

1. `github.com/new` → owner `ryukikiyomizu` → name `techmania-ci-deps` → **Private** →
   tick *Add a README* → *Create repository*.
2. On the new repo: **Add file → Upload files** → drag the archive in → **Commit
   changes**. Top level of the repo, not inside a folder.

   The web upload refuses anything over **25 MB**, and a whole FMOD folder is usually
   40-90 MB because it carries native binaries for every platform. CI does not need
   those: it compiles, and only the C# headers and `.asmdef` files matter for that (the
   `x86`/`x86_64` native folders are already committed in techmania itself). So strip
   them — one command, from inside `TECHMANIA\Assets\Plugins`:

   ```powershell
   tar -czf "$HOME\fmod-code.tar.gz" -C . FMOD `
     --exclude='*/bin/*' --exclude='*.dll'  --exclude='*.so'    --exclude='*.dylib' `
     --exclude='*.a'    --exclude='*.lib'   --exclude='*.exp'   --exclude='*.aar' `
     --exclude='*.framework' --exclude='FMOD/Cache/*'
   tar -tzf "$HOME\fmod-code.tar.gz" | Select-Object -First 3   # must start with FMOD/
   "{0:N1} MB" -f ((Get-Item "$HOME\fmod-code.tar.gz").Length/1MB)
   ```

   Expect roughly 1-4 MB. The archive is then picked up automatically: the restore step
   takes any `.zip` or `.tar.gz` it finds, so this needs no workflow change. Unity will
   log warnings about orphaned `.meta` files for the stripped binaries - that is
   cosmetic and only ever matters for a player build, which this trimmed archive cannot
   produce. If you later want CI to build the .exe itself, the full folder has to go in
   as a *release asset* instead (2 GB limit) and I would point the workflow at that.
3. `github.com/settings/personal-access-tokens/new` → fine-grained → name it
   `ci-fmod-read` → *Repository access:* **Only select repositories** → pick
   `techmania-ci-deps` → *Repository permissions* → **Contents: Read-only** →
   *Generate token* → copy it now, it is shown once.
4. `github.com/ryukikiyomizu/techmania/settings/secrets and variables/actions` →
   **Variables** tab → *New repository variable* →
   name `FMOD_RESTORE_REPO`, value `ryukikiyomizu/techmania-ci-deps`.
5. Same page, **Secrets** tab → *New repository secret* →
   name `FMOD_RESTORE_TOKEN`, value the token from step 3.

That is the whole setup. It deliberately stores the zip as a *committed file* rather than
a release asset: a release needs a tag spelled identically in two places, and that
mismatch is the most common way this breaks. One name, one place, and the restore step
takes any `.zip` it finds.

The token can read that one repo's files and nothing else — not settings, not your other
repos. If you lose track of it: `github.com/settings/personal-access-tokens` → delete →
make another → replace the secret.

## 4. The Unity license

Two secrets, nothing else: `UNITY_USERNAME` (your Unity ID e-mail) and `UNITY_PASSWORD`.
The workflow activates with `RageAgainstThePixel/activate-unity-license@v2.2.2`,
`license: Personal`, `license-version: '6.x'`.

Read that action's `action.yml` and you'll see both `main:` and `post:` point at the same
bundle — the `post:` half is what returns the seat at job end, including on failure. That
is why there is no separate "return license" step here (and why none was needed): I checked
both `RageAgainstThePixel` and `buildalon` and **neither publishes a
`return-unity-license` action**, so the built-in cleanup is the mechanism to rely on.

Three real caveats:

- **Pin the version, not `@main`.** The workflow pins `@v2.2.2`, `@v2.6.0`, `@v3.1.0`
  (each action's latest release as of writing). This action authenticates *as you*, so a
  floating tag is the wrong place to be convenient. Bump deliberately.
- **2FA.** If your Unity ID uses 2FA, username/password auth can be rejected — use an app
  password, or a dedicated non-2FA build account.
- **Daily activation cap.** Personal seats allow only a few activate/deactivate cycles per
  day, so don't debug by hammering re-runs. The `concurrency:` guard also stops two runs
  racing for the one seat.
- Because the action signs in with your credentials, **fork PRs must not get them.** The
  `unity` job is guarded with a same-repo `if:`, but also set
  *Settings → Actions → General → Require approval for all outside collaborators*.

## 5. Everything Actions needs, in one place

Four values total. All four live on the **techmania** repo's
`Settings → Secrets and variables → Actions` page — secrets and variables are two tabs
of that one screen.

| where | name | value |
|---|---|---|
| Secrets tab | `UNITY_USERNAME` | your Unity ID e-mail |
| Secrets tab | `UNITY_PASSWORD` | that account's password |
| Secrets tab | `FMOD_RESTORE_TOKEN` | the read-only PAT from step 3 |
| **Variables** tab | `FMOD_RESTORE_REPO` | `ryukikiyomizu/techmania-ci-deps` |

**Variables and Secrets are two tabs of one page and they are not interchangeable.**
Anything filed under *Variables* is readable as `vars.NAME`, stored as plaintext, and is
**not** masked in workflow logs; CI will still call it "unset" because it looks in
`secrets.*`. `FMOD_RESTORE_REPO` is deliberately a variable (it is only a repo name), the
token is deliberately a secret.

A misspelled *name* is the other classic silent failure: the workflow just reports the item as
missing. Verify without leaving the browser by re-opening that page, or with
`gh secret list && gh variable list`.

Both Unity secrets are already in place, which is why the first run of PR #1 stopped
at preflight and named FMOD as the only remaining blocker. If you ever prefer the CLI over the web
forms, `gh secret set UNITY_PASSWORD` prompts and pipes nothing to a file or a chat log.

## 6. Run it

```bash
git add .github/workflows/unity-ci.yml .github/UNITY-CI-SETUP.md
git commit -m "Add Unity CI: compile check + EditMode tests"
git push origin HEAD

gh workflow run "Unity CI (compile + EditMode tests)"
gh run watch
```

Triggers: `workflow_dispatch`, pushes to `master`, and PRs touching `**/*.cs`,
`Packages/**` or `ProjectSettings/**` (same-repo only).

Budget **30-50 min** for a first run on `windows-latest` (Unity install + cold
`Library/` import of ~4,000 assets). The editor install is cached by `unity-setup` and
the `Library` by `actions/cache`, so later runs are much shorter. Public repo → minutes
are free, but Windows runners still count against the concurrency of 5 on a free plan,
so a run may queue.

## 7. Reading the result

| symptom | meaning |
|---|---|
| `preflight` fails, summary says FMOD MISSING | step 1-3 not done, or `FMOD_RESTORE_REPO` / `FMOD_RESTORE_TOKEN` wrong |
| `preflight` fails, "`UNITY_USERNAME` / `UNITY_PASSWORD` not set" | step 4 |
| `error CS0246: The type or namespace name 'FMOD'` | restore ran but the archive layout is wrong — re-check step 2's `tar -tzf` |
| `Logs/tests.log` mentions NUnit but the compile step passed | expected pre-existing gap, see below — it is *not* a compile failure |
| green | the tree compiles, and `DjProgressionTests`, `ProfileCredentialTests`, `ProfileCredentialFlowTests`, `NfcReaderServiceTests` passed |

### If the tests step fails but the import step passed

`packages-lock.json` resolves `com.unity.test-framework` as **`1.6.0`, `source:
builtin`** — the editor ships it, so it does *not* need a `manifest.json` entry and
this may well just work. The only remaining question is whether `nunit.framework.dll`
is auto-referenced into `Assembly-CSharp-Editor`, where the four test files sit with no
test assembly of their own. If it isn't, fix by adding
`TECHMANIA/Assets/Scripts/Editor/Tests.asmdef`:

```json
{
  "name": "Techmania.EditorTests",
  "references": [
    "UnityEngine.TestRunner",
    "UnityEditor.TestRunner"
  ],
  "includePlatforms": [ "Editor" ],
  "excludeReferences": [],
  "overrideReferences": true,
  "precompiledReferences": [ "nunit.framework.dll" ],
  "autoReferenced": false,
  "defineConstraints": [ "UNITY_INCLUDE_TESTS" ],
  "versionDefines": [],
  "noEngineReferences": false
}
```

Create it in the editor once so Unity generates the matching `.meta`. Don't add a
`com.unity.test-framework` line to `manifest.json` — pinning a version over a builtin
package risks downgrading it. If the tests already compile without the asmdef, leave it
alone: adding one moves the tests into a separate assembly.

## Optional, but I'd do it

- **Drop the MCP package** from `Packages/manifest.json`:
  `"com.coplaydev.unity-mcp": "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main"`
  is dev tooling pinned to a floating `#main`, so every CI package resolve clones it
  and can break on someone else's merge.
- **Stop committing `TECHMANIA/graphify-out/`** (208 MB, 1,034 tracked files). The
  workflow `rm -rf`s it before import so it costs Unity nothing, but everyone still pays
  for it on every clone. A `.gitignore` entry alone would do nothing here, because the
  files are already tracked - it needs `git rm -r --cached TECHMANIA/graphify-out` plus
  the ignore line, as its own commit so it is easy to revert.
- **Player builds** need one extra step before them, not new code — see below.

## Adding a player build

I said earlier that this project has no CLI entry point for `BuildPipeline.BuildPlayer`.
It does: **`Assets/Scripts/Editor/LoadProbeBuild.cs`** already exposes `public static void
Build()`. Despite the name it is not probe-specific — it uses `BuildOptions.None`, takes
its scene list from `EditorBuildSettings.scenes` (enabled ones), writes
`<TECHMANIA_PROBE_BUILD_DIR>/TECHMANIA.exe` for `StandaloneWindows64`, and **throws** if
`BuildResult != Succeeded`, which is exactly what you want from CI. The only thing the
probe ever contributed is the env-var name and the fact that I kept this file while
deleting the T3 tooling around it.

Insert these two steps after the import step (before the tests, so a broken build fails
the run):

```yaml
      # BuildPostProcessor.CopyDefaultAssetBundle File.Copy()es Assets/AssetBundles/default
      # next to the built player as Themes/Default.tmtheme, so a missing source is a
      # FileNotFoundException mid-build. That bundle is build output and is gitignored
      # (Assets/.gitignore: /[Aa]ssets/[Aa]sset[Bb]undles/*), which is why CI has to
      # build it and why a fresh clone has no Themes/Default.tmtheme to fall back on.
      - name: Build default theme bundle
        uses: buildalon/unity-action@v3.1.0
        with:
          log-name: bundles
          build-target: ${{ env.BUILD_TARGET }}
          args: '-quit -batchmode -nographics -executeMethod BuildAssetBundleWindow.BuildForDefaultPlatform'

      - name: Build Windows player
        env:
          TECHMANIA_PROBE_BUILD_DIR: ${{ github.workspace }}\Build
        uses: buildalon/unity-action@v3.1.0
        with:
          log-name: player
          build-target: ${{ env.BUILD_TARGET }}
          args: '-quit -batchmode -nographics -executeMethod LoadProbeBuild.Build'

      - uses: actions/upload-artifact@v4
        with:
          name: windows-player
          path: Build/TECHMANIA_Data
          if-no-files-found: error
```

Two caveats that will bite regardless of CI:

- `Assets/StreamingAssets` is gitignored, so a CI-built player ships with **no tracks**
  and no keysounds; it will boot to the song list and then fail on selection. Fine as a
  compile/build smoke test, useless as a playable build.
- `upload-artifact` needs the `_Data` sibling next to the `.exe` — uploading only
  `TECHMANIA.exe` gives you an unrunnable file.
