## MODIFIED Requirements

### Requirement: The declared requirement is enforced before validation and the handler

The dispatch decorators SHALL enforce what the request declares before validation and before the
handler runs. The CQS pipeline SHALL consist of logging, authorization, validation and the handler,
without unused caching or invalidation decorators. A request without a declared requirement SHALL
be refused. Caches owned inside a handler's implementation SHALL not bypass authorization.

#### Scenario: A cached implementation answer is not a permission bypass
- **WHEN** a handler's implementation reuses a previously read answer and an unpermitted caller repeats the request
- **THEN** authorization refuses the request before that handler runs

#### Scenario: A handler never checks a permission itself
- **WHEN** a handler runs
- **THEN** the requirement has already been enforced by the pipeline

#### Scenario: A denied caller does not reach validation
- **WHEN** an unpermitted caller submits an invalid payload
- **THEN** authorization refuses the request before validating its fields or invoking its handler

## RENAMED Requirements

- FROM: `### Requirement: The declared requirement is enforced before the handler and outside the cache`
- TO: `### Requirement: The declared requirement is enforced before validation and the handler`
