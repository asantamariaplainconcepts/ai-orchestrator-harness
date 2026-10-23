## ADDED Requirements

### Requirement: The attachments are read from the body the mirror already holds
The Evidence step SHALL read an issue's attachments out of the mirrored body it already has, and
SHALL NOT ask the vendor for a list. An attachment is a markdown image or link in that body whose
address is a vendor asset — `github.com/user-attachments/assets/…` and the older
`github.com/<owner>/<repo>/assets/…` — and its **name is the alt text**, which is what the vendor
writes when a file is attached and what `gh --attach '<file>#<alt>'` puts there.

Reading it costs no seam, no column and no cache: the body arrives with the task that is already on
screen, so the list is free and cannot be stale in a way the rest of the tab is not.

An attachment whose alt text is empty SHALL still be listed. It has no name to match on, which is a
fact about that attachment and not a reason to drop it.

#### Scenario: An attachment in the body is listed
- **WHEN** the mirrored body contains a markdown image whose address is a `user-attachments` asset
  and whose alt text is `evidence-294-login.png`
- **THEN** the step lists one attachment named `evidence-294-login.png` at that address

#### Scenario: A picture that is not a vendor asset is not an attachment
- **WHEN** the body embeds an image hosted somewhere that is not a vendor asset address
- **THEN** it is not listed as an attachment of the issue

#### Scenario: An unnamed attachment is still listed
- **WHEN** the body contains a vendor asset whose alt text is empty
- **THEN** it is listed, with no name, and it does not match the convention

### Requirement: Matching is by name against the issue's own number, and the rule is on the screen
An attachment SHALL be counted as matched when its name begins `evidence-<the issue's number>-`,
case-insensitively. Everything else the issue carries SHALL be listed apart, under its own heading,
rather than dropped or folded in: matching by proximity would be the product guessing which picture
belongs to which change.

The step SHALL state how many matched, and SHALL state the rule it matched by in one sentence
beside the list. A filter nobody can read is a filter that looks like a fault the first time
nothing matches.

#### Scenario: Three of five match
- **WHEN** issue #235 has five attachments, three of them named `evidence-235-…` and two named
  anything else
- **THEN** the step shows that three matched, draws those three, and lists the other two apart

#### Scenario: The rule is readable
- **WHEN** the step is drawn for issue #235
- **THEN** the sentence naming the convention `evidence-235-*` is on screen, whether or not
  anything matched

#### Scenario: A number that is a prefix of another is not matched
- **WHEN** issue #23 is on screen and an attachment is named `evidence-235-login.png`
- **THEN** it does not match, because the separator after the number is part of the rule

### Requirement: Every row says where it came from, and the two provenances are told apart
Each row SHALL say where the attachment came from. There are two: **the issue body**, which a person
wrote, and **a run step**, which the loop captured. They SHALL be distinguishable on screen by
wording and by glyph, and SHALL NOT be distinguishable by colour alone.

A row read from the body SHALL name the issue it is attached to. It SHALL NOT name a person or a
time the mirror does not hold: the vendor exposes no uploader and no upload time for an asset
embedded in a body, and inventing either would be the same false-looking-true this epic's #283 was
opened to remove. The freshness a row can honestly give is the mirror's own — when this issue was
last read — and it SHALL be said as that.

#### Scenario: A body attachment says which issue it is attached to
- **WHEN** a row was read from issue #294's body
- **THEN** it reads as attached to issue #294, with how long ago the issue was mirrored

#### Scenario: A run-step capture says which step captured it
- **WHEN** a row came from a run step
- **THEN** it names that step, and its wording and glyph differ from a body row's

#### Scenario: Neither provenance is carried by colour alone
- **WHEN** the two kinds of row are drawn
- **THEN** each carries its provenance in words, so the distinction survives a reader who cannot
  separate the two colours

### Requirement: A cut body is said, and never counted as none
The parser SHALL have the three answers every reader of a mirrored body has: the attachments it
found, "this issue has none", and **"this could not be read"**. It SHALL answer the third when the
body it was handed was truncated, using the one shape the seam reader established rather than a
fourth spelling of the same idea.

The sentence naming the convention over an empty list SHALL NOT be shown when the truth is that the
body was cut. An issue with six attachments past the cut reported as an issue with none is the
failure the mirror's completeness capability exists to prevent, and a new surface does not get to
reintroduce it.

#### Scenario: A cut body with nothing found is unknown, not empty
- **WHEN** the task's body was truncated and no attachment is in the text that arrived
- **THEN** the step says the mirror holds only part of the issue, and does not say the issue has no
  attachments

#### Scenario: A whole body with nothing found is empty
- **WHEN** the task's body was not truncated and it contains no attachment
- **THEN** the step says the issue has no attachments and names the convention

#### Scenario: What arrived is still read
- **WHEN** the task's body was truncated and two attachments are in the text that arrived
- **THEN** those two are drawn, and the step also says the body was cut

### Requirement: The picture is fetched through the seam, with the vendor's credential
The bytes of an attachment SHALL be fetched by this process through the vendor seam, using the
credential that vendor's reader already resolves, and SHALL NOT be fetched by the browser. A vendor
asset is served to a request that carries the vendor's token and refused to one that does not, so a
picture pointed straight at the vendor from this app's browser is a broken image every time.

The read SHALL be addressed by an address the task's **own mirrored body names**. An address the
body does not carry SHALL be refused as not found — not as forbidden — so that probing addresses
through this route teaches nothing.

A reader for a vendor that does not serve attachment bytes SHALL say so rather than answer with
nothing that could be mistaken for an empty picture.

#### Scenario: The picture is drawn from bytes this process fetched
- **WHEN** a matched attachment is drawn
- **THEN** its bytes were fetched by the server through the seam, and the browser made no request
  to the vendor

#### Scenario: An address the issue does not carry is refused
- **WHEN** the route is asked for an asset address that is not in that task's mirrored body
- **THEN** it answers not found, and says nothing about whether that address exists

### Requirement: A picture that cannot be drawn is said, never drawn broken
Where the bytes cannot be had, the step SHALL say which of the reasons it is, in words, in the row's
own place — and SHALL NOT draw a broken image, an empty frame, or a picture that is not there.

The reasons SHALL be distinct: the vendor answered and the asset is **gone**; nothing answered at
all, so it is **unreachable**; and the asset is larger than this product will carry, so it is **too
large**. Collapsing them would tell somebody a screenshot was deleted when their network was down.

Failure SHALL be confined to the row it is about. One attachment that cannot be fetched SHALL NOT
stop the others from drawing, SHALL NOT empty the list, and SHALL NOT affect any other step of the
tab.

#### Scenario: An asset the vendor no longer serves
- **WHEN** the vendor answers that an attachment's asset is not there
- **THEN** the row says the attachment is no longer at the vendor, and draws no image

#### Scenario: Nothing answers at all
- **WHEN** the vendor cannot be reached for any of the attachments
- **THEN** each row says so, the names, the count and the rule are still on screen, and every other
  step of the Change tab works exactly as it did

#### Scenario: One failure does not take the rest
- **WHEN** three attachments are drawn and the second cannot be fetched
- **THEN** the first and third draw their pictures and the second says why it has none
