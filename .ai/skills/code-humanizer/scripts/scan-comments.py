#!/usr/bin/env python3
"""Mechanical floor for the code-humanizer skill.

Two jobs, and neither is judgement:

  scan     locate the comment blocks carrying a pattern's tell, so the pass starts from
           a list rather than from a scroll. A hit is a candidate, never a verdict —
           SKILL.md's "What not to touch" decides, and most hits are keeps.
  verify   prove the pass changed comment lines only. This is the completion criterion
           the skill cannot self-certify, so it is a command instead of a claim.

Dependency-free on purpose: it must run identically on a fresh clone, in CI, and inside
an agent session with nothing installed.

  scan-comments.py scan [paths...]      # default: files changed against the merge base
  scan-comments.py verify [--base REF]  # exit 1 if any non-comment line changed
"""
import re
import subprocess
import sys
from pathlib import Path

SUFFIXES = {".cs", ".ts", ".tsx"}
SKIP_PARTS = {"node_modules", "obj", "bin", "dist", ".git"}
COMMENT_START = re.compile(r"^\s*(///|//|\*|/\*)")
STRIP = re.compile(r"^\s*(///|//|\*/?|/\*+)\s?")

# Each entry: (id, headline, tell regex, guard regex or None).
# A guard that matches inside the following window clears the hit — the tell alone
# over-reports, and over-reporting a keep is how a pass destroys a fact.
PATTERNS = [
    ("P1", "internals of a codebase this repo does not control",
     r"(?:orca|copilot|cursor|codex|windsurf)(?:'s|s')?\s+`[^`]+`"
     r"|`src/main/[^`]+`|`[^`]*\.(?:ts|tsx|js|mjs):\d+`", None),
    ("P2", "credit that carries no consequence",
     r"\b(?:orca|copilot|cursor|codex)\b[^.]{0,60}"
     r"\b(?:reaches the same|is explicit about it too|agrees|confirms|says the same|"
     r"came to the same|does the same)\b", None),
    ("P3", "cross-reference that restates what it points to",
     r"same (?:rule|reason|conclusion)\b[^\"“]{0,80}[\"“]"
     r"(?:\s*\w+){5,}", None),
    ("P4", "old state with no consequence",
     r"\b(?:used to|no longer|until now|had been|previously)\b",
     r"\b(?:which|so|because|meant|leaving|left|otherwise|and that|now)\b|—|--"),
    ("P5", "status note whose subject is an issue",
     r"\b(?:since|after|before|as of)\s+#\d+",
     r"\b(?:\d[\d,.]*\s*(?:rows?|bytes?|ms|minutes?|seconds?|chunks?)|measured)\b"),
]
# P6 (a clause restating the declaration) is structural, not lexical — see restates().

GENERIC = {"the", "and", "for", "that", "this", "with", "from", "are", "was", "not",
           "but", "its", "all", "one", "has", "when", "what", "which", "into"}


def words(text):
    spaced = re.sub(r"(?<!^)(?=[A-Z])", " ", text)
    return {w.lower() for w in re.findall(r"[A-Za-z]+", spaced) if len(w) > 2} - GENERIC


def source_files(paths):
    out = []
    for raw in paths:
        p = Path(raw)
        candidates = [p] if p.is_file() else sorted(p.rglob("*"))
        for c in candidates:
            if c.suffix in SUFFIXES and not SKIP_PARTS & set(c.parts):
                out.append(c)
    return out


def blocks(path):
    """Yield (start_line, text, declaration) for every comment run of 2+ lines."""
    lines = path.read_text(errors="ignore").splitlines()
    run, start = [], 0
    for i, line in enumerate(lines + [""]):
        if COMMENT_START.match(line):
            if not run:
                start = i + 1
            run.append(STRIP.sub("", line.strip()))
        else:
            if len(run) >= 2:
                decl = line.strip() if line.strip() else ""
                yield start, " ".join(run), decl
            run = []


def restates(text, decl):
    """P6: the comment's content words are the declaration's words."""
    if not decl or len(text) > 200:
        return False
    cw, dw = words(re.sub(r"</?\w+>|`[^`]*`", " ", text)), words(decl)
    if len(cw) < 2 or not dw:
        return False
    shared = cw & dw
    return len(shared) >= 2 and len(shared) / len(cw) >= 0.7


def scan(paths):
    hits = []
    for path in source_files(paths):
        for start, text, decl in blocks(path):
            plain = re.sub(r"<code>.*?</code>", " ", text, flags=re.S)
            plain = re.sub(r"<see cref=\"[^\"]*\"\s*/?>|<[a-z/][^>]*>", " ", plain)
            for pid, headline, tell, guard in PATTERNS:
                for m in re.finditer(tell, plain, re.I):
                    window = plain[m.end():m.end() + 170]
                    if guard and re.search(guard, window, re.I):
                        continue
                    hits.append((pid, headline, path, start, m.group(0).strip()))
                    break
            if restates(text, decl):
                hits.append(("P6", "a clause restating the declaration", path, start,
                             text[:60]))
    return hits


def changed_files(base):
    cmd = ["git", "diff", "--name-only", "--diff-filter=d", base]
    out = subprocess.run(cmd, capture_output=True, text=True).stdout
    return [f for f in out.splitlines() if Path(f).suffix in SUFFIXES]


def merge_base():
    for ref in ("origin/master", "master"):
        r = subprocess.run(["git", "merge-base", "HEAD", ref],
                           capture_output=True, text=True)
        if r.returncode == 0 and r.stdout.strip():
            return r.stdout.strip()
    return "HEAD"


def verify(base):
    """Every added or removed line in the diff must be a comment line."""
    out = subprocess.run(["git", "diff", "-U0", base, "--"], capture_output=True,
                         text=True).stdout
    offenders, current, seen = [], None, set()
    for line in out.splitlines():
        if line.startswith("+++ b/"):
            current = line[6:]
        elif line[:1] in "+-" and not line.startswith(("+++", "---")):
            body = line[1:]
            if Path(current or "").suffix not in SUFFIXES:
                continue
            seen.add(current)
            if body.strip() and not COMMENT_START.match(body):
                offenders.append((current, line[0], body.strip()[:90]))
    if offenders:
        print("code changed — the pass may only touch comment lines:\n")
        for f, sign, body in offenders[:40]:
            print("  " + f + "  " + sign + " " + body)
        print("\n" + str(len(offenders)) + " non-comment lines changed")
        return 1
    if not seen:
        print("no source file in scope changed against " + base + " — nothing to verify")
        return 0
    print("comment lines only: " + str(len(seen)) + " source files changed, 0 code lines")
    return 0


def main():
    argv = sys.argv[1:]
    mode = argv[0] if argv else "scan"
    rest = argv[1:]
    if mode == "verify":
        base = rest[rest.index("--base") + 1] if "--base" in rest else merge_base()
        return verify(base)
    if mode != "scan":
        print(__doc__)
        return 2
    paths = [a for a in rest if not a.startswith("-")] or changed_files(merge_base())
    if not paths:
        print("nothing in scope")
        return 0
    hits = scan(paths)
    by_id = {}
    for pid, headline, path, line, snippet in hits:
        by_id.setdefault((pid, headline), []).append((path, line, snippet))
    for (pid, headline), rows in sorted(by_id.items()):
        print("\n" + pid + "  " + headline + "  (" + str(len(rows)) + ")")
        for path, line, snippet in rows:
            print("  " + str(path) + ":" + str(line) + "  " + snippet)
    print("\n" + str(len(hits)) + " candidates in " + str(len(set(h[2] for h in hits))) +
          " files. A candidate is not a verdict — apply \"What not to touch\".")
    return 0


if __name__ == "__main__":
    sys.exit(main())
