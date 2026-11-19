# WinSCPREA CLI-Anything Test Plan

This plan separates the harness from a live SFTP server. Core behavior is exercised with isolated settings and a fake backend; the .NET backend is checked with local site-management commands. Remote transfer cases require a deliberately provisioned test server and are not run against personal or production sites.

## Unit and interface tests

1. `pretty_json` formats valid JSON and leaves non-JSON text untouched.
2. The backend runs without a shell, appends the `winscp` command and optional `--json`, and merges environment overrides without putting secret values in arguments.
3. Click help exposes the expected commands and documents JSON mode.
4. Site list, profile add, and profile removal forward the expected argument arrays; `--yes` is forwarded only when supplied.
5. Non-zero backend exit codes propagate to the shell.

## Local .NET integration checks

1. Point `WINSCPREA_SETTINGS_PATH` at a temporary JSON file.
2. Run `norea winscp site add` and `site list --json` through a fresh `NOREA_CLI_PATH` process and confirm the profile round-trips without credentials.
3. Confirm duplicate site names are rejected unless `--replace` is supplied, and removal without `--yes` does not change the file.
4. Confirm `--dry-run` leaves the isolated settings file unchanged.

## Live SFTP checks

With a disposable test server only: verify first-contact fingerprint rejection and explicit trust, listing, mkdir, rename, upload/download, no-overwrite behavior, recursive delete confirmation, and rejection of the remote root. These checks are skipped when no test endpoint is configured.
