## 1. The commands

- [x] 1.1 In `.ai/commands/aio/refine.md`, replace the two per-action comment instructions of "Close what you can" with one section before `## End` that posts a single closing comment with `gh issue comment $ARGUMENTS --body-file <file>`: what the run closed in the body, what is left for the person who opened the issue and what would close it, or that the five were already answerable
- [x] 1.2 Make the same change in `harness/commands/refine.md`, keeping it free of this repository's names

## 2. The allow list

- [x] 2.1 Add `Bash(gh issue comment:*)` to the allow list in `.claude/settings.json`, beside the `gh issue` read entries

## 3. Tests

- [x] 3.1 In `AgentCommandTests`, assert that `refine.md` names `gh issue comment` before `## End` and in no second place
- [x] 3.2 Assert that `.claude/settings.json` allows `Bash(gh issue comment:*)` and still does not allow `gh` as a whole or `gh api`
