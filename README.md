<p align="center"><img src="assets/pulse-logo.png" alt="Pulse" width="180"></p>

# Pulse 1.1.0-beta.1

[Türkçe](README.tr.md)

A compact Windows 10/11 laptop battery widget, written in C# and WPF for .NET Framework 4.8. Portable, offline, with no NuGet packages or AI model.

![Pulse with sample data](preview-en.png)

## Start and update

Exit the old Pulse from its tray menu, extract the Windows ZIP and launch `Pulse.exe`. Copying a new executable does not update another running or desktop copy. Windows may show an unknown-publisher warning because this beta is unsigned.

The profile stays in `%LOCALAPPDATA%\Pulse\profile-v1.xml`. The filename is kept for compatibility, but its internal format is now version 2. On first migration, Pulse saves `profile-v1.xml.v1.bak`. Older versions cannot read the new profile format. To roll back, exit Pulse and restore that backup manually. Do not distribute your profile or battery reports with the application.

Native battery identity is more specific than the earlier WMI identity. An identity change starts a separate model; the old archive is retained instead of silently mixing potentially different batteries. You can reanalyze Windows history to seed the new model.

## What it shows

Battery percentage, current battery discharge or net charging power (average of readings in the last 5 seconds), a five-minute graph, approximate remaining time, current/full/design capacity, voltage and health when the hardware supplies them. Battery power is not wall-socket power. Firmware may refresh more slowly than Pulse polls.

The language follows the Windows display language: Turkish selects Turkish; other languages select English. Restart after changing the display language. Optional overrides: `--lang=tr` and `--lang=en`.

Drag the title or the empty top edge above it to move the widget, use ◇/◆ for always-on-top, − for the tray, and × to exit. Double-click the tray icon to restore it. The tray also has standby-loss and reset-learning controls. Reset clears the current battery's active archive; separate older-battery archives and migration backups remain available on disk.

## Learning rules

- **Typical usage:** only active, unplugged discharge contributes. Plugged-in discharge, including battery assistance under heavy load, never contributes.
- **Real energy:** elapsed seconds weight the measured watts. Real short bursts remain in the energy total; a separate 30-second median and time-based smoothing stabilize the displayed estimate. A sustained workload still changes the live estimate.
- **Unusual load:** after a baseline exists, unusually high minutes are stored separately. One long gaming outage cannot replace normal use. High use recurring on at least five of the last seven sufficiently observed days may become the new normal. The app cannot identify a game or infer why power was lost, and a cold start containing only gaming cannot establish a separate everyday baseline.
- **Missing watts:** a 1–5 minute capacity trend can supply an approximate rate once movement exceeds the battery's reporting granularity. Zero/flat readings, corrections and gaps do not manufacture energy.
- **Sleep and screen off:** never train the model. This also conservatively excludes screen-off downloads. Display dimming alone does not reset learning. Suspend, source changes and in-flight readings across transitions are isolated.
- **Standby loss:** optional approximate net capacity loss is stored separately. No wake requests or background service. Windows may suspend the app entirely, so continuous standby observation is not promised. An observed charger connection invalidates a standby-loss interval.

Typical use needs at least 20 minutes of eligible observations, or an imported Windows prior. Days have capped influence and newer days receive more weight. Live remaining time follows current use separately. Old data is retained, but only the latest 180 archived days are loaded into the working model.

## Charging and 99% / charge limits

Charging observations train only the charging model, in 5-percentage-point bands. A band needs at least three observed charging sessions and three minutes of coverage before it is used as a learned curve. More minutes in one session do not pretend to be independent cycles. Current net charge power adjusts the estimate as computer load changes. Complete, uninterrupted band traversals are timed separately; after three traversals their observed capacity gain and duration refine the band curve.

If Windows reports no charging, Pulse shows **Plugged in · not charging** without a countdown. A reported zero charging rate with stable capacity for two minutes also pauses the countdown and charging learning. The waiting period does not become a very slow final charging segment. An explicit Windows 100% report while no longer charging allows **Charging complete**. Pulse never rewrites a reported 99% as 100%, and never guesses that an unexplained pause is a confirmed 80% charging limit.

Manufacturer charging limits are not queried by a universal API here. Charger-specific curves and full-cycle ETA calibration remain follow-up work. The curve learns net watts and observed full-band durations, not a promised completion time under future workload changes.

## Windows battery history

**Analyze my battery history** creates a fresh temporary `powercfg /batteryreport /xml /duration 14` report each time; it does not reuse a batteryhealth file. It removes the temporary report after parsing. Nothing is uploaded and the battery is not stress-tested.

Only recent active, unplugged intervals are eligible. Sleep/Connected Standby, AC intervals, invalid capacity changes, duplicates and overlaps are excluded. Adjacent fragments are combined before filtering, avoiding extra votes for one fragmented session. At least three retained sessions, 60 minutes and two days are required. A median/MAD filter removes unusual sessions, with capped duration weighting.

The import is a cold-start prior, replaced by sufficient live history rather than added as a second vote over overlapping observations. Reanalysis replaces the previous import. Available detail depends on what Windows recorded; requesting 14 days does not guarantee 14 complete days of data. The imported prior expires after 30 days; **Pulse's own archived observations do not**.

## Storage and performance

- Recent 48-hour observations are kept in memory and appended to daily text journals. Whole-day journal cleanup may leave a little extra raw data on disk until the day is fully outside that window.
- Daily duration/energy totals for normal use, high load, charge and standby are retained indefinitely in `profile-v1.xml.days\<battery hash>\`.
- Only changed daily summaries and the small active model are atomically replaced; recent history is not rewritten wholesale. Writes are normally batched every two minutes and on exit. An abrupt shutdown can lose the unsaved interval.
- A damaged final journal record is ignored. Independent daily summaries prevent journal replay from double counting energy.
- Old v1 minute medians are preserved as approximate legacy summaries; their missing raw energy cannot be reconstructed.
- Native Windows battery IOCTLs provide capability/unit checks, battery identity and cached metadata. Relative-unit batteries never display invented W/Wh. WMI/basic status is a limited fallback when native data is unavailable. If a unique identity is unavailable, device path/tag/design capacity conservatively separate profiles.
- Visible native readings poll every second; tray/fallback readings every five seconds. Hidden windows do not redraw the graph. Standby reads are at most once per minute when allowed.

A typical daily summary in testing was about 0.5 KB (filesystem allocation may be larger), roughly 0.2 MB/year in file contents for one battery, plus recent journals and the model. WPF/.NET memory is much larger than the executable; see [validation notes](VALIDATION.md) for a short measured sample rather than an unsupported memory claim.

## Estimates and beta limitations

The range is approximate, not a statistical confidence guarantee. It starts broad, widens for variable use, and can widen from observed five-minute capacity prediction errors. Windows-reported rate and capacity share the same hardware source. Workload changes are excluded from steady-workload error calibration. End-to-end charge/discharge completion accuracy is not yet field-calibrated.

Automated scenarios and real native reads have been checked on one laptop. Real lid-close/resume, charger transitions, charge ceilings, multiple-battery hardware and multiple laptop brands still need field testing. This is a beta, not a claim of cross-device release certification.

## Build and check

Run `powershell -ExecutionPolicy Bypass -File .\build.ps1` on Windows with .NET Framework 4.8 installed. No downloads are performed by the build.

Checks: `Pulse.exe --learning-test`, `--history-test`, `--standby-test`, `--self-test`, `--language-test`, `--powerwatch-test`. Wait for each process and inspect its exit code; result files are created beside the executable. `--preview --lang=tr` or `--preview --lang=en` render sample images. `--preview --idle` renders the 99% idle case. `--probe` writes local battery readings; do not publish that file as part of the release. `--smoke-test` performs a 70-second offscreen/hidden run using an isolated `smoke-work` profile and writes process measurements; it does not touch the user's real profile.

No license is selected yet. A public repository by itself does not grant an open-source license.

The large Current Consumption value updates every five seconds with the mean of readings in the last five seconds, remaining fixed between updates. It initially shows the first available reading. The smaller 1 min avg. shows the mean of readings in the last minute; during startup it uses available readings. Charging power follows the same display rules. Visible native sampling and learning still run every second, and the graph receives raw readings.
