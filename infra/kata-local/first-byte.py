"""Milliseconds from spawning a command in a PTY to its first byte of output.

The same number pty-demo's server.js reports as firstByteMs: the console spawns `kubectl exec -it`
in a PTY and waits for the first thing it says.

    python3 first-byte.py kubectl exec -it <pod> -- sh -c 'tty'
"""
import fcntl
import os
import pty
import select
import struct
import sys
import termios
import time

start = time.monotonic()
pid, fd = pty.fork()
if pid == 0:
    os.execvp(sys.argv[1], sys.argv[1:])
# 30 rows by 100 columns, what pty-demo's page asks for before it knows the window.
fcntl.ioctl(fd, termios.TIOCSWINSZ, struct.pack("HHHH", 30, 100, 0, 0))

ready, _, _ = select.select([fd], [], [], 60)
first = os.read(fd, 1) if ready else b""
elapsed = (time.monotonic() - start) * 1000
rest = b""
while True:
    ready, _, _ = select.select([fd], [], [], 10)
    if not ready:
        break
    try:
        chunk = os.read(fd, 4096)
    except OSError:
        break
    if not chunk:
        break
    rest += chunk
os.waitpid(pid, 0)
output = (first + rest).decode(errors="replace").replace("\r", "").strip().replace("\n", " | ")
print(f"{elapsed:.0f} ms  {output}" if first else "no output in 60 s")
