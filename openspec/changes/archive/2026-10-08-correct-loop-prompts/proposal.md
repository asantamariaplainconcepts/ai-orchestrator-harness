## Why

An audit of the loop's prompts (`.ai/commands/aio/*.md` and the skills they name), run with the
prompt-audit method against the three ships of 7 October, found 13 defects (#493): a gate `/aio:ship`
cannot satisfy (an approved PR), seven places where a skill waits for a person that ship does not
have, a gate that was not rerun on the commit being merged, `/aio:waves` defaulting to a status refine
no longer writes, phantom tools and commands (`TodoWrite`, `/opsx-apply`, `openspec-sync-specs`), and
a broken Copilot prompt link.

## What Changes

- `/aio:ship` refuses an argument that is not an open issue, says how each person-shaped step is
  decided or turned into a failure, and lets a person pause a run without a hold.
- `/aio:sync` reruns the gate on the close-out commit before the merge; in ship the bump is the patch.
- `/aio:waves` defaults to `status:spec-ready`.
- `/aio:implement` says how to evidence a screen `capture.sh` does not shoot, and how to count a
  requirement's ordinal.
- `/aio:refine` says when ship may edit the issue body.
- The skills that duplicate a script say the command or script wins inside `/aio:*`; the phantom
  references are removed; `.github/prompts/` gains `/aio-review` and loses the broken `/aio-grill`.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `land-a-change`: the gate runs on the commit the merge takes, and ship decides or fails every step that asks a person.

## Impact

`.ai/commands/aio/`, `.ai/commands/opsx/`, seven skills under `.ai/skills/`, `.github/prompts/`. No code.
