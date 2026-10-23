# the-request-pipeline Specification

## Purpose
How a request reaches a handler and how a failure reaches the caller: modules discovered from the assemblies
beside the host, use cases mapped from their own static routes method, one dispatch pipeline with the
permission check outside the cache, and exactly one error body. Reverse-engineered from the shipped code, not
negotiated ahead of it.

## Requirements
### Requirement: Modules and their routes are discovered, not registered by hand
`ModuleRegistration` SHALL compose whatever `AiOrchestrator.Modules.*.dll` ships beside the host, excluding
`.Contracts` assemblies because they carry no implementation. `UseCaseDiscovery` SHALL map every `IUseCase`
exposing a static `AddRoutes(IEndpointRouteBuilder)`, so adding a use case adds its endpoints with nothing
central to update — and the architecture tests SHALL assert every use case exposes that method.

#### Scenario: A new module is composed by shipping beside the host
- **WHEN** a module assembly is present at startup
- **THEN** its registrations and its use cases' routes are added without a call site naming it

#### Scenario: A Contracts assembly is not composed
- **WHEN** a module's `.Contracts` assembly is present
- **THEN** it is excluded from module discovery

### Requirement: The declared requirement is enforced before the handler and outside the cache
The dispatch decorators SHALL enforce what the request declares **before** the handler runs, and that check
SHALL sit **outside** the caching decorator on purpose: inside it, a response cached for somebody allowed
would be served to somebody who is not.

#### Scenario: A cached answer is not a permission bypass
- **WHEN** a permitted caller's response is cached and an unpermitted caller repeats the request
- **THEN** the refusal happens before the cache is consulted

#### Scenario: A handler never checks a permission itself
- **WHEN** a handler runs
- **THEN** the requirement has already been enforced by the pipeline

### Requirement: Every request has exactly one handler, dispatched through the sender
A request SHALL be dispatched through `ISender` to exactly **one** handler, and every handler SHALL be
`internal sealed` — CQS001 fails the build otherwise, and the architecture tests assert both the count and the
shape.

#### Scenario: A second handler for one request fails the build or the suite
- **WHEN** two handlers exist for the same request
- **THEN** the architecture test fails

### Requirement: There is one error body, and it is RFC 7807
Domain errors SHALL be `ErrorOr<T>` values mapped to HTTP through `ApiResults.Problem` — the single mapping,
with no ad-hoc status codes at call sites. Input-validation failures SHALL short-circuit to
`GlobalExceptionHandler` as a 400 `ValidationProblemDetails`, and everything else SHALL be a 500
`ProblemDetails`. No endpoint SHALL write its own error body. A refusal a person can act on SHALL be a 4xx
carrying the remedy rather than a 500.

#### Scenario: A domain refusal is a problem document
- **WHEN** a handler returns a domain error
- **THEN** the response is RFC 7807 through `ApiResults.Problem`, carrying the sentence the domain used

#### Scenario: A validation failure is a 400 with the field
- **WHEN** a request fails its validator
- **THEN** the response is a 400 `ValidationProblemDetails` written by the global handler
