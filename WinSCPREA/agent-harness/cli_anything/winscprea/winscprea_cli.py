"""Click command line for WinSCPREA's agent-facing SFTP operations."""

from __future__ import annotations

import getpass
import json
import os
import shlex
import sys
from pathlib import Path

import click

from cli_anything.winscprea import __version__
from cli_anything.winscprea.core.backend import (
    BackendNotFoundError,
    pretty_json,
    run_winscp,
)
from cli_anything.winscprea.utils.repl_skin import ReplSkin


history_file = Path(os.environ.get(
    "WINSCPREA_HISTORY_FILE",
    str(Path.home() / ".cli-anything-winscprea" / "history"),
))
skin = ReplSkin("winscprea", version=__version__, history_file=str(history_file))


def _forward(ctx: click.Context, arguments: list[str], password_env: str | None = None) -> None:
    json_mode = bool(ctx.obj.get("json", False))
    extra_env: dict[str, str] = {}
    if password_env and not os.environ.get(password_env) and sys.stdin.isatty():
        extra_env[password_env] = getpass.getpass("Password SFTP (non verrà salvata): ")
    try:
        result = run_winscp(arguments, json_mode=json_mode, extra_env=extra_env)
    except BackendNotFoundError as exc:
        if json_mode:
            click.echo(json.dumps({
                "ok": False,
                "error": {"code": "backend_not_found", "message": str(exc), "details": None},
            }, ensure_ascii=False))
            raise click.exceptions.Exit(2) from exc
        raise click.ClickException(str(exc)) from exc

    stdout = result.stdout.rstrip()
    stderr = result.stderr.rstrip()
    if stdout:
        if json_mode:
            click.echo(stdout)
        else:
            click.echo(pretty_json(stdout) or stdout)
    if stderr:
        click.echo(stderr, err=True)
    if result.returncode:
        raise click.exceptions.Exit(result.returncode)


@click.group(invoke_without_command=True, no_args_is_help=False)
@click.option("--json", "json_mode", is_flag=True, help="Emette JSON stabile, adatto agli agenti.")
@click.version_option(__version__, prog_name="winscprea")
@click.pass_context
def cli(ctx: click.Context, json_mode: bool) -> None:
    """Gestisci siti e trasferimenti SFTP tramite WinSCPREA e SSH.NET."""
    ctx.ensure_object(dict)
    ctx.obj["json"] = json_mode or bool(ctx.obj.get("json", False))
    if ctx.invoked_subcommand is None:
        _repl(ctx.obj["json"])


@cli.group("site")
def site_group() -> None:
    """Gestisci i profili dei siti SFTP salvati localmente."""


@site_group.command("list")
@click.pass_context
def site_list(ctx: click.Context) -> None:
    """Elenca i siti senza mostrare password o chiavi private."""
    _forward(ctx, ["site", "list"])


@site_group.command("add")
@click.argument("name")
@click.option("--host", required=True, help="Nome host o indirizzo IP del server SFTP.")
@click.option("--username", required=True, help="Utente remoto.")
@click.option("--port", type=click.IntRange(1, 65535), default=22, show_default=True)
@click.option("--remote-dir", default="/", show_default=True, help="Cartella remota iniziale.")
@click.option("--authentication", type=click.Choice(["password", "private-key"]), default="password", show_default=True)
@click.option("--private-key", type=click.Path(path_type=Path, exists=True, dir_okay=False), help="Percorso della chiave privata SSH.")
@click.option("--replace", is_flag=True, help="Sostituisce un profilo con lo stesso nome.")
@click.option("--dry-run", is_flag=True, help="Mostra l'operazione senza modificare il profilo.")
@click.pass_context
def site_add(ctx: click.Context, name: str, host: str, username: str, port: int, remote_dir: str,
             authentication: str, private_key: Path | None, replace: bool, dry_run: bool) -> None:
    """Aggiunge o aggiorna un profilo; credenziali escluse dal file impostazioni."""
    args = ["site", "add", name, "--host", host, "--username", username, "--port", str(port),
            "--remote-dir", remote_dir, "--authentication", authentication]
    if private_key:
        args.extend(["--private-key", str(private_key)])
    if replace:
        args.append("--replace")
    if dry_run:
        args.append("--dry-run")
    _forward(ctx, args)


@site_group.command("remove")
@click.argument("name")
@click.option("--yes", is_flag=True, help="Conferma la rimozione del profilo.")
@click.option("--dry-run", is_flag=True, help="Mostra l'operazione senza modificare il profilo.")
@click.pass_context
def site_remove(ctx: click.Context, name: str, yes: bool, dry_run: bool) -> None:
    """Rimuove un profilo locale; richiede --yes per confermare."""
    args = ["site", "remove", name]
    if yes:
        args.append("--yes")
    if dry_run:
        args.append("--dry-run")
    _forward(ctx, args)


@cli.command("status")
@click.pass_context
def status(ctx: click.Context) -> None:
    """Mostra backend, percorsi impostazioni e conteggio dei siti."""
    _forward(ctx, ["status"])


@cli.group("remote")
def remote_group() -> None:
    """Elenca e gestisci elementi sul server SFTP."""


@remote_group.command("list")
@click.option("--site", "site_name", required=True)
@click.option("--path", default=None, help="Cartella remota da elencare.")
@click.option("--trust-host-key", default=None)
@click.option("--password-env", default="WINSCPREA_PASSWORD", show_default=True)
@click.option("--key-passphrase-env", default="WINSCPREA_KEY_PASSPHRASE", show_default=True)
@click.pass_context
def remote_list(ctx: click.Context, site_name: str, path: str | None, trust_host_key: str | None,
                password_env: str, key_passphrase_env: str) -> None:
    """Elenca una cartella remota."""
    args = ["remote", "list", "--site", site_name]
    if path:
        args.extend(["--path", path])
    _add_connection_args(args, trust_host_key, password_env, key_passphrase_env)
    _forward(ctx, args, password_env)


@remote_group.command("mkdir")
@click.option("--site", "site_name", required=True)
@click.option("--path", required=True)
@click.option("--trust-host-key", default=None)
@click.option("--password-env", default="WINSCPREA_PASSWORD", show_default=True)
@click.option("--key-passphrase-env", default="WINSCPREA_KEY_PASSPHRASE", show_default=True)
@click.option("--dry-run", is_flag=True)
@click.pass_context
def remote_mkdir(ctx: click.Context, site_name: str, path: str, trust_host_key: str | None,
                 password_env: str, key_passphrase_env: str, dry_run: bool) -> None:
    """Crea una cartella remota, inclusi gli eventuali genitori."""
    args = ["remote", "mkdir", "--site", site_name, "--path", path]
    _add_connection_args(args, trust_host_key, password_env, key_passphrase_env)
    if dry_run:
        args.append("--dry-run")
    _forward(ctx, args, password_env)


@remote_group.command("rename")
@click.option("--site", "site_name", required=True)
@click.option("--from", "source", required=True)
@click.option("--to", "target", required=True)
@click.option("--trust-host-key", default=None)
@click.option("--password-env", default="WINSCPREA_PASSWORD", show_default=True)
@click.option("--key-passphrase-env", default="WINSCPREA_KEY_PASSPHRASE", show_default=True)
@click.option("--yes", is_flag=True)
@click.option("--dry-run", is_flag=True)
@click.pass_context
def remote_rename(ctx: click.Context, site_name: str, source: str, target: str, trust_host_key: str | None,
                  password_env: str, key_passphrase_env: str, yes: bool, dry_run: bool) -> None:
    """Rinomina un elemento remoto; non sovrascrive destinazioni esistenti."""
    args = ["remote", "rename", "--site", site_name, "--from", source, "--to", target]
    _add_connection_args(args, trust_host_key, password_env, key_passphrase_env)
    if yes:
        args.append("--yes")
    if dry_run:
        args.append("--dry-run")
    _forward(ctx, args, password_env)


@remote_group.command("delete")
@click.option("--site", "site_name", required=True)
@click.option("--path", required=True)
@click.option("--trust-host-key", default=None)
@click.option("--password-env", default="WINSCPREA_PASSWORD", show_default=True)
@click.option("--key-passphrase-env", default="WINSCPREA_KEY_PASSPHRASE", show_default=True)
@click.option("--yes", is_flag=True, help="Conferma anche l'eliminazione ricorsiva delle cartelle.")
@click.option("--dry-run", is_flag=True)
@click.pass_context
def remote_delete(ctx: click.Context, site_name: str, path: str, trust_host_key: str | None,
                  password_env: str, key_passphrase_env: str, yes: bool, dry_run: bool) -> None:
    """Elimina un file o una cartella remota; richiede --yes."""
    args = ["remote", "delete", "--site", site_name, "--path", path]
    _add_connection_args(args, trust_host_key, password_env, key_passphrase_env)
    if yes:
        args.append("--yes")
    if dry_run:
        args.append("--dry-run")
    _forward(ctx, args, password_env)


@cli.command("upload")
@click.argument("files", nargs=-1, required=True, type=click.Path(path_type=Path, exists=True))
@click.option("--site", "site_name", required=True)
@click.option("--remote-dir", default=None)
@click.option("--trust-host-key", default=None)
@click.option("--password-env", default="WINSCPREA_PASSWORD", show_default=True)
@click.option("--key-passphrase-env", default="WINSCPREA_KEY_PASSPHRASE", show_default=True)
@click.option("--dry-run", is_flag=True)
@click.pass_context
def upload(ctx: click.Context, files: tuple[Path, ...], site_name: str, remote_dir: str | None,
           trust_host_key: str | None, password_env: str, key_passphrase_env: str, dry_run: bool) -> None:
    """Carica file o directory locali senza sovrascrivere contenuti remoti."""
    args = ["upload", "--site", site_name]
    if remote_dir:
        args.extend(["--remote-dir", remote_dir])
    _add_connection_args(args, trust_host_key, password_env, key_passphrase_env)
    if dry_run:
        args.append("--dry-run")
    args.extend(str(path.resolve()) for path in files)
    _forward(ctx, args, password_env)


@cli.command("download")
@click.option("--site", "site_name", required=True)
@click.option("--remote-path", required=True)
@click.option("--local-dir", required=True, type=click.Path(path_type=Path, file_okay=False))
@click.option("--trust-host-key", default=None)
@click.option("--password-env", default="WINSCPREA_PASSWORD", show_default=True)
@click.option("--key-passphrase-env", default="WINSCPREA_KEY_PASSPHRASE", show_default=True)
@click.option("--dry-run", is_flag=True)
@click.pass_context
def download(ctx: click.Context, site_name: str, remote_path: str, local_dir: Path, trust_host_key: str | None,
             password_env: str, key_passphrase_env: str, dry_run: bool) -> None:
    """Scarica un elemento remoto in una cartella locale, senza sovrascrivere."""
    args = ["download", "--site", site_name, "--remote-path", remote_path, "--local-dir", str(local_dir.resolve())]
    _add_connection_args(args, trust_host_key, password_env, key_passphrase_env)
    if dry_run:
        args.append("--dry-run")
    _forward(ctx, args, password_env)


def _add_connection_args(args: list[str], trust: str | None, password_env: str, key_passphrase_env: str) -> None:
    if trust:
        args.extend(["--trust-host-key", trust])
    args.extend(["--password-env", password_env, "--key-passphrase-env", key_passphrase_env])


def _repl(json_mode: bool) -> None:
    Path(skin.history_file).parent.mkdir(parents=True, exist_ok=True)
    skin.print_banner()
    session = skin.create_prompt_session()
    click.echo("Digita 'help' per i comandi, 'exit' per uscire. Le password non vengono salvate.")
    while True:
        try:
            line = skin.get_input(session, context="SFTP")
        except (EOFError, KeyboardInterrupt):
            break
        if not line:
            continue
        try:
            parts = _split_repl(line)
        except ValueError as exc:
            skin.error(f"Sintassi non valida: {exc}")
            continue
        if parts and parts[0].lower() in {"exit", "quit"}:
            break
        if parts and parts[0].lower() in {"help", "?"}:
            parts = [*parts[1:], "--help"] if len(parts) > 1 else ["--help"]
        try:
            cli.main(args=parts, prog_name="winscprea", standalone_mode=False, obj={"json": json_mode})
        except click.exceptions.Exit as exc:
            if exc.exit_code:
                skin.error(f"Comando terminato con codice {exc.exit_code}.")
        except click.ClickException as exc:
            exc.show()
    skin.print_goodbye()


def _split_repl(line: str) -> list[str]:
    """Split a REPL line while preserving Windows paths and quoted arguments."""
    if os.name != "nt":
        return shlex.split(line)
    import ctypes

    count = ctypes.c_int()
    shell32 = ctypes.windll.shell32
    shell32.CommandLineToArgvW.argtypes = [ctypes.c_wchar_p, ctypes.POINTER(ctypes.c_int)]
    shell32.CommandLineToArgvW.restype = ctypes.POINTER(ctypes.c_wchar_p)
    kernel32 = ctypes.windll.kernel32
    kernel32.LocalFree.argtypes = [ctypes.c_void_p]
    kernel32.LocalFree.restype = ctypes.c_void_p
    argv = shell32.CommandLineToArgvW("winscprea " + line, ctypes.byref(count))
    if not argv:
        raise ValueError("Impossibile interpretare la riga di comando.")
    try:
        return [argv[index] for index in range(1, count.value)]
    finally:
        kernel32.LocalFree(argv)


def main() -> None:
    cli(prog_name="winscprea")


if __name__ == "__main__":
    main()
