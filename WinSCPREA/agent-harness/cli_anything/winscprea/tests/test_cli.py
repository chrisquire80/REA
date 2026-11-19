from __future__ import annotations

import json
import os
import sys
from pathlib import Path

import pytest
from click.testing import CliRunner

from cli_anything.winscprea.core import backend
from cli_anything.winscprea.core.backend import BackendResult
from cli_anything.winscprea.winscprea_cli import _split_repl, cli


def test_pretty_json_formats_objects_and_preserves_plain_text() -> None:
    assert backend.pretty_json('{"ok":true}') == '{\n  "ok": true\n}'
    assert backend.pretty_json("connected") is None


def test_backend_passes_json_and_keeps_secret_out_of_arguments(monkeypatch: pytest.MonkeyPatch) -> None:
    seen: dict[str, object] = {}
    monkeypatch.setattr(backend, "resolve_backend", lambda: ["norea-test"])

    def fake_run(command, **kwargs):
        seen["command"] = command
        seen["env"] = kwargs["env"]
        return type("Completed", (), {"returncode": 0, "stdout": "{}", "stderr": ""})()

    monkeypatch.setattr(backend.subprocess, "run", fake_run)
    result = backend.run_winscp(
        ["remote", "list"],
        json_mode=True,
        extra_env={"WINSCPREA_PASSWORD": "do-not-log-this"},
    )

    assert result == BackendResult(0, "{}", "")
    assert seen["command"] == ["norea-test", "winscp", "remote", "list", "--json"]
    assert "do-not-log-this" not in seen["command"]
    assert seen["env"]["WINSCPREA_PASSWORD"] == "do-not-log-this"


def test_help_lists_sftp_commands_and_json_mode() -> None:
    result = CliRunner().invoke(cli, ["--help"])
    assert result.exit_code == 0
    assert "site" in result.output
    assert "upload" in result.output
    assert "--json" in result.output


@pytest.mark.skipif(sys.platform != "win32", reason="La riga REPL usa il parser nativo Windows.")
def test_repl_keeps_quoted_windows_paths() -> None:
    parsed = _split_repl('upload --site test "C:\\Users\\A User\\release.zip"')
    assert parsed == ["upload", "--site", "test", "C:\\Users\\A User\\release.zip"]


def test_site_add_forwards_configuration_without_a_password(monkeypatch: pytest.MonkeyPatch) -> None:
    seen: dict[str, object] = {}

    def fake_forward(ctx, args, password_env=None):
        seen["args"] = args
        seen["password_env"] = password_env

    monkeypatch.setattr("cli_anything.winscprea.winscprea_cli._forward", fake_forward)
    result = CliRunner().invoke(
        cli,
        ["site", "add", "staging", "--host", "sftp.example.net", "--username", "deploy"],
    )

    assert result.exit_code == 0
    assert seen["args"] == [
        "site", "add", "staging", "--host", "sftp.example.net", "--username", "deploy",
        "--port", "22", "--remote-dir", "/", "--authentication", "password",
    ]
    assert "do-not-log-this" not in seen["args"]
    assert seen["password_env"] is None


def test_remote_delete_only_forwards_confirmation_when_requested(monkeypatch: pytest.MonkeyPatch) -> None:
    calls: list[list[str]] = []
    monkeypatch.setattr(
        "cli_anything.winscprea.winscprea_cli._forward",
        lambda ctx, args, password_env=None: calls.append(args),
    )
    runner = CliRunner()

    refused_by_default = runner.invoke(cli, ["remote", "delete", "--site", "staging", "--path", "/tmp/x"])
    confirmed = runner.invoke(cli, ["remote", "delete", "--site", "staging", "--path", "/tmp/x", "--yes"])

    assert refused_by_default.exit_code == 0
    assert confirmed.exit_code == 0
    assert "--yes" not in calls[0]
    assert "--yes" in calls[1]


def test_backend_nonzero_exit_code_is_propagated(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(
        "cli_anything.winscprea.winscprea_cli.run_winscp",
        lambda *args, **kwargs: BackendResult(3, '{"ok":false}', ""),
    )
    result = CliRunner().invoke(cli, ["--json", "status"])
    assert result.exit_code == 3
    assert json.loads(result.output) == {"ok": False}


def test_missing_backend_is_a_json_error_when_requested(monkeypatch: pytest.MonkeyPatch) -> None:
    def unavailable(*args, **kwargs):
        raise backend.BackendNotFoundError("backend unavailable")

    monkeypatch.setattr(
        "cli_anything.winscprea.winscprea_cli.run_winscp",
        unavailable,
    )
    result = CliRunner().invoke(cli, ["--json", "status"])
    assert result.exit_code == 2
    assert json.loads(result.output) == {
        "ok": False,
        "error": {"code": "backend_not_found", "message": "backend unavailable", "details": None},
    }


def _backend_path() -> Path | None:
    try:
        command = backend.resolve_backend()
    except backend.BackendNotFoundError:
        return None
    if command[0].lower() == "dotnet":
        return Path(command[1])
    return Path(command[0])


@pytest.mark.skipif(
    sys.platform != "win32" or not os.environ.get("NOREA_CLI_PATH"),
    reason="Imposta NOREA_CLI_PATH al backend NoreaCli aggiornato per eseguire l'integrazione locale.",
)
def test_isolated_site_profile_lifecycle(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    executable = _backend_path()
    if executable is None or executable.name.lower() not in {"noreacli.exe", "norea.exe", "noreacli.dll"}:
        pytest.skip("NoreaCli.exe pubblicato non trovato; imposta NOREA_CLI_PATH per eseguire l'integrazione locale.")
    settings = tmp_path / "settings.json"
    monkeypatch.setenv("NOREA_CLI_PATH", str(executable))
    monkeypatch.setenv("WINSCPREA_SETTINGS_PATH", str(settings))

    def run(*args: str) -> tuple[int, dict]:
        completed = backend.run_winscp(args, json_mode=True)
        return completed.returncode, json.loads(completed.stdout)

    code, added = run("site", "add", "test-site", "--host", "sftp.example.net", "--username", "test-user")
    assert code == 0 and added["data"]["action"] == "site-added"
    saved = json.loads(settings.read_text(encoding="utf-8"))
    assert saved["Sites"][0]["Name"] == "test-site"
    assert "Password" not in saved["Sites"][0]

    code, listed = run("site", "list")
    assert code == 0 and listed["data"]["count"] == 1
    assert "test-site" in json.dumps(listed)

    code, duplicate = run("site", "add", "test-site", "--host", "other.example.net", "--username", "test-user")
    assert code == 1 and duplicate["error"]["code"] == "site_exists"

    before = settings.read_bytes()
    code, dry_run = run("site", "remove", "test-site", "--dry-run")
    assert code == 0 and dry_run["data"]["dryRun"] is True
    assert settings.read_bytes() == before

    code, not_confirmed = run("site", "remove", "test-site")
    assert code == 1 and not_confirmed["error"]["code"] == "confirmation_required"
    assert settings.read_bytes() == before

    code, removed = run("site", "remove", "test-site", "--yes")
    assert code == 0 and removed["data"]["action"] == "site-removed"
    assert json.loads(settings.read_text(encoding="utf-8"))["Sites"] == []
