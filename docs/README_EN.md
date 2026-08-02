# PortSentinel

**A full-screen Windows TUI for network observation, session history, explainable rules, and local telemetry.**

PortSentinel 0.5.0 is a standalone self-contained `portsentinel.exe`. It reads TCP/UDP IPv4/IPv6 tables through the Windows IP Helper API, correlates processes, stores local sessions in SQLite, and adds application timelines without capturing network payloads.

## 0.5.0 highlights

- Application Watch for a selected network-active process;
- first seen, last seen, observation count, and connection cycles;
- reconnect-loop detection;
- automatic JSON and Markdown watch reports;
- bounded reverse-DNS correlation with timeout and cache;
- native Windows process tree through Toolhelp32;
- stable comparison of the two latest stored sessions;
- JSON and Markdown session-diff exports;
- the complete 0.4.0 sessions, baselines, rules, and network tools panel remains available.

DNS names and reconnect-loop indicators are diagnostic metadata, not malware verdicts. The snapshot backend does not collect HTTP bodies, cookies, credentials, or decrypted TLS content.

## Existing capabilities

- live TCP/UDP monitor;
- listeners and active connections;
- process inspector and quick scan;
- SQLite session history and baselines;
- explainable rules with Authenticode and SHA-256 enrichment;
- GitHub Releases updater with SHA-256 verification;
- self-contained Windows x64 release.

## Start

Download `PortSentinel-0.5.0-win-x64.zip` from GitHub Releases, verify the `.sha256` file, extract the archive to a writable folder, and run `portsentinel.exe`.

The Russian [`README.md`](../README.md) is the primary project documentation.
