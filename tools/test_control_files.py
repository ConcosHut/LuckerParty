"""Atomic scenario commands despite brief Windows reader locks."""

import os
from pathlib import Path
import time


def replace_control_file(temporary: Path, destination: Path) -> None:
    deadline = time.monotonic() + 2
    while True:
        try:
            os.replace(temporary, destination)
            return
        except PermissionError:
            # Godot can have the previous JSON open for a moment. Windows then
            # denies replacement; a short retry preserves the atomic handoff.
            if os.name != "nt" or time.monotonic() >= deadline:
                raise
            time.sleep(.01)
