## 1. Input contract

- [x] 1.1 Add the GitHub issue form and reference-only PR template, preserving legacy DoR.
- [x] 1.2 Implement the bounded pure validator using shared control identity.
- [x] 1.3 Expose the authorized read-only assessment endpoint.

## 2. Verification

- [x] 2.1 Cover form/API/manual edits, malformed data, identity, dependencies, Seam, epics and legacy bodies.
- [x] 2.2 Run targeted tests and validate the OpenSpec change; coordinate the full gate with the parent.
- [x] 2.3 Commit the verified slice locally and report consumer limitations.

Verified: dotnet test AiOrchestrator.Modules.Tasks.FunctionalTests 183/183 (0 failed);
openspec validate --strict clean. The wrong-type theory treats a text value in a free-text
field (observation, goal, changeId) as valid input, matching the existing empty-array case.
The endpoint reports assessment snapshots only; it neither reads nor writes provider state,
so consumers must fetch issue bodies upstream.
