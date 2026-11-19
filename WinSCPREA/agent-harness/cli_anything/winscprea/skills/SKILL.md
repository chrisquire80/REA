---
name: cli-anything-winscprea
description: Operate WinSCPREA through its agent-ready SFTP command line for saved sites, safe remote file management, and transfers.
---

# WinSCPREA CLI

Use the `winscprea` Click CLI to operate WinSCPREA. It delegates protocol work to `norea winscp` and the same SSH.NET core used by the desktop app.

## Setup

From `WinSCPREA/agent-harness`:

```powershell
py -3 -m venv .venv
.venv\Scripts\Activate.ps1
python -m pip install -e .
```

The harness requires the NOREA CLI. Publish it from `WinSCPREA/agent-harness` with `.\publish_norea_cli.ps1`, or set `NOREA_CLI_PATH` to `NoreaCli.exe` / `NoreaCli.dll`.

## Commands

Start with `winscprea --help`. Running `winscprea` with no command opens the interactive REPL; type `help` or `exit` there.

```powershell
winscprea site list
winscprea site add staging --host sftp.example.net --username deploy --port 22
winscprea --json site list
winscprea status
winscprea remote list --site staging --path /incoming
winscprea remote mkdir --site staging --path /incoming/releases --dry-run
winscprea upload --site staging --remote-dir /incoming .\release.zip
winscprea download --site staging --remote-path /incoming/report.csv --local-dir .\downloads
winscprea remote rename --site staging --from /incoming/old.zip --to /incoming/new.zip --yes
winscprea remote delete --site staging --path /incoming/old --yes
```

## Authentication and host keys

- Never put passwords or private-key passphrases in command arguments. The wrapper accepts only environment variable names via `--password-env` and `--key-passphrase-env`; values are read by the backend from the process environment. Interactive password commands may prompt through hidden input.
- Site profiles intentionally store connection metadata and an optional private-key path, never passwords.
- On first connection, inspect the SHA-256 fingerprint reported by the CLI and retry with `--trust-host-key <fingerprint>` only after verifying it with the server owner. A changed trusted key is rejected until checked and explicitly trusted again.

## Safe operation rules

- Use `--json` for machine-readable output. Check the process exit code; errors in JSON mode use `{ "ok": false, "error": { "code", "message", "details" } }`.
- Uploads and downloads never overwrite existing files.
- Remote rename and recursive delete require `--yes`. Use `--dry-run` first for mutations.
- The remote root cannot be deleted, renamed or downloaded as a single item.
- Use `WINSCPREA_SETTINGS_PATH` to point development or tests at an isolated settings file.

## Troubleshooting

- If the backend cannot be found, set `NOREA_CLI_PATH` to the full path of `NoreaCli.exe` or `NoreaCli.dll`.
- For password authentication, set `WINSCPREA_PASSWORD` or pass a different variable name with `--password-env`. In non-interactive use, the variable must already be set.
- For encrypted SSH keys, set `WINSCPREA_KEY_PASSPHRASE` or pass a different name with `--key-passphrase-env`.
- For unknown commands and option syntax, inspect `winscprea --help` and the relevant nested help, such as `winscprea remote --help`.

