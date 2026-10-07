# Changelog

## 0.1.0-beta.5 — 2026-10-07

### Added

- Playlists: repeat ordered bar effects with separate durations and speeds for CPU and GPU.
- Bar response: see the sensor reading, resulting segments or speed, smoothing,
  input range and any hold near a level boundary. Unapplied drafts are labelled separately.
- Profile previews and direct activation from Profiles or the tray.
- Guided sensor setup and a ten-second bar check that restores the active profile.
- Copy animation between CPU and GPU while retaining the destination sensors and range.
- Window position, size and maximized state restored across launches.

### Changed

- Distinct Animated bar (speed), Sensor meter (level), Fixed level and Playlist controls.
- Shorter sensor labels, relevant readings first and technical IDs in secondary details.
- Compact header, adjustable preview/editor divider and contextual profile actions.
- English interface and system-error summaries, with regional number and date formats retained.

### Fixed

- Sensor meters use the nearest segment instead of rounding down. Smoothed full-scale
  readings can reach the full bar; hysteresis cannot make the endpoint unreachable.
- Independent random sequences in CPU and GPU previews.
- Activating a saved profile respects Save or Discard when the editor has unsaved changes.
- Sensor selectors show readable labels, and tray profile submenus follow the selected theme.
- Connection notices no longer shift the editor while a dropdown is open.
- Startup diagnosis identifies forced administrator compatibility, which can prevent
  automatic launch. The app does not change that Windows setting.

### Updating

Exit the running app from its tray menu, extract the entire new ZIP and run the new
EXE. Keep the EXE and Core DLL together. Settings remain under
`%LOCALAPPDATA%\ZX6DisplayControl`. If the app folder moved and Start with Windows
is enabled, save preferences from the new copy to repair startup registration.

Requires one Z-X6, AIDA64 shared memory and .NET Framework 4.7.2 or later.
The executable is unsigned.

## 0.1.0-beta.2 — 2026-10-06

### Added

- A combined Display workspace with a holder-style animated preview and separate
  CPU/GPU controls. The preview distinguishes output sent to the holder from
  changes that have not been applied.
- GitHub update notices, including published betas. The app checks at startup
  at most once every 24 hours; automatic checks can be disabled. Manual checking
  and a link to the release page are available in Settings & About.
- Connection status buttons with access to sensor and USB recovery actions.
- Visible explanations of animation presets and the ranges they use.

### Changed

- Built-in profiles are protected. Saving changes to one creates a personal copy;
  built-ins cannot be overwritten, renamed or deleted.
- App preferences have separate Save and Revert actions. Saving a profile leaves
  pending preferences alone, and saving preferences preserves profile edits.
- Profiles are grouped into built-ins and personal configurations. Selecting a
  profile previews it; Use profile activates it.
- Temperatures above 99 °C keep the display on at 99, with the actual temperature
  reported in the app. Sensor-driven animations use the original reading.
- The connection workflow consistently supports one holder at a time.

### Fixed

- Live refresh no longer closes open sensor, Mode, Preset or Profile menus.
- The applied preview uses the last successful transmission and clears when the
  display is off or disconnected.
- Older customized built-ins and profile names are preserved during migration.
- A separate pre-migration settings snapshot survives later ordinary saves,
  including recovery from the previous-settings file.
- Fixed-level mode ignores a dormant animation-pause setting.
- Unexpected controller failures are logged immediately with exception details.
- Canceling Exit while an update check and a file operation are pending restores
  the manual update button.

### Upgrading from beta.1

1. Download `ZX6DisplayControl.zip` from this release.
2. Choose **Exit** in the running app's tray menu.
3. Extract the entire new package, keeping the EXE and Core DLL together, and run
   the new EXE. Settings remain under `%LOCALAPPDATA%\ZX6DisplayControl`.
4. If **Start with Windows** is enabled and the app folder moved, select
   **Save preferences** from the new copy to update its startup registration.

Customized built-ins become personal profiles automatically. The first save
after migration preserves the old settings in
`settings.before-profile-library-v2.json`; ordinary backups still retain only
the previous valid save. Export profiles to keep lasting copies.

Automatic update checks are enabled by default, including for existing settings.
Disable **Check for updates at startup** and select **Save preferences** to opt
out. Checks contact GitHub with the app name/version and normal connection
information; they do not upload sensor readings, profiles or diagnostics.
Updates are downloaded and installed manually. Beta.1 has no update checker,
so this first upgrade must be downloaded from GitHub directly.

The executable remains unsigned. This release requires Windows with .NET Framework
4.7.2 or later, AIDA64 shared memory, and one Z-X6 holder. Other holder models and
firmware variants have not been verified.

## 0.1.0-beta.1

- Initial public beta: AIDA64 temperature sources, independent CPU/GPU bar
  animations, profiles, tray controls, diagnostics and portable packaging.
