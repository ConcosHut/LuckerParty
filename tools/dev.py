#!/usr/bin/env python3
"""Cross-platform build/run/check/export commands. Requires Python 3.11+."""
import argparse
import os
from pathlib import Path
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / "src" / "LuckerParty.Godot"


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
    result = subprocess.run(command, cwd=ROOT, env=environment, check=True, timeout=timeout,
                            capture_output=inspect, text=True)
    if inspect:
        output = result.stdout + result.stderr
        print(output, end="", flush=True)
        # Godot can return zero even when its .NET exporter logs an error.
        if "ERROR:" in output or "Build FAILED." in output:
            raise RuntimeError("Godot logged an error; refusing to accept this run")
        return output


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=["build", "run", "check", "export"])
    parser.add_argument("--target", choices=["windows", "linux"], default="windows")
    parser.add_argument("--godot", default=os.environ.get("GODOT_BIN"))
    parser.add_argument("--dotnet", default=os.environ.get("DOTNET_BIN"))
    args = parser.parse_args()
    dotnet = tool_path("dotnet", args.dotnet, [ROOT / ".tools/dotnet/dotnet", ROOT / ".tools/dotnet/dotnet.exe"])
    environment = os.environ.copy()
    environment["PATH"] = str(Path(dotnet).resolve().parent) + os.pathsep + environment.get("PATH", "")
    environment["DOTNET_ROOT"] = str(Path(dotnet).resolve().parent)
    environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
    environment["DOTNET_NOLOGO"] = "1"
    run([dotnet, "build", str(PROJECT / "LuckerParty.Godot.csproj")], environment)
    # Keep engine dependencies out of the portable core.
    import xml.etree.ElementTree as ET
    core = ET.parse(ROOT / "src/LuckerParty.Core/LuckerParty.Core.csproj")
    references = core.findall(".//ProjectReference") + core.findall(".//PackageReference")
    if any("godot" in reference.get("Include", "").lower() for reference in references):
        raise RuntimeError("Core cannot reference Godot; keep engine integration in LuckerParty.Godot.")
    if args.command == "build":
        return
    godot = tool_path("godot", args.godot, sorted((ROOT / ".tools/godot").glob("**/Godot*mono_linux.x86_64")))
    version = subprocess.check_output([godot, "--version"], env=environment, text=True).strip()
    if not version.startswith("4.5.2.stable.mono"):
        raise RuntimeError(f"Expected Godot 4.5.2 .NET; found {version}")
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
    else:
        preset, filename = ("Windows Desktop", "LuckerParty.exe") if args.target == "windows" else ("Linux", "LuckerParty.x86_64")
        output = ROOT / "artifacts" / args.target
        output.mkdir(parents=True, exist_ok=True)
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
        archive = shutil.make_archive(str(ROOT / "artifacts" / f"LuckerParty-prototype-01.1-{args.target}-x64"), "zip", root_dir=output)
        print(f"Packaged full export: {archive}")


if __name__ == "__main__":
    try:
        main()
    except (OSError, RuntimeError, subprocess.CalledProcessError, subprocess.TimeoutExpired) as error:
        print(f"FAILED: {error}", file=sys.stderr)
        sys.exit(1)
