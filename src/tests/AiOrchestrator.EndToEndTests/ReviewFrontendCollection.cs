using Xunit;

namespace AiOrchestrator.EndToEndTests;

/// <summary>
/// The classes whose tests stub the SPA's HTTP at the route level against one Vite booted out of
/// this checkout (`ReviewFrontendFixture`). A definition local to #431 was folded into master with
/// it; this branch left origin/master before that merge, so its classes carry their own until the
/// merge brings the shared one — at which point two definitions with one name is a compile error
/// rather than a silent second Vite, which is the failure mode this file exists to prevent: xUnit
/// serializes the tests inside a collection, and two fixtures at once kill each other over the dev
/// server's strict port (measured: "Port 5173 is already in use").
/// </summary>
[CollectionDefinition("review-frontend")]
public sealed class ReviewFrontendCollection : ICollectionFixture<ReviewFrontendFixture>;
