# PortSentinel

**A full-screen Windows TUI for scalable telemetry archives, TCP/UDP ETW metadata, network snapshots, and explainable diagnostics.**

PortSentinel 0.5.6 adds a server-side Timeline Explorer for large archived captures. Capture indexes and event timelines are read in SQLite pages instead of materializing every stored event in memory.

## 0.5.6 highlights

- paged capture index and event timeline using `COUNT(*)`, `LIMIT`, and `OFFSET`;
- dynamic page size based on terminal height;
- PageUp/PageDown and Home/End navigation;
- event-kind and protocol-family presets;
- parameterized text search across process name, addresses, ports, and diagnostic notes;
- exact sequence jump with page/index calculation;
- JSON schema v1 and Markdown export of the currently displayed SQL page;
- backward-compatible indexes for capture/sequence, capture/kind, and capture/protocol;
- the complete 0.5.5 Network Coverage panel remains available.

## Scale and query safety

Timeline filters are applied by SQLite before events are read. User text is passed through parameters, while `%`, `_`, and `\` are escaped as literal `LIKE` characters. Existing tables and records are not migrated or rewritten.

A page export intentionally contains only the visible filtered range. This prevents accidental full-materialization of very large captures.

## Existing capabilities

- TCP4/TCP6 and UDP4/UDP6 kernel ETW coverage;
- Connection Health for fail, reconnect, retransmit, and repeated-connect patterns;
- safe Windows IP Helper API snapshot fallback;
- capture profiles, archive search, selective comparison, and retention preview;
- SQLite telemetry and session history;
- Application Watch and reverse-DNS correlation;
- process tree, baselines, explainable rules, and SHA-256/Authenticode enrichment;
- GitHub Releases updater with SHA-256 verification.

## Privacy boundary

PortSentinel does not capture or store packet payloads, HTTP bodies, cookies, credentials, tokens, or decrypted TLS content.

## Start

Download `PortSentinel-0.5.6-win-x64.zip`, verify the `.sha256` file, extract it to a writable directory, and run `portsentinel.exe`.

The Russian [`README.md`](../README.md) is the primary project documentation.
