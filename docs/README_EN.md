# PortSentinel

**A full-screen Windows TUI for network snapshots, ETW event metadata, session history, and explainable analysis.**

PortSentinel 0.5.1 is a standalone self-contained `portsentinel.exe`. It adds an optional read-only kernel ETW backend for TCP lifecycle events while preserving the Windows IP Helper API snapshot backend as a safe fallback.

## 0.5.1 highlights

- kernel TCP IPv4 connect, accept, disconnect, and retransmit events;
- a bounded 12-second capture window;
- ETW capability and elevation status;
- automatic snapshot fallback when ETW cannot be controlled;
- JSON schema v1 and Markdown capture exports;
- event cards with process and endpoint metadata;
- the complete 0.5.0 Application Watch, DNS, process tree, and session comparison panel remains available.

Controlling a kernel ETW session normally requires elevated access. PortSentinel does not modify access groups or system tracing settings.

## Privacy boundary

PortSentinel does not capture packet payloads, HTTP bodies, cookies, credentials, tokens, or decrypted TLS content. ETW events and fallback snapshots are diagnostic metadata, not malware verdicts.

## Existing capabilities

- Application Watch and reconnect-loop indicators;
- bounded reverse-DNS correlation;
- native Windows network process tree;
- SQLite session history, baselines, and comparisons;
- explainable rules with Authenticode and SHA-256 enrichment;
- GitHub Releases updater with SHA-256 verification.

## Start

Download `PortSentinel-0.5.1-win-x64.zip` from GitHub Releases, verify the `.sha256` file, extract the archive to a writable folder, and run `portsentinel.exe`.

The Russian [`README.md`](../README.md) is the primary project documentation.
