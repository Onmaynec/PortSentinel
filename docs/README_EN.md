# PortSentinel

**A full-screen Windows TUI for ETW metadata, SQLite archives, Installer Watch, and explainable network diagnostics.**

PortSentinel 0.5.7 adds a guided before/after workflow for observing network metadata while software is installed manually.

## 0.5.7 highlights

- Standard Watch: an 8-second baseline followed by a 30-second watch capture;
- Deep Watch: a 10-second baseline followed by a 60-second watch capture;
- a manual installer-start checkpoint between captures;
- automatic SQLite persistence for both captures;
- an optional process-name hint used only to prioritize candidates;
- PID-independent comparison of normalized network fingerprints;
- outbound ephemeral local ports excluded from fingerprints to reduce expected noise;
- process, endpoint, TCP/UDP, and failure-signal summaries;
- analysis of the latest archived pair;
- JSON schema v1 and Markdown reports;
- the complete 0.5.6 Timeline Explorer remains available.

## Trust model

PortSentinel does not launch an installer executable or modify the system. A process hint is a display priority, not proof of attribution.

New metadata may come from background applications, Windows services, scheduled tasks, child processes, package managers, service hosts, or a browser opened by the installer. Baseline and watch are separate bounded captures, so activity in the gap is not recorded.

## Existing capabilities

- server-side Timeline Explorer pagination and filters;
- TCP4/TCP6 and UDP4/UDP6 kernel ETW coverage;
- Connection Health diagnostics;
- safe Windows IP Helper API snapshot fallback;
- archive search, selective comparison, and retention preview;
- sessions, baselines, explainable rules, process tree, and reverse DNS;
- GitHub Releases updater with SHA-256 verification.

## Privacy boundary

PortSentinel does not capture or store packet payloads, HTTP bodies, cookies, credentials, tokens, or decrypted TLS content.

## Start

Download `PortSentinel-0.5.7-win-x64.zip`, verify its `.sha256` file, extract it to a writable directory, and run `portsentinel.exe`.

The Russian [`README.md`](../README.md) is the primary project documentation.
