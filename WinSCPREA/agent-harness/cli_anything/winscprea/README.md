# WinSCPREA CLI-Anything Harness

`winscprea` is a Python Click interface for the WinSCPREA SFTP client. It follows the CLI-Anything harness layout and delegates every connection and transfer to the same C# `SftpSessionService` used by the WinForms app through `NoreaCli`; it does not implement a second SFTP stack.

## Requirements

- Windows x64 with .NET 10 runtime and the NOREA CLI (`norea.exe` / `NoreaCli.exe`)
- Python 3.10 or later
- Python dependencies installed by `setup.py`

## Install for development

From this directory:

```powershell
py -3 -m venv .venv
.venv\Scripts\Activate.ps1
python -m pip install -e .
```

Build or update the backend from this directory with `..\..\publish_norea_cli.ps1`. Set `NOREA_CLI_PATH` if the NOREA executable is not on `PATH` and is not in the repository `dist` or `%LOCALAPPDATA%\Programs\NOREA` directory:

```powershell
$env:NOREA_CLI_PATH = "C:\path\to\NoreaCli.exe"
```

## Examples

```powershell
winscprea --help
winscprea site list
winscprea site add staging --host sftp.example.net --username deploy --port 22
winscprea status
winscprea --json site list
winscprea remote list --site staging --path /incoming
winscprea upload --site staging --remote-dir /incoming .\release.zip
winscprea download --site staging --remote-path /incoming/report.csv --local-dir .\downloads
```

Passwords and private-key passphrases never appear in command arguments or site profiles. Set `WINSCPREA_PASSWORD` or `WINSCPREA_KEY_PASSPHRASE` in the process environment. In interactive use, the CLI can prompt for a missing password with a hidden input. For scripts, use `--password-env` / `--key-passphrase-env` to name an environment variable. Key trust is explicit: inspect the reported SHA-256 fingerprint, then retry with `--trust-host-key <fingerprint>`. Changed host keys are rejected until the fingerprint is supplied again.

Remote delete and rename require `--yes`; mutating commands support `--dry-run`. Existing files are never overwritten. Folder deletion is recursive, and deleting or downloading the remote root is refused.

Running `winscprea` without a command starts the CLI-Anything interactive REPL. Use `help` or `exit` there.

## Development checks

See `tests/TEST.md` for the test plan. Run Python checks with `python -m pytest tests`. Build the shared backend from the repository root with `dotnet publish NoreaCli/NoreaCli.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o dist`.
