# Source Control Switcher

![Source Control Switcher Logo](https://www.ryadel.com/wp-content/uploads/2019/10/Source-Control-Switcher-logo-368x200.png)

Visual Studio can only have one source control provider active at a time, and it does not pick one per
repository. **Source Control Switcher** does: when you open a solution or a folder, it detects which kind of
repository it belongs to and activates the matching source control provider.

| Repository | Detected by | Providers |
|---|---|---|
| Git | a `.git` folder (or file, for worktrees and submodules) in the solution folder or a parent | Git (Microsoft Git provider, built into Visual Studio) |
| Subversion | a `.svn` folder in the solution folder or a parent | VisualSVN, AnkhSVN |
| Team Foundation Version Control | TFVC bindings in the `.sln` file, or a TFVC local workspace (`$tf`) | Visual Studio Team Foundation Server (built in) |
| Perforce | `p4 where` succeeds in the solution folder (only when `p4.exe` is on PATH) | P4VS |
| Anything else | a file or folder name you choose | any source control provider, by GUID |

The extension supports **Visual Studio 2022 and Visual Studio 2026** (Community, Professional, Enterprise; x64 and
Arm64). Users of Visual Studio 2015–2019 can keep using version 1.1.2.

## Options

*Tools* > *Options* > *Source Control Switcher* > *General*:

- **General**
  - *Switch automatically* — switch when a solution or folder opens. When off, switch on demand with
    *Tools* > *Detect Source Control Provider*.
  - *Show status bar notifications* — a status bar message whenever the provider changes.
  - *Logging* — detail written to the *Source Control Switcher* pane of the Output window (Off, Normal, Verbose).
- **Providers** — for Git, Subversion, TFVC and Perforce: *Automatic* (first supported provider installed), a
  specific provider, or *Disabled* (leave the provider alone for that repository type).
- **Detection**
  - *Search parent folders* — look for markers in the solution folder and all its parents.
  - *Detect TFVC bindings in .sln files*.
  - *Detect Perforce* and *Perforce timeout (seconds)* — the Perforce check runs in the background.
- **Custom providers** — `marker=providerGuid` rules separated by `;`. When the marker (a file or folder name)
  is found, the provider with that GUID is activated. Custom rules are checked before the built-in ones.

## Commands

- *Tools* > *Detect Source Control Provider* — re-runs detection for the open solution or folder and activates the
  matching provider, for example after changing the provider by hand.

## Building

See [BUILD.txt](https://github.com/Ryadel/SourceControlSwitcher/blob/master/BUILD.txt).

## Credits

This extension is based on [SccAutoSwitcher](https://github.com/ceztko/SccAutoSwitcher) by *Francesco Pretto*.
Thanks to everyone who contributed fixes and ideas, including *QianMoXi* (start-up with a solution),
*joe-kr* (Visual Studio 2022 support, AnkhSVN 2019) and *davideciarmiello* (TFVC support).

License is MIT.

## Useful References

 * [**Source Control Switcher** on GitHub](https://github.com/Ryadel/SourceControlSwitcher/)
 * [**Source Control Switcher** on Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=Ryadel.SourceControlSwitcher)
 * [**Source Control Switcher** official page](https://www.ryadel.com/en/portfolio/source-control-switcher/)
