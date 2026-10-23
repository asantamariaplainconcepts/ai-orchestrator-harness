using System.Reflection;
using AiOrchestrator.BuildingBlocks.Identity;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiOrchestrator.BuildingBlocks.CQS;

// The pipeline order is fixed by AddVsaCqsArchitecture and is not configurable per call site:
// Logging -> Authorization -> Validation -> Handler.

sealed partial class LoggingCommandHandlerDecorator<TCommand, TResponse>(
    IAppCommandHandler<TCommand, TResponse> inner,
    ILogger<LoggingCommandHandlerDecorator<TCommand, TResponse>> logger
) : IAppCommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<TResponse> Handle(TCommand command, CancellationToken cancellationToken)
    {
        LogHandling(logger, typeof(TCommand).Name);
        var response = await inner.Handle(command, cancellationToken);
        LogHandled(logger, typeof(TCommand).Name);
        return response;
    }

    [LoggerMessage(EventId = 1001, Level = LogLevel.Debug, Message = "Handling command {Command}")]
    static partial void LogHandling(ILogger logger, string command);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Debug, Message = "Handled command {Command}")]
    static partial void LogHandled(ILogger logger, string command);
}

sealed partial class LoggingQueryHandlerDecorator<TQuery, TResponse>(
    IAppQueryHandler<TQuery, TResponse> inner,
    ILogger<LoggingQueryHandlerDecorator<TQuery, TResponse>> logger
) : IAppQueryHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    public async Task<TResponse> Handle(TQuery query, CancellationToken cancellationToken)
    {
        LogHandling(logger, typeof(TQuery).Name);
        var response = await inner.Handle(query, cancellationToken);
        LogHandled(logger, typeof(TQuery).Name);
        return response;
    }

    [LoggerMessage(EventId = 1003, Level = LogLevel.Debug, Message = "Handling query {Query}")]
    static partial void LogHandling(ILogger logger, string query);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Debug, Message = "Handled query {Query}")]
    static partial void LogHandled(ILogger logger, string query);
}

/// <summary>
/// Enforces the request's declared permission before validation and the handler.
/// </summary>
sealed class AuthorizationCommandHandlerDecorator<TCommand, TResponse>(
    IAppCommandHandler<TCommand, TResponse> inner,
    IProjectPermissions permissions,
    IOptions<PermissionGrants> grants,
    ProjectVisibilityAudit audit
) : IAppCommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<TResponse> Handle(TCommand command, CancellationToken cancellationToken)
    {
        await AuthorizationPipeline.EnsurePermitted(
            command,
            permissions,
            grants.Value,
            cancellationToken
        );

        var asked = audit.Asked;
        var response = await inner.Handle(command, cancellationToken);

        await AuthorizationPipeline.EnsureNarrowed(
            command,
            permissions,
            asked,
            audit,
            cancellationToken
        );

        return response;
    }
}

sealed class AuthorizationQueryHandlerDecorator<TQuery, TResponse>(
    IAppQueryHandler<TQuery, TResponse> inner,
    IProjectPermissions permissions,
    IOptions<PermissionGrants> grants,
    ProjectVisibilityAudit audit
) : IAppQueryHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    public async Task<TResponse> Handle(TQuery query, CancellationToken cancellationToken)
    {
        await AuthorizationPipeline.EnsurePermitted(
            query,
            permissions,
            grants.Value,
            cancellationToken
        );

        // Snapshotted before the handler runs, so that the check below reads the *handler's*
        // behaviour and not its own question.
        var asked = audit.Asked;
        var response = await inner.Handle(query, cancellationToken);

        await AuthorizationPipeline.EnsureNarrowed(
            query,
            permissions,
            asked,
            audit,
            cancellationToken
        );

        return response;
    }
}

static class AuthorizationPipeline
{
    public static async Task EnsurePermitted<TRequest>(
        TRequest request,
        IProjectPermissions permissions,
        PermissionGrants grants,
        CancellationToken cancellationToken
    )
    {
        // The concrete request type, because Sender resolves the handler by it — so a declaration
        // on the command is a declaration the pipeline actually reads.
        var declared = typeof(TRequest).GetCustomAttribute<RequiresAttribute>();

        // Default deny (design D1). An operation nobody declared anything for is refused, which is
        // the whole reason this is a decorator and not a line inside each handler: forgetting has
        // to fail closed.
        if (declared is null)
        {
            throw new PermissionDeniedException(typeof(TRequest).FullName!);
        }

        // The two declarations that are not permissions. Whether anybody is asking at all is the
        // pipeline's business, not this decorator's: /api is refused unauthenticated before a
        // handler is ever resolved.
        if (declared.Access is Access.AnyCaller or Access.FiltersToCaller)
        {
            return;
        }

        if (declared.Permission is not { } permission)
        {
            // An Access value added without a rule here, or an attribute in some third state. Refused
            // rather than waved through, for the same reason omission is.
            throw new PermissionDeniedException(typeof(TRequest).FullName!);
        }

        if (request is not IScopedToProject scoped)
        {
            // Not a refusal — a wiring mistake. Refusing would hide it behind a 403 that reads like
            // an ordinary one; this says which type is wrong, and a test asserts the pairing so it
            // cannot reach a deployment (task 5.3).
            throw new InvalidOperationException(
                $"{typeof(TRequest).Name} requires '{permission}' but does not implement "
                    + $"{nameof(IScopedToProject)}, so there is no project to check it against."
            );
        }

        // Two questions, in order, and neither collapses into the other: which bundle this caller
        // holds *here*, then whether that bundle holds this permission.
        var role = await permissions.RoleOn(scoped.ProjectId, cancellationToken);

        if (role is null || !grants.Holds(role.Value, permission))
        {
            throw new PermissionDeniedException(typeof(TRequest).FullName!);
        }
    }

    /// <summary>
    /// Holds <see cref="Access.FiltersToCaller"/> to its word: a request that says it narrows its
    /// own answer, for a caller whose answer would in fact narrow, must have asked. If it did not,
    /// the response it just built spans every project and is refused rather than sent.
    /// <para>
    /// After the handler rather than before, because "did you ask" is not a question that has an
    /// answer beforehand. The cost is a response computed and thrown away, which is the correct
    /// trade the first time it happens and zero times after — this fires on a wiring mistake, not
    /// on a caller.
    /// </para>
    /// <para>
    /// It cannot fire where nothing narrows. One owner, or a bootstrap administrator, gets
    /// <c>null</c> from <see cref="IProjectPermissions.VisibleProjects"/> meaning all of them, and
    /// an unfiltered answer is then simply the right answer — which is exactly why nine unfiltered
    /// slices sat here unnoticed until a deployment had two people in it.
    /// </para>
    /// </summary>
    public static async Task EnsureNarrowed<TRequest>(
        TRequest request,
        IProjectPermissions permissions,
        bool askedBeforeHandling,
        ProjectVisibilityAudit audit,
        CancellationToken cancellationToken
    )
    {
        if (
            askedBeforeHandling
            || audit.Asked
            || typeof(TRequest).GetCustomAttribute<RequiresAttribute>()?.Access
                is not Access.FiltersToCaller
        )
        {
            return;
        }

        if (await permissions.VisibleProjects(cancellationToken) is not null)
        {
            throw new PermissionDeniedException(
                $"{typeof(TRequest).FullName} declares {nameof(Access.FiltersToCaller)} but never "
                    + $"read {nameof(IProjectPermissions.VisibleProjects)}, so its answer spans "
                    + "every project."
            );
        }
    }
}

/// <summary>
/// Input-shape validation. A failure short-circuits the pipeline as a <see cref="ValidationException"/>,
/// which the global exception handler renders as RFC 7807 ValidationProblemDetails. Domain errors are a
/// different concern and travel as <c>ErrorOr</c> results returned by the handler.
/// </summary>
sealed class ValidationCommandHandlerDecorator<TCommand, TResponse>(
    IAppCommandHandler<TCommand, TResponse> inner,
    IEnumerable<IValidator<TCommand>> validators
) : IAppCommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<TResponse> Handle(TCommand command, CancellationToken cancellationToken)
    {
        await ValidationPipeline.EnsureValid(command, validators, cancellationToken);
        return await inner.Handle(command, cancellationToken);
    }
}

sealed class ValidationQueryHandlerDecorator<TQuery, TResponse>(
    IAppQueryHandler<TQuery, TResponse> inner,
    IEnumerable<IValidator<TQuery>> validators
) : IAppQueryHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    public async Task<TResponse> Handle(TQuery query, CancellationToken cancellationToken)
    {
        await ValidationPipeline.EnsureValid(query, validators, cancellationToken);
        return await inner.Handle(query, cancellationToken);
    }
}

static class ValidationPipeline
{
    public static async Task EnsureValid<TRequest>(
        TRequest request,
        IEnumerable<IValidator<TRequest>> validators,
        CancellationToken cancellationToken
    )
    {
        var failures = new List<FluentValidation.Results.ValidationFailure>();

        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(request, cancellationToken);
            if (!result.IsValid)
            {
                failures.AddRange(result.Errors);
            }
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }
    }
}
