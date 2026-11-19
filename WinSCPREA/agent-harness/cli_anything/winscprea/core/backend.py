"""Subprocess adapter for the shared NoreaCli / SSH.NET SFTP backend."""

from __future__ import annotations

import json
import os
import shutil
import subprocess
from dataclasses import dataclass
from pathlib import Path
from typing import Mapping, Sequence


@dataclass(frozen=True)
class BackendResult:
    returncode: int
    stdout: str
    stderr: str


class BackendNotFoundError(RuntimeError):
    """Raised when the NOREA command line backend cannot be located."""


def resolve_backend() -> list[str]:
    """Find the installed NoreaCli executable or the app-local distribution."""
    override = os.environ.get("NOREA_CLI_PATH")
    if override:
        candidate = Path(os.path.expandvars(override)).expanduser()
        if candidate.is_file():
            if candidate.suffix.lower() == ".dll":
                return ["dotnet", str(candidate)]
            return [str(candidate)]
        raise BackendNotFoundError(f"NOREA_CLI_PATH non punta a un file: {candidate}")

    roots = [Path(__file__).resolve().parents[5] / "dist"]
    local_app_data = os.environ.get("LOCALAPPDATA")
    if local_app_data:
        roots.append(Path(local_app_data) / "Programs" / "NOREA")
    for root in roots:
        for name in ("NoreaCli.exe", "norea.exe"):
            candidate = root / name
            if candidate.is_file():
                return [str(candidate)]
    on_path = shutil.which("norea") or shutil.which("norea.exe")
    if on_path:
        return [on_path]
    raise BackendNotFoundError(
        "NoreaCli non trovato. Imposta NOREA_CLI_PATH al percorso di NoreaCli.exe "
        "oppure aggiungi norea.exe al PATH."
    )


def run_winscp(
    arguments: Sequence[str],
    *,
    json_mode: bool = False,
    extra_env: Mapping[str, str] | None = None,
) -> BackendResult:
    """Execute a winscp command without a shell and return captured output."""
    command = [*resolve_backend(), "winscp", *arguments]
    if json_mode:
        command.append("--json")
    env = os.environ.copy()
    if extra_env:
        env.update(extra_env)
    completed = subprocess.run(
        command,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        env=env,
        check=False,
    )
    return BackendResult(completed.returncode, completed.stdout, completed.stderr)


def pretty_json(text: str) -> str | None:
    """Format backend JSON for terminal display; preserve plain text otherwise."""
    try:
        value = json.loads(text)
    except (json.JSONDecodeError, TypeError):
        return None
    return json.dumps(value, ensure_ascii=False, indent=2)
