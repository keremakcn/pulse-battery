# Validation / Doğrulama — 1.1.0-beta.1

Date / Tarih: 2026-10-01. Windows laptop, .NET Framework, release compiler settings. This records observed checks, not a guarantee for other hardware.

## Automated results

| Check | Result |
|---|---|
| Learning and storage scenarios | 72 passed |
| Battery-report integration | 17 passed |
| Standby and source isolation | 21 passed |
| Basic energy/time calculations | Passed |
| Turkish, English, fallback language and formatting | Passed |
| Native display/suspend notification registration | Registered; display state available |
| Native battery read on this laptop | Passed; physical watts and Wh available |
| Turkish/English normal and 99% idle previews | Rendered and visually inspected |
| Build | Passed |

110 named behavioral checks cover cadence invariance (1/5/10/15 seconds), real transient energy, persistent load, AC discharge exclusion including error calibration, sleep gaps, capacity fallback and gauge correction, rare/repeated high loads, independent charge sessions, complete charge-band timing, 99% idle, source events, 90-day retention, incremental journals, truncated records, migration/backups, multiple/relative-unit battery decoding and duration-weighted graphs. Data for these scenarios is synthetic. Test profiles use isolated temporary directories.

The final executable's SHA-256 is:

`B2F1D193875C52AD85BFF97A4F25717B3F2DEDA7F7CBFEAA641220E8F0B957AC`

## Short real-process sample

The algorithm build before the branding-only update ran with an isolated profile for 70 seconds: 10-second warmup, 30 seconds with a visible WPF window placed offscreen, then 30 seconds hidden in the tray. Actual native battery reads were used. No real user profile was changed.

| Mode | Wall time | CPU, one-core equivalent | Working set | Private bytes | Reads | Paints |
|---|---:|---:|---:|---:|---:|---:|
| Visible, offscreen | 30.02 s | 0.781% | 116.6 MiB | 94.1 MiB | 30 | 30 |
| Tray | 30.02 s | 0.208% | 117.3 MiB | 93.7 MiB | 5 | 0 |

CPU is process CPU seconds / elapsed seconds, multiplied by 100; it is not the Task Manager percentage normalized over all cores. WPF memory greatly exceeds executable size. These short samples do not establish battery overhead, long-term memory behavior, on-screen GPU cost, or performance on other computers. Working set and private bytes describe different things and are not additive.

A representative persisted daily summary was approximately 0.5 KB of file content. Recent journals and small model snapshots occupy additional space; filesystem allocation is larger than file content for small files.

## Field checks still required

- Real lid-close/resume and Modern Standby, including an AC transition during standby.
- Multiple physical laptop brands and multiple-battery machines.
- Real charger changes and manufacturer 80/99% charging limits.
- Complete charge/discharge cycles to compare predicted ranges with actual completion.
- Long-running resource measurements on battery and AC.

The implemented charging model combines net power, independent sessions and measured complete-band durations. It does not identify individual chargers or query every manufacturer's charge ceiling. Five-minute prediction errors can widen an approximate range; this is not a calibrated statistical confidence interval or proof of full-cycle ETA accuracy.

## Türkçe kısa açıklama

110 davranış kontrolü, temel hesaplar, dil kontrolleri ve gerçek Windows bildirim/pil okuması geçti. Normal görünüm ve %99 bekleme görünümü Türkçe/İngilizce incelendi. Tepside ekran çizimi yapılmadığı ve okumanın 5 saniyeye indiği gerçek işlemde doğrulandı.

Yukarıdaki kaynak ölçümü kısadır; farklı cihazlarda aynı sonucu veya pil tüketimine belirli bir etkiyi garanti etmez. Gerçek kapak/uyku, adaptör, şarj sınırı ve tam pil döngüsü kontrolleri henüz tamamlanmadığı için sürüm beta olarak işaretlendi.

## Branding update — 2026-10-02

Executable icon extraction, embedded seven-size ICO, 256px WPF window icon and the actual NotifyIcon assignment were checked. The logo was added to both README files. Battery-learning code is unchanged.

## Five-second power display — 2026-10-02

Checked rolling-window expiration, per-reading updates, unchanged raw graph values, charge-mode reset, missing/zero readings and sleep gaps. Build, existing basic checks and language checks passed. Turkish preview inspected. Learning.cs is byte-identical to the previous desktop version.

## Display cadence follow-up — 2026-10-02

Verified that the displayed watt value remains fixed between five-second boundaries, updates with the next window mean, and that the one-minute average expires older samples. Charge transitions, missing/zero power, sleep gaps and raw sample retention passed. Existing 72 learning checks, basic checks and language checks passed again. Turkish preview inspected; learning code remains unchanged.

## Top-edge dragging — 2026-10-02

Build and Turkish preview rendering passed. Drag handling now covers the window area above the title row's lower edge and excludes button ancestors. Battery calculations and assets were not changed.
