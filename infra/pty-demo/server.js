// A PTY per WebSocket, spawned from whichever backend the page asked for. Bytes go out as they
// come; the only message the page sends besides keystrokes is a resize, as JSON with a leading
// NUL so it can never be mistaken for typed input.
import { createServer } from "node:http";
import { readFile } from "node:fs/promises";
import { WebSocketServer } from "ws";
import pty from "node-pty";
import { backends } from "./backends.js";

const port = Number(process.env.PORT ?? 7681);
const page = new URL("./public/index.html", import.meta.url);

const http = createServer(async (req, res) => {
  if (req.url === "/backends") {
    const list = Object.entries(backends).map(([id, b]) => ({
      id,
      label: b.label,
      isolation: b.isolation,
      disabled: b.disabled || null,
    }));
    res.writeHead(200, { "content-type": "application/json" }).end(JSON.stringify(list));
    return;
  }
  res.writeHead(200, { "content-type": "text/html; charset=utf-8" }).end(await readFile(page));
});

new WebSocketServer({ server: http }).on("connection", (socket, req) => {
  const url = new URL(req.url, "http://localhost");
  const backend = backends[url.searchParams.get("backend")];
  if (!backend || backend.disabled) {
    socket.close(1008, backend?.disabled || "unknown backend");
    return;
  }

  const started = Date.now();
  const term = pty.spawn(backend.command, backend.args, {
    name: "xterm-256color",
    cols: Number(url.searchParams.get("cols") ?? 100),
    rows: Number(url.searchParams.get("rows") ?? 30),
    cwd: backend.cwd ?? process.cwd(),
    env: { ...process.env, ...backend.env, TERM: "xterm-256color" },
  });
  console.log(`[${url.searchParams.get("backend")}] pid ${term.pid}: ${backend.command} ${backend.args.slice(0, 3).join(" ")}…`);

  let first = true;
  term.onData((data) => {
    if (first) {
      first = false;
      socket.send(`\x00${JSON.stringify({ firstByteMs: Date.now() - started })}`);
    }
    socket.send(data);
  });
  term.onExit(({ exitCode }) => {
    socket.send(`\r\n\x1b[2m[proceso terminado, código ${exitCode}]\x1b[0m\r\n`);
    socket.close();
  });

  socket.on("message", (raw) => {
    const text = raw.toString();
    if (text.startsWith("\x00")) {
      const { cols, rows } = JSON.parse(text.slice(1));
      term.resize(cols, rows);
    } else {
      term.write(text);
    }
  });
  socket.on("close", () => term.kill());
});

http.listen(port, "127.0.0.1", () => console.log(`pty-demo on http://127.0.0.1:${port}`));
