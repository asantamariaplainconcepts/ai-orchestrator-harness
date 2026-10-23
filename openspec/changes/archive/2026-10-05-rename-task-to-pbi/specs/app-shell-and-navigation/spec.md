# app-shell-and-navigation Delta

## MODIFIED Requirements

### Requirement: Seven routes inside one shell, with the inbox as the landing screen
`src/frontend/app/routes.tsx` SHALL declare one shell route rendering `AppShell` with children
`pbis`, `folders`, `folders/:folderId` and `settings`, and the index SHALL redirect to `/pbis`. A
client route asked for directly SHALL come back as the application, and moving between screens SHALL
be the router rather than a page load. A request for `/workflows` SHALL redirect to `/pbis` rather
than 404, because the route was a published link and a bookmark that followed it did nothing wrong.
A request for `/runs/:runId` SHALL redirect to the run's owning folder screen scoped to its working
directory with the run handed to the tab strip, for the same reason — and `/history` SHALL redirect
to `/pbis`, because the screen is deleted and its question is answered per worktree. A request for
the old `/tasks` and `/tasks/pull-requests` routes SHALL redirect to `/pbis` and
`/pbis/pull-requests` respectively, for the same reason a bookmark is never punished.

#### Scenario: A deep link is served the application
- **WHEN** `/folders/{id}` is requested directly from the browser
- **THEN** the host returns the application and the client resolves the route

#### Scenario: Navigation is not a page load
- **WHEN** a person moves between two screens
- **THEN** the document is not re-fetched

#### Scenario: A run deep link lands on its worktree
- **WHEN** `/runs/{runId}` is requested
- **THEN** the application resolves the run's folder and working directory, navigates there, and
opens the run as a tab beside that screen's content

#### Scenario: A bookmark to the old history route lands on the inbox
- **WHEN** `/history` is requested
- **THEN** the client redirects to `/pbis`

#### Scenario: A bookmark to the old workflows route lands on the inbox
- **WHEN** `/workflows` is requested
- **THEN** the client redirects to `/pbis`

#### Scenario: A bookmark to the old inbox still lands
- **WHEN** `/tasks` is requested
- **THEN** the client redirects to `/pbis`
