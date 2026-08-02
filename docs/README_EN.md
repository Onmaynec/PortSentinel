# PortSentinel

**A full-screen Windows TUI for ETW metadata, safe session control, SQLite archives, and explainable network diagnostics.**

PortSentinel 0.5.8 adds an ETW Session Guard that inventories active logger session names, separates PortSentinel-owned sessions from foreign sessions, records guarded-capture diagnostics, and provides explicitly confirmed cleanup for orphaned `PortSentinel-*` sessions only.

## 0.5.8 highlights

- active ETW session-name inventory;
- ownership boundary between `PortSentinel-*` and foreign sessions;
- a 15-second guarded capture with automatic SQLite persistence;
- attempt, backend, fallback, session-count, and native-code diagnostics;
- best-effort classification for access denied, name collision, resource limit, and unavailable-session failures;
- one bounded retry only for a likely name collision;
- JSON schema v1 and Markdown inventory/diagnostic reports;
- dry-run cleanup that requires `Y` confirmation;
- foreign ETW sessions are never stopped, restarted, or modified;
- the complete 0.5.7 Installer Watch remains available.

## Safety boundary

Cleanup applies an ownership filter before attach/stop and accepts only names beginning with `PortSentinel-`. Other PortSentinel instances should be closed first, because an active capture from another instance uses the same prefix.

Kernel ETW control generally requires elevation. If the kernel session cannot be controlled, PortSentinel uses its Windows IP Helper API snapshot fallback. Failure classification is diagnostic and does not replace Windows Event Log or vendor-specific troubleshooting.

## Existing capabilities

- Installer Watch before/after reports;
- server-side Timeline Explorer pagination and filters;
- TCP4/TCP6 and UDP4/UDP6 kernel ETW coverage;
- Connection Health diagnostics;
- archive search, selective comparison, and retention preview;
- sessions, baselines, explainable rules, process tree, and reverse DNS;
- GitHub Releases updater with SHA-256 verification.

## Privacy boundary

PortSentinel does not capture or store packet payloads, HTTP bodies, cookies, credentials, tokens, or decrypted TLS content.

## Start

Download `PortSentinel-0.5.8-win-x64.zip`, verify its `.sha256` file, extract it to a writable directory, and run `portsentinel.exe` as administrator for kernel ETW capture.

The Russian [`README.md`](../README.md) is the primary project documentation.
