#!/usr/bin/env python3
"""Cross-platform build/run/check/export commands. Requires Python 3.11+."""
import argparse
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / "src" / "LuckerParty.Godot"
LAUNCHER = ROOT / "src" / "LuckerParty.Launcher"


def build_info(version):
    commit = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
    dirty = bool(subprocess.check_output(["git", "status", "--porcelain"], cwd=ROOT, text=True).strip())
    return {"version": version, "commit": commit, "dirty": dirty}


def write_json(path, value):
    path.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")


def export_game(args, base, environment, version):
    preset, filename = ("Windows Desktop", "LuckerParty.exe") if args.target == "windows" else ("Linux", "LuckerParty.x86_64")
    output = ROOT / "artifacts" / "exports" / args.target / version
    if output.exists():
        shutil.rmtree(output)
    output.mkdir(parents=True)
    run(base + ["--headless", "--export-release", preset, str(output / filename)],
        environment, timeout=300, inspect=True)
    if not (output / filename).is_file() or not (output / "LuckerParty.pck").is_file():
        raise RuntimeError("Export is incomplete: missing executable or resource pack")
    required = ["LuckerParty.Godot.dll", "LuckerParty.Core.dll", "GodotSharp.dll",
                "coreclr.dll" if args.target == "windows" else "libcoreclr.so"]
    for dependency in required:
        if not any(output.rglob(dependency)):
            raise RuntimeError(f"Export is incomplete: missing {dependency}")
    shutil.copy2(ROOT / "docs/PLAYTEST.txt", output / "PLAYTEST.txt")
    write_json(output / "build-info.json", build_info(version))
    archive = shutil.make_archive(str(ROOT / "artifacts" / f"LuckerParty-{version}-{args.target}-x64"), "zip", root_dir=output)
    print(f"Packaged full game export: {archive}")
    return output


def package_launcher(args, dotnet, environment, game, version):
    platform = "win-x64" if args.target == "windows" else "linux-x64"
    suffix = "Windows" if args.target == "windows" else "Linux"
    package_id = f"LuckerParty.{'Dev.' if args.profile == 'development' else ''}{suffix}"
    stage = ROOT / "artifacts" / "staging" / args.profile / platform / version / args.channel
    if stage.exists():
        shutil.rmtree(stage)
    run([dotnet, "publish", str(LAUNCHER / "LuckerParty.Launcher.csproj"), "-c", "Release",
         "-r", platform, "--self-contained", "true", "-o", str(stage), f"-p:Version={version}"], environment, timeout=300)
    shutil.copytree(game, stage / "game")
    write_json(stage / "build-info.json", build_info(version))
    write_json(stage / "distribution.json", {
        "packageId": package_id, "platform": platform, "channel": args.channel,
        "repository": "https://github.com/ConcosHut/LuckerParty", "feed": args.feed,
        "gameExecutable": "LuckerParty.exe" if args.target == "windows" else "LuckerParty.x86_64"
    })
    shutil.copy2(ROOT / "docs/THIRD-PARTY-NOTICES.md", stage / "THIRD-PARTY-NOTICES.md")
    shutil.copytree(ROOT / "licenses", stage / "licenses")
    output = Path(args.output_dir).resolve() if args.output_dir else ROOT / "artifacts" / "releases" / args.profile / platform
    output.mkdir(parents=True, exist_ok=True)
    if any(output.glob(f"*{version}*-full.nupkg")):
        raise RuntimeError(f"Version {version} already exists in {output}; choose a new version or output directory")
    run([dotnet, "tool", "restore"], environment)
    directive = "[win]" if args.target == "windows" else "[linux]"
    command = [dotnet, "tool", "run", "vpk", "--", directive, "pack",
               "--packId", package_id, "--packVersion", version, "--packDir", str(stage),
               "--mainExe", "LuckerParty.Launcher.exe" if args.target == "windows" else "LuckerParty.Launcher",
               "--runtime", platform, "--channel", f"{platform}-{args.channel}",
               "--outputDir", str(output), "--packTitle", "Lucker Party" + (" Dev" if args.profile == "development" else ""),
               "--packAuthors", "ConcosHut", "--releaseNotes", str(ROOT / "docs/release-notes.md")]
    if args.target == "linux":
        command += ["--icon", str(ROOT / "assets/launcher-icon.png"), "--categories", "Game"]
    run(command, environment, timeout=300)
    if not any(output.glob(f"*{version}*-full.nupkg")) or not (output / f"releases.{platform}-{args.channel}.json").is_file():
        raise RuntimeError("Velopack package/feed is incomplete")
    print(f"Launcher packages and update feed: {output}")


def tool_path(name, explicit, local_candidates):
    if explicit:
        return explicit
    for candidate in local_candidates:
        if candidate.is_file():
            return str(candidate)
    found = shutil.which(name)
    if not found:
        raise RuntimeError(f"Cannot find {name}. Set its command-line option or install the pinned tool; see README.md.")
    return found


def run(command, environment, timeout=180, inspect=False):
    print("Running:", subprocess.list2cmdline([str(item) for item in command]), flush=True)
    result = subprocess.run(command, cwd=ROOT, env=environment, check=False, timeout=timeout,
                            capture_output=inspect, text=True)
    if inspect:
        output = result.stdout + result.stderr
        print(output, end="", flush=True)
        result.check_returncode()
        # Godot can return zero even when its .NET exporter logs an error.
        if "ERROR:" in output or "Build FAILED." in output:
            raise RuntimeError("Godot logged an error; refusing to accept this run")
        return output
    result.check_returncode()


def main():
    # Godot/MSBuild share intermediate files across targets. Own the workspace
    # for the entire command rather than allowing concurrent exports to race.
    lock_path = ROOT / ".tools/build.lock"
    lock_path.parent.mkdir(exist_ok=True)
    build_lock = lock_path.open("a+b")
    if os.name == "nt":
        import msvcrt
        build_lock.seek(0)
        if not build_lock.read(1):
            build_lock.write(b"0")
            build_lock.flush()
        build_lock.seek(0)
        msvcrt.locking(build_lock.fileno(), msvcrt.LK_NBLCK, 1)
    else:
        import fcntl
        fcntl.flock(build_lock.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=["build", "run", "check", "export", "pack"])
    parser.add_argument("--target", choices=["windows", "linux"], default="windows")
    parser.add_argument("--godot", default=os.environ.get("GODOT_BIN"))
    parser.add_argument("--dotnet", default=os.environ.get("DOTNET_BIN"))
    parser.add_argument("--version", help="SemVer package/export identity (default: version.json)")
    parser.add_argument("--channel", choices=["stable", "beta"], default="stable")
    parser.add_argument("--profile", choices=["development", "player"], default="development")
    parser.add_argument("--feed", help="Optional local/private update feed for development packages")
    parser.add_argument("--output-dir", help="Velopack release directory; retained to generate deltas")
    args = parser.parse_args()
    dotnet = tool_path("dotnet", args.dotnet, [ROOT / ".tools/dotnet/dotnet", ROOT / ".tools/dotnet/dotnet.exe"])
    environment = os.environ.copy()
    environment["PATH"] = str(Path(dotnet).resolve().parent) + os.pathsep + environment.get("PATH", "")
    squashfs = ROOT / ".tools/squashfs/usr/bin"
    if squashfs.is_dir():
        environment["PATH"] = str(squashfs) + os.pathsep + environment["PATH"]
    environment["DOTNET_ROOT"] = str(Path(dotnet).resolve().parent)
    environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
    environment["DOTNET_NOLOGO"] = "1"
    version = args.version or json.loads((ROOT / "version.json").read_text())["version"]
    if not re.fullmatch(r"(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)(?:-[A-Za-z0-9.-]+)?", version):
        raise RuntimeError("Version must be a three-part SemVer with optional prerelease")
    run([dotnet, "build", str(PROJECT / "LuckerParty.Godot.csproj")], environment)
    run([dotnet, "build", str(LAUNCHER / "LuckerParty.Launcher.csproj")], environment)
    # Keep engine dependencies out of the portable core.
    import xml.etree.ElementTree as ET
    core = ET.parse(ROOT / "src/LuckerParty.Core/LuckerParty.Core.csproj")
    references = core.findall(".//ProjectReference") + core.findall(".//PackageReference")
    if any("godot" in reference.get("Include", "").lower() for reference in references):
        raise RuntimeError("Core cannot reference Godot; keep engine integration in LuckerParty.Godot.")
    if args.command == "build":
        return
    editor_pattern = "**/Godot*mono_win64_console.exe" if os.name == "nt" else "**/Godot*mono_linux.x86_64"
    godot = tool_path("godot", args.godot, sorted((ROOT / ".tools/godot").glob(editor_pattern)))
    godot_version = subprocess.check_output([godot, "--version"], env=environment, text=True).strip()
    if not godot_version.startswith("4.5.2.stable.mono"):
        raise RuntimeError(f"Expected Godot 4.5.2 .NET; found {godot_version}")
    base = [godot, "--path", str(PROJECT)]
    run(base + ["--headless", "--editor", "--import"], environment, inspect=True)
    if args.command == "run":
        run(base, environment, timeout=None)
    elif args.command == "check":
        output = run(base + ["--headless", "--fixed-fps", "60", "--", "--smoke-test"],
                     environment, timeout=60, inspect=True)
        if "SMOKE_TEST_PASS:" not in output:
            raise RuntimeError("Smoke scenario did not report completion")
        output = run(base + ["--headless", "--fixed-fps", "240", "--", "--camera-check"],
                     environment, timeout=60, inspect=True)
        if "CAMERA_CHECK_PASS:" not in output:
            raise RuntimeError("Camera interpolation scenario did not report completion")
        run([dotnet, "run", "--project", str(ROOT / "tests/LuckerParty.Launcher.Checks")], environment, timeout=60)
        run([dotnet, "run", "--project", str(ROOT / "tests/LuckerParty.Core.Checks")], environment, timeout=60)
        run([sys.executable, str(ROOT / "tools/test_multiplayer.py"), "--godot", godot], environment, timeout=180)
    else:
        output = export_game(args, base, environment, version)
        if args.command == "pack":
            package_launcher(args, dotnet, environment, output, version)


if __name__ == "__main__":
    try:
        main()
    except (OSError, RuntimeError, subprocess.CalledProcessError, subprocess.TimeoutExpired) as error:
        print(f"FAILED: {error}", file=sys.stderr)
        sys.exit(1)
