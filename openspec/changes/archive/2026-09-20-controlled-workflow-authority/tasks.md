## 1. Establish the ACP-00 boundary

- [x] 1.1 Read live epic #321, the held #317/#318 proposal, ADR-0005 and shipped controlled execution behavior.
- [x] 1.2 Record Proposed ADR-0006 with alternatives and the exact superseded clauses, leaving accepted ADR-0005 intact.

## 2. Publish the proposed contract

- [x] 2.1 Define authority, full Task identity, pinned published policy and bounded human grants.
- [x] 2.2 Define atomic claim/capacity CAS, nonce/read-back rules, generations, permit fencing and no-takeover recovery.
- [x] 2.3 Define module ownership and stable dispatch identity for ACP-01/02/04/05 without implementing those slices.
- [x] 2.4 Cover absence, uncertainty, Hold, cancellation, closure without code, expired grants and owner conflict in transition examples.
- [x] 2.5 Document cooperative threat-model limits and preserve legacy/local-only execution without shared authority.

## 3. Validate and hand off

- [x] 3.1 Validate OpenSpec deltas strictly, relative documentation links and whitespace; confirm accepted ADR and held proposal are unchanged.
- [x] 3.2 Prepare the documentation-only slice for local commit and send its authority contract and paths to the coordinating sessions.

Runtime implementation belongs to the separate ACP slices in #321. Completing this
documentation checklist is not human acceptance, lifecycle activation or removal of #317's Hold.
