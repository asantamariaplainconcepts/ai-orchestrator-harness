using AiOrchestrator.Server.Installed;

namespace AiOrchestrator.Server.FunctionalTests;

/// <summary>
/// #678. What the .NET tool <c>aio</c> answers to its arguments. The harness's command has the same
/// name, so <c>aio issue view 1</c> typed at the tool must start nothing and point at the script.
/// </summary>
public sealed class ToolTests
{
    [Fact]
    public void The_harness_command_typed_at_the_tool_is_a_usage_error_naming_the_script()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        Installation.Answer(["issue", "view", "1"], output, error).ShouldBe(2);

        error.ToString().ShouldContain(".aio/aio");
        error.ToString().ShouldContain("issue view 1");
        output.ToString().ShouldBeEmpty();
    }

    [Fact]
    public void Version_prints_the_version_api_version_reports()
    {
        using var output = new StringWriter();

        Installation.Answer(["--version"], output, TextWriter.Null).ShouldBe(0);

        var reported = typeof(Installation).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .Cast<System.Reflection.AssemblyInformationalVersionAttribute>()
            .Single()
            .InformationalVersion;
        output.ToString().Trim().ShouldBe(reported);
    }

    [Fact]
    public void Help_says_how_to_start_it_and_no_arguments_start_it()
    {
        using var output = new StringWriter();

        Installation.Answer(["--help"], output, TextWriter.Null).ShouldBe(0);
        output.ToString().ShouldContain("48117");

        Installation.Answer([], TextWriter.Null, TextWriter.Null).ShouldBeNull();
    }

    [Fact]
    public void A_build_output_is_not_the_tool()
    {
        // Only `dotnet pack` writes DotnetToolSettings.xml beside the assembly.
        Installation.IsTool.ShouldBeFalse();
    }
}
