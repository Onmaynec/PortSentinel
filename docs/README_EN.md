# PortSentinel

**A full-screen Windows TUI for network snapshots, ETW event metadata, local telemetry history, and explainable analysis.**

PortSentinel 0.5.2 is a standalone self-contained `portsentinel.exe`. It archives ETW or snapshot-fallback captures in SQLite, lets the user browse stored event metadata, and compares the two latest captures using a PID-independent lifecycle fingerprint.

## 0.5.2 highlights

- automatic SQLite persistence for ETW and fallback captures;
- telemetry history with backend status and lifecycle counters;
- stored event browsing;
- JSON schema v1 and Markdown archive exports;
- comparison of the two latest captures;
- added-event and missing-fingerprint views;
- JSON and Markdown telemetry-diff exports;
- the complete 0.5.1 ETW Control Center remains available.

## Storage and compatibility

Version 0.5.2 adds `telemetry_captures` and `telemetry_events` to the existing `%LocalAppData%\PortSentinel\portsentinel.db`. Tables are created with `CREATE TABLE IF NOT EXISTS`; existing sessions, baselines, and reports remain unchanged.

The lifecycle fingerprint includes event kind, protocol, endpoints, and process name, but excludes PID. It is diagnostic metadata and not a malware verdict.

## Privacy boundary

PortSentinel does not capture or store packet payloads, HTTP bodies, cookies, credentials, tokens, or decrypted TLS content. Archived data contains timestamps, event kinds, process metadata, endpoints, backend status, and explicit limitations.

## Existing capabilities

- read-only kernel ETW TCP lifecycle capture with safe snapshot fallback;
- Application Watch and reconnect-loop indicators;
- bounded reverse-DNS correlation;
- native Windows network process tree;
- SQLite session history, baselines, and comparisons;
- explainable rules with Authenticode and SHA-256 enrichment;
- GitHub Releases updater with SHA-256 verification.

## Start

Download `PortSentinel-0.5.2-win-x64.zip` from GitHub Releases, verify the `.sha256` file, extract the archive to a writable folder, and run `portsentinel.exe`.

The Russian [`README.md`](../README.md) is the primary project documentation.
