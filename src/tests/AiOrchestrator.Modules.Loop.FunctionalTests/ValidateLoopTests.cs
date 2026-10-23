using AiOrchestrator.Modules.Loop.Definition;

namespace AiOrchestrator.Modules.Loop.FunctionalTests;

/// <summary>#498. The use case answers what the reader found, in the shape the API sends.</summary>
public sealed class ValidateLoopTests
{
    static readonly string RepositoryLoop = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "loop.yaml"));

    [Fact]
    public async Task A_valid_loop_answers_its_states_and_where_the_loop_does_nothing()
    {
        var response = await new ValidateLoop.Handler().Handle(new ValidateLoop.Query(RepositoryLoop), CancellationToken.None);

        response.Valid.ShouldBeTrue();
        response.Problems.ShouldBeEmpty();
        response.States.Count.ShouldBe(10);
        response.States.Single(state => state.Name == "merging").Transitions
            .ShouldBe([new ValidateLoop.Transition("merged", "done", null), new ValidateLoop.Transition("conflict", "rebasing", null)]);
        response.Undeclared.ShouldBe(["needs-refinement", "done"]);
        // #606: the folder page asks the CLI each of these whether it trusts the folder.
        response.Providers.ShouldBe(["claude-code"]);
    }

    [Fact]
    public async Task A_broken_loop_answers_its_problems_and_no_states()
    {
        var yaml = RepositoryLoop.Replace("    max_loops: 1\n", "", StringComparison.Ordinal);

        var response = await new ValidateLoop.Handler().Handle(new ValidateLoop.Query(yaml), CancellationToken.None);

        response.Valid.ShouldBeFalse();
        response.States.ShouldBeEmpty();
        response.Providers.ShouldBeEmpty();
        response.Problems.ShouldBe(["states.rebasing: goes back to merging without max_loops; a state that can go back says how many times"]);
    }
}
