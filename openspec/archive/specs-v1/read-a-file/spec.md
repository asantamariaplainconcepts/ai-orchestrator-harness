# read-a-file Specification

## Purpose
The file tree of a working directory and the preview beside it — text over one endpoint, bytes over
another, and git as the thing that clears the path. Most files in a checkout have not changed at all, so
"what is in this file" is a different question from "what changed in it". Reverse-engineered from the
shipped code, not negotiated ahead of it.

## Requirements
### Requirement: A file's text is read through the probe, and git clears the path
The system SHALL expose `GET /api/folders/{id:guid}/file` (`Features/Folders/UseCases/GetFile.cs`,
`workspace:read`) returning one file's contents for the preview beside the tree. The path SHALL be
treated as the untrusted input and SHALL be cleared by **git**: `IWorkspaceProbe` returns nothing for a
path git does not list in the working directory the folder resolved to — which covers `../`, an absolute
path, a symlink out of the checkout and anything ignored — so this slice SHALL NOT reason about path
traversal itself.

#### Scenario: A path that climbs out is refused
- **WHEN** the requested path is `../` something, an absolute path, or a symlink pointing out of the
  directory
- **THEN** nothing is returned, because git does not list it

#### Scenario: A file in the working directory is read
- **WHEN** the path names a file git lists in the resolved working directory
- **THEN** its contents are returned

### Requirement: Bytes are a sibling endpoint, not a flag
The system SHALL expose `GET /api/folders/{id:guid}/file/bytes` (`GetFileBytes.cs`, `workspace:read`)
for the image or PDF the preview draws, as a sibling of the text read rather than a flag on it, because
the two are wanted at different rates: text is the nearly-always case and stays cheap, and bytes are
fetched only when the extension says the screen can render them. It SHALL inherit the same path
clearing, since both call the same probe method. A directory SHALL NOT be readable as bytes.

#### Scenario: An image is fetched only when the screen can draw it
- **WHEN** the selected file's extension is one the screen renders as bytes
- **THEN** the bytes endpoint is the one called, and the text endpoint is not

#### Scenario: A directory is refused as bytes
- **WHEN** the path names a directory
- **THEN** the read is refused

### Requirement: The tree is listed depth-first and says nothing about state
`DirectoryFiles` SHALL list the tree depth-first with directories before what is under them, SHALL say
nothing about the state of any file, SHALL list a directory symlink without walking into it, and SHALL NOT
loop. A file containing a NUL SHALL be called binary. The empty path and the directory itself SHALL be
refused.

#### Scenario: A symlinked directory is listed but not entered
- **WHEN** the tree contains a symlink to a directory
- **THEN** it is listed and not walked into, and the listing terminates

#### Scenario: A binary file is named as one
- **WHEN** a file contains a NUL byte
- **THEN** it is reported as binary rather than as text
