# PortSentinel

**A full-screen Windows TUI for ETW metadata, network snapshots, local telemetry archives, and explainable connection diagnostics.**

PortSentinel 0.5.4 adds kernel TCP fail/reconnect events and a Connection Health analyzer for live or archived captures.

## 0.5.4 highlights

- kernel `TcpIpFail` and `TcpIpReconnect` callbacks through TraceEvent;
- numeric failure code and protocol preserved as evidence without speculative decoding;
- a 15-second Capture & Health workflow with automatic SQLite persistence;
- analysis of the latest or any selected archived capture;
- explainable findings for kernel failures, retransmit bursts, reconnect loops, and rapid repeated connects;
- explicit capture-window and snapshot-fallback limitations;
- a 0–100 health score with Stable, Observe, Degraded, and Critical grades;
- JSON schema v1 and Markdown health reports;
- the complete 0.5.3 Archive Operations panel remains available.

## Trust model

Every finding contains severity, confidence, evidence, and a limitation. PortSentinel preserves numeric kernel failure codes but does not claim undocumented meanings. Retransmits and reconnects can have normal causes such as Wi-Fi loss, congestion, roaming, proxies, or application retry logic.

The health score is a diagnostic summary, not a malware, ownership, or security verdict.

## Privacy boundary

PortSentinel does not capture or store packet payloads, HTTP bodies, cookies, credentials, tokens, or decrypted TLS content.

## Existing capabilities

- read-only kernel ETW with safe snapshot fallback;
- capture profiles, archive search, selective comparison, and retention preview;
- SQLite telemetry and session history;
- Application Watch and reverse-DNS correlation;
- process tree, baselines, explainable rules, and SHA-256/Authenticode enrichment;
- GitHub Releases updater with SHA-256 verification.

## Start

Download `PortSentinel-0.5.4-win-x64.zip`, verify the `.sha256` file, extract it to a writable directory, and run `portsentinel.exe`.

The Russian [`README.md`](../README.md) is the primary project documentation.
