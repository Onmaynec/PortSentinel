# PortSentinel

**A full-screen Windows TUI for network observation, session history, baselines, and explainable rules.**

PortSentinel 0.4.0 is a standalone self-contained `portsentinel.exe`. It reads TCP/UDP IPv4/IPv6 tables through the Windows IP Helper API, correlates processes, stores local sessions in SQLite, compares the current state with a baseline, and presents explainable findings.

## 0.4.0 highlights

- stable baseline fingerprints that do not depend on PID;
- `NewListenerRule`;
- `WildcardListenerRule`;
- `UnsignedNetworkProcessRule`;
- `TempDirectoryNetworkProcessRule`;
- severity, confidence, evidence, and limitations;
- SHA-256 enrichment for executable files;
- Authenticode certificate and publisher metadata;
- detailed rule finding cards in the TUI.

Findings describe observable facts. They are not malware verdicts.

## Existing capabilities

- live TCP/UDP monitor;
- listeners and active connections;
- process inspector and quick scan;
- SQLite session history;
- JSON and Markdown exports;
- GitHub Releases updater with SHA-256 verification;
- self-contained Windows x64 release.

## Start

Download `PortSentinel-0.4.0-win-x64.zip` from GitHub Releases, verify the `.sha256` file, extract the archive to a writable folder, and run `portsentinel.exe`.

The Russian [`README.md`](../README.md) is the primary project documentation.
