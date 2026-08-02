# PortSentinel

**A full-screen Windows TUI for TCP/UDP ETW metadata, network snapshots, local telemetry archives, and explainable diagnostics.**

PortSentinel 0.5.5 expands the read-only kernel capture backend to TCP and UDP over IPv4 and IPv6, then turns the observed metadata into live or archived coverage reports.

## 0.5.5 highlights

- TCP IPv6 connect, accept, disconnect, retransmit, and reconnect callbacks;
- UDP IPv4/IPv6 send and receive callbacks;
- normalized `TCP4`, `TCP6`, `UDP4`, and `UDP6` protocol families;
- a 15-second Coverage Capture workflow with automatic SQLite persistence;
- latest-capture and selected-archive coverage analysis;
- protocol matrix with event, process, endpoint, send, and receive counts;
- IPv4/IPv6 and TCP/UDP distribution;
- top remote endpoints;
- JSON schema v1 and Markdown coverage reports;
- corrected ETW port handling by removing a redundant byte swap;
- the complete 0.5.4 Connection Health panel remains available.

## Coverage limitations

Coverage only describes events observed during the bounded capture window. A missing protocol family does not prove that no matching traffic occurred. Kernel UDP callbacks can omit the source port; unavailable ports are stored as `0` and reported as a limitation.

## Privacy boundary

PortSentinel does not capture or store packet payloads, HTTP bodies, cookies, credentials, tokens, or decrypted TLS content. Capture results are capped at 5000 normalized events.

## Existing capabilities

- Connection Health for fail, reconnect, retransmit, and repeated-connect patterns;
- safe Windows IP Helper API snapshot fallback;
- capture profiles, archive search, selective comparison, and retention preview;
- SQLite telemetry and session history;
- Application Watch and reverse-DNS correlation;
- process tree, baselines, explainable rules, and SHA-256/Authenticode enrichment;
- GitHub Releases updater with SHA-256 verification.

## Start

Download `PortSentinel-0.5.5-win-x64.zip`, verify the `.sha256` file, extract it to a writable directory, and run `portsentinel.exe`.

The Russian [`README.md`](../README.md) is the primary project documentation.
