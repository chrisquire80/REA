# WinSCPREA CLI Harness Architecture

## Source of truth

The SFTP protocol, host-key verification, site settings and transfer safety rules live in `WinSCPREA.Core`. Both the WinForms application and `NoreaCli` reference this project. The Python CLI-Anything package is a thin Click adapter around `norea winscp`; it must not introduce a second SFTP library or persist credentials.

## User and agent entry points

- `winscprea` is the Python entry point for humans and agents.
- `norea winscp` is the stable machine-facing backend and emits JSON with `--json`.
- `WINSCPREA_SETTINGS_PATH` selects an isolated settings file for development and tests.
- `NOREA_CLI_PATH` points the adapter to a particular `NoreaCli.exe` or `NoreaCli.dll`.

## Safety rules

- Passwords and key passphrases are supplied only through environment variables; they are never command arguments or profile fields.
- New or changed server keys require the user to inspect and pass the exact SHA-256 fingerprint.
- Existing remote or local files are not overwritten.
- Recursive remote delete and remote rename require `--yes`; mutating commands support `--dry-run`.
- The remote root cannot be renamed, deleted or downloaded as a single item.

## Verification scope

Python tests cover Click command forwarding, output formatting and process invocation. Local .NET integration tests use a temporary settings file. Live transfer tests must use a disposable SFTP server and are documented separately in `cli_anything/winscprea/tests/TEST.md`.
