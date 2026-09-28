#!/usr/bin/env python3
"""Verify the real game's private launcher pipe with a bounded process."""
import subprocess
import sys

process = subprocess.Popen(sys.argv[1:], stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                           stderr=subprocess.STDOUT, text=True, errors='replace')
try:
    output = process.communicate('show\nclose\n', timeout=30)[0]
except subprocess.TimeoutExpired:
    process.kill()
    output = process.communicate()[0]
    raise RuntimeError('Launcher control timed out: ' + output)
print(output, flush=True)
if process.returncode != 0 or any(f'LAUNCHER_CONTROL: {command}' not in output for command in ['show', 'close']):
    raise RuntimeError('Game did not receive both commands or exit normally')
print('LAUNCHER_GAME_CONTROL_PASS: real Godot process receives Show/Close and exits normally', flush=True)
