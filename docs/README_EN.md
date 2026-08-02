# PortSentinel

**A full-screen Windows TUI for ETW event metadata, network snapshots, local telemetry archives, and explainable analysis.**

PortSentinel 0.5.3 is a standalone self-contained `portsentinel.exe`. It adds bounded capture profiles, parameterized archive search, selective capture comparison, and retention controls with a mandatory dry-run preview.

## 0.5.3 highlights

- capture profiles for 5, 15, 30, and 60 seconds;
- automatic SQLite persistence for every profile capture;
- archive search by process name, local/remote IP address, and diagnostic note;
- presets for retransmit, disconnect, snapshot-fallback, and listener events;
- comparison of any pair from the latest 50 captures;
- archive statistics including capture/event counts, date range, and database size;
- retention policies that keep the latest 25, 50, 100, or 250 captures;
- a mandatory preview and explicit `Y` confirmation before deletion;
- the complete 0.5.2 Telemetry Archive remains available.

## Retention safety

Retention deletes only old rows from `telemetry_captures`; related `telemetry_events` are removed by a foreign-key cascade inside a transaction. Existing sessions, baselines, and report files are untouched.

## Search and comparison

Search uses parameterized SQLite queries. Selective comparison uses the existing PID-independent lifecycle fingerprint based on event kind, protocol, endpoints, and process name. The result is diagnostic metadata, not a malware verdict.

## Privacy boundary

PortSentinel does not capture or store packet payloads, HTTP bodies, cookies, credentials, tokens, or decrypted TLS content.

## Existing capabilities

- read-only kernel ETW TCP lifecycle capture with safe snapshot fallback;
- SQLite telemetry history and exports;
- Application Watch and reconnect-loop indicators;
- bounded reverse-DNS correlation;
- native Windows network process tree;
- session history, baselines, and explainable rules;
- GitHub Releases updater with SHA-256 verification.

## Start

Download `PortSentinel-0.5.3-win-x64.zip` from GitHub Releases, verify the `.sha256` file, extract the archive to a writable folder, and run `portsentinel.exe`.

The Russian [`README.md`](../README.md) is the primary project documentation.
