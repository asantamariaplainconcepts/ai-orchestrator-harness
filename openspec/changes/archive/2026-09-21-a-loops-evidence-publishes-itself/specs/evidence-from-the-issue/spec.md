## MODIFIED Requirements

### Requirement: Every row says where it came from, and the two provenances are told apart
Every row SHALL say where it came from, and the two provenances told apart: a picture a person put
and a picture the loop produced are different facts to whoever approves. The loop's own publishes —
`/aio:implement`'s evidence comment — arrive as issue attachments named by the convention, carrying
the SHA they were observed against in the same comment, so the provenance that reaches the reader is
the vendor's own record of the upload and not a phrase the loop wrote about itself.

#### Scenario: The loop publishes what a change moved
- **WHEN** `/aio:implement` publishes captures for issue `#n`
- **THEN** one comment carries the images embedded and the observed SHA, and the evidence step pairs
  them as it pairs any attachment — by the `evidence-<n>-*` name

#### Scenario: The publication degrades
- **WHEN** the credential, the CLI's version or the vendor refuses the publish
- **THEN** no comment is composed, the captures stay in `.harness/evidence/`, and the issue's check
  line says UNPUBLISHED with the reason — an absent attachment stays absent rather than invented

### Requirement: The attachments are read from the body the mirror already holds
The attachments SHALL keep being read from the body the mirror already holds — the loop's publishes
are ordinary comments and take the same path a person's do: the vendor's asset URLs in the issue's
body, never a second store and never a parallel API.

#### Scenario: The loop's comment is read like anyone's
- **WHEN** the evidence step reads an issue whose latest comment is the loop's publish
- **THEN** the attachments pair by name exactly as a person's do, with no second code path and no
  second store
