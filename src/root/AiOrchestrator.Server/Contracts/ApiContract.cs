using System.Reflection;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace AiOrchestrator.Server.Contracts;

/// <summary>
/// The API's contract with the frontend (#508): an OpenAPI document of every route under
/// <c>/api</c>, written by <c>dotnet build</c> to <c>src/frontend/shared/http/openapi.json</c>, from
/// which the frontend's build generates <c>contracts.ts</c>. A DTO that changes changes the
/// document, and a screen that reads what is gone fails <c>pnpm typecheck</c>. Generating replaces
/// keeping two copies by hand and checking that they agree (#490).
/// </summary>
static class ApiContract
{
    const string Prefix = "api/";

    /// <summary>
    /// Whether this process is the build describing the API rather than a server: the OpenAPI
    /// build step runs the entry point under its own assembly, with a server that listens nowhere.
    /// </summary>
    public static bool IsBeingDescribed { get; } = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

    public static IServiceCollection AddApiContract(this IServiceCollection services) =>
        services.AddOpenApi(
            options =>
            {
                options.ShouldInclude = description =>
                    description.RelativePath?.StartsWith(Prefix, StringComparison.Ordinal) == true;

                // Every use case names its answer Response and its body Request, Command or Query, so
                // the bare name would collide; the use case's name in front keeps each schema's name
                // the same from one build to the next.
                options.CreateSchemaReferenceId = type =>
                    OpenApiOptions.CreateDefaultSchemaReferenceId(type) is { } name
                    && type.Type is { Name: "Response" or "Request" or "Command" or "Query", DeclaringType: { } useCase }
                        ? useCase.Name + name
                        : OpenApiOptions.CreateDefaultSchemaReferenceId(type);

                // The web defaults read a number written as a string too, and the schema says so;
                // the server only ever writes numbers, so the contract says number and nothing else.
                options.AddSchemaTransformer(
                    (schema, _, _) =>
                    {
                        if (schema.Type is { } type && (type & (JsonSchemaType.Integer | JsonSchemaType.Number)) != 0)
                        {
                            schema.Type = type & ~JsonSchemaType.String;
                            schema.Pattern = null;
                        }
                        return Task.CompletedTask;
                    }
                );
            }
        );
}
