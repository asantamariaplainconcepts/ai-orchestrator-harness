# Tasks: laya-scores-the-risk

## 1. The server supervisor

- [x] 1.1 Write `.harness/laya-requirements.txt` pinning `laya[serve]==0.3.20`
      req: risk-score/5
      files: .harness/laya-requirements.txt
- [x] 1.2 Write `.harness/scripts/laya` — `ensure|start|stop|status`, pidfile and venv under
      `.harness/data/laya/`, loopback `127.0.0.1:8317`, `SSL_CERT_FILE` pinned to the venv's certifi,
      idempotent `ensure`, `start` refusing a live port by naming the pid that holds it
      req: risk-score/4
      files: .harness/scripts/laya
- [x] 1.3 Verify against reality: `ensure` on this machine twice (second run a no-op saying healthy),
      `stop`, `status` reading a dead pidfile without starting anything, `/health` answering on loopback
      only
      req: risk-score/4
      files: .harness/scripts/laya

## 2. The scorer

- [x] 2.1 Write `.harness/scripts/risk-score <issue>` — state from `gh issue view` + change directory +
      diffstat, `noul` questions over the repository's collision rules plus one `choice` band, weights in
      code, `confidence` = `answer_confidence`, `why` naming every fired question, fallbacks to
      `medium/0/0` with the reason and no retry
      req: risk-score/1, risk-score/2, risk-score/3
      files: .harness/scripts/risk-score
- [x] 2.2 Verify against reality: run it on this branch's own issue (the change scoring itself) and on a
      server deliberately stopped, and read both answers as the JSON the loop declares
      req: risk-score/1, risk-score/3
      files: .harness/scripts/risk-score

## 3. Wiring into the loop's declarations

- [x] 3.1 `.harness/issue-loop.yaml` — the `risk` node's comment says laya rather than Jev, and its
      fallback line names the script and its prerequisites; nothing else moves
      req: risk-score/1
      files: .harness/issue-loop.yaml
- [x] 3.2 `.harness/commands.json` — add the `laya-status` one-shot (no tab, no gate)
      req: risk-score/5
      files: .harness/commands.json
- [x] 3.3 `.harness/protected-paths` check: the new `.harness/` files need no entry — `.harness/*`
      already covers them, so the tripwire asks for a person on this change exactly as designed
      files: .harness/scripts/laya

## 4. The gate

- [x] 4.1 `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj` passes on
      what is about to be merged, and `openspec validate laya-scores-the-risk --strict` passes
      req: risk-score/1
      files: .harness/scripts/risk-score
