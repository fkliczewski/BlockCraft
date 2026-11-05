#!/usr/bin/env python3
"""Kompiluje i uruchamia BlockCraft.

Użycie:
    python run.py                  # Release, uruchom grę
    python run.py --debug          # konfiguracja Debug
    python run.py --no-build       # bez kompilacji
    python run.py -- --selftest    # argumenty po "--" trafiają do gry
"""
import argparse
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent
PROJECT = ROOT / "src" / "BlockCraft" / "BlockCraft.csproj"


def main() -> int:
    parser = argparse.ArgumentParser(description="Kompiluje i uruchamia BlockCraft.")
    parser.add_argument("--debug", action="store_true", help="konfiguracja Debug zamiast Release")
    parser.add_argument("--no-build", action="store_true", help="pomiń kompilację")
    parser.add_argument("game_args", nargs=argparse.REMAINDER, help="argumenty dla gry (po --)")
    args = parser.parse_args()

    config = "Debug" if args.debug else "Release"
    game_args = args.game_args
    if game_args and game_args[0] == "--":
        game_args = game_args[1:]

    if not args.no_build:
        print(f"== Kompilacja ({config})", flush=True)
        build = subprocess.run(["dotnet", "build", str(PROJECT), "-c", config, "--nologo"])
        if build.returncode != 0:
            print("Kompilacja nie powiodła się.", file=sys.stderr)
            return build.returncode

    exe_name = "BlockCraft.exe" if sys.platform == "win32" else "BlockCraft"
    exe = PROJECT.parent / "bin" / config / "net10.0" / exe_name
    if not exe.exists():
        print(f"Nie znaleziono {exe} - uruchom bez --no-build.", file=sys.stderr)
        return 1

    print(f"== Uruchamianie {exe.relative_to(ROOT)} {' '.join(game_args)}", flush=True)
    # Katalog roboczy jak przy "dotnet run" (folder projektu).
    return subprocess.run([str(exe), *game_args], cwd=PROJECT.parent).returncode


if __name__ == "__main__":
    sys.exit(main())
