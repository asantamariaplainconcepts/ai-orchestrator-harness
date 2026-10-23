using System.Globalization;
using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Ports;
using FluentValidation;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AiOrchestrator.Modules.Loop.Definition;

/// <summary>What reading a loop file gave: the loop, or every problem found in it.</summary>
/// <param name="Loop">Null whenever <paramref name="Problems"/> is not empty — a loop with one
/// broken rule is not a loop the engine may half-follow.</param>
/// <param name="Problems">Each one names where it is, as a path into the file
/// (<c>states.code-review.on.rejected</c>), then what is wrong.</param>
sealed record LoopReading(LoopDefinition? Loop, IReadOnlyList<string> Problems)
{
    public bool IsValid => Problems.Count == 0;
}

/// <summary>
/// Reads <c>.aio/loop.yaml</c> in the format of README.md#the-loop and checks its rules.
/// Every problem is collected rather than the first one returned, so one save fixes the file.
/// <para>
/// #669. Deserialised onto <see cref="LoopFile"/>, whose shape <see cref="Shape"/> checks, then into
/// a <see cref="LoopDefinition"/>, whose rules across states <see cref="Rules"/> checks once every
/// state is read. The keys are checked on the file as plain mappings: a typed read drops an unknown one.
/// </para>
/// </summary>
static class LoopReader
{
    static readonly string[] RootKeys = ["name", "wip", "agents", "states"];
    static readonly string[] AgentKeys = ["provider", "model"];
    static readonly string[] StateKeys = ["script", "agent", "command", "score", "provider", "model", "max_loops", "on", "next", "pr", "back", "tab"];
    static readonly string[] OutcomeKeys = ["hold", "next", "pr"];
    static readonly Dictionary<string, VendorStep> Steps = new(StringComparer.Ordinal) { ["draft"] = VendorStep.OpenDraftPullRequest, ["ready"] = VendorStep.MarkReady, ["close"] = VendorStep.Close };
    const string File = "loop.yaml";
    static readonly Shape FileShape = new();
    static readonly Rules LoopRules = new();
    static readonly IDeserializer Yaml = new DeserializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).IgnoreUnmatchedProperties().WithDuplicateKeyChecking().Build();

    /// <summary>The folder's loop, or why it cannot be followed, as the engine and the Loop screen's strip read it.</summary>
    public static async Task<(LoopDefinition? Definition, string? Problem)> ReadAsync(LoopFolder folder, CancellationToken ct)
    {
        var path = HarnessScripts.Own(folder.Path, File);
        if (!System.IO.File.Exists(path))
        {
            return (null, $"No loop declared. Add .aio/{File} to the repository.");
        }

        var reading = Read(await System.IO.File.ReadAllTextAsync(path, ct));
        return reading.Loop is { } loop ? (loop, null) : (null, $"{Path.GetRelativePath(folder.Path, path).Replace('\\', '/')} breaks its rules: {string.Join("; ", reading.Problems)}");
    }

    public static LoopReading Read(string yaml)
    {
        LoopFile? file;
        Dictionary<object, object>? keys;
        try
        {
            file = Yaml.Deserialize<LoopFile?>(yaml);
            keys = Yaml.Deserialize<Dictionary<object, object>?>(yaml);
        }
        catch (YamlException failure)
        {
            return new LoopReading(null, [$"line {failure.Start.Line}: {(failure.InnerException ?? failure).Message}"]);
        }

        string[] problems = file is null || keys is null
            ? ["the file is not a mapping with name, agents and states"]
            : [.. Unknown(keys), .. FileShape.Validate(file).Errors.Select(error => error.ErrorMessage)];
        var loop = problems.Length == 0 ? Definition(file!) : null;
        problems = loop is null ? problems : [.. LoopRules.Validate(loop).Errors.Select(error => error.ErrorMessage)];
        return problems.Length == 0 ? new LoopReading(loop, []) : new LoopReading(null, problems);
    }

    static LoopDefinition Definition(LoopFile file) =>
        new(
            file.Name!,
            Count(file.Wip),
            file.Agents?.ToDictionary(agent => agent.Key, agent => new LoopAgent(agent.Value!.Provider!, Text(agent.Value.Model)), StringComparer.Ordinal) ?? [],
            [.. file.States!.Select(entry => State(entry.Key, entry.Value!))]
        );

    static LoopState State(string name, StateFile state) =>
        new(name, Scripts(state.Script)!, Text(state.Agent), Text(state.Command), Text(state.Score), Text(state.Provider), Text(state.Model), Count(state.MaxLoops), Transitions(state), Text(state.Back), Text(state.Tab));

    static List<LoopTransition> Transitions(StateFile state) =>
        state.Next is { } next
            ? [new LoopTransition(null, next, null, Step(state.Pr))]
            : [
                .. (state.On ?? []).Select(outcome => outcome.Value is IDictionary<object, object> mapped
                    ? new LoopTransition(outcome.Key, Text(mapped, "next"), Text(mapped, "hold"), Step(Text(mapped, "pr")))
                    : new LoopTransition(outcome.Key, (string)outcome.Value!, null)),
            ];

    static VendorStep Step(string? pr) => Steps.GetValueOrDefault(pr ?? "");

    /// <summary>The shape a state's fields must have before anything is read across states.</summary>
    sealed class Shape : AbstractValidator<LoopFile>
    {
        public Shape()
        {
            RuleFor(file => file.Name).NotEmpty().WithMessage("name: the loop has no name");
            RuleFor(file => file.Wip).Must(Counted).WithMessage("wip: a whole number above zero");
            RuleForEach(file => file.Agents).Must(agent => Text(agent.Value?.Provider) is not null).WithMessage((_, agent) => $"agents.{agent.Key}: declares no provider");
            RuleFor(file => file.States).NotEmpty().WithMessage("states: the loop declares no states");
            RuleForEach(file => file.States).Must(entry => entry.Value is not null).WithMessage((_, entry) => $"states.{entry.Key}: a state is a mapping of what it runs and where it leads");
            RuleForEach(file => file.States).Custom((entry, context) =>
            {
                foreach (var problem in entry.Value is { } state ? Problems(state, $"states.{entry.Key}").OfType<string>() : [])
                {
                    context.AddFailure(problem);
                }
            });
        }

        static IEnumerable<string?> Problems(StateFile state, string where)
        {
            yield return state.On is not null && state.Next is not null ? $"{where}: has both on and next; next is for a state with one way out" : null;
            yield return state.Next is null && state.Pr is not null ? $"{where}.pr: goes beside next; under on, it goes on the outcome that takes it" : null;
            yield return state.Next is not null ? Pr(state.Pr, $"{where}.pr") : null;
            yield return state.On is { Count: 0 } ? $"{where}.on: a mapping of each outcome to the state it leads to" : null;
            yield return Scripts(state.Script) is null ? $"{where}.script: one script, or a list of them" : null;
            yield return Counted(state.MaxLoops) ? null : $"{where}.max_loops: a whole number above zero";
            foreach (var (outcome, value) in state.On ?? [])
            {
                yield return Outcome(value, $"{where}.on.{outcome}");
            }
        }

        /// <summary>
        /// An outcome leads to a state, to a mapping with its <c>next</c> and <c>pr</c>, or to a hold
        /// with its reason and an optional <c>next</c>. Anything else under it is a second set of
        /// outcomes, and a state produces one.
        /// </summary>
        static string? Outcome(object? value, string at)
        {
            if (value is string { Length: > 0 })
            {
                return null;
            }
            if (value is not IDictionary<object, object> mapped)
            {
                return $"{at}: leads to a state, or to a hold with its reason";
            }
            var nested = mapped.Keys.Select(key => $"{key}").Where(key => !OutcomeKeys.Contains(key, StringComparer.Ordinal)).ToArray();
            return nested.Length > 0
                    ? $"{at}: on does not nest — a state produces one outcome, and an outcome leads to a state or to a hold (found {string.Join(", ", nested)})"
                : !mapped.ContainsKey("hold") ? Text(mapped, "next") is null ? $"{at}: leads to a state with next, or to a hold with its reason" : Pr(Text(mapped, "pr"), $"{at}.pr")
                : Text(mapped, "hold") is null ? $"{at}: a hold carries its reason, which is what the person reads"
                : mapped.ContainsKey("next") && Text(mapped, "next") is null ? $"{at}.next: names one state"
                : mapped.ContainsKey("pr") ? $"{at}.pr: a hold moves nothing at the vendor; pr goes on an outcome that leads to a state"
                : null;
        }

        static string? Pr(string? pr, string where) =>
            pr is null || Steps.ContainsKey(pr) ? null : $"{where}: '{pr}' is not a value of the loop format; it takes {string.Join(", ", Steps.Keys)}";
    }

    /// <summary>The rules that hold across states, once every state has been read.</summary>
    sealed class Rules : AbstractValidator<LoopDefinition>
    {
        public Rules()
        {
            RuleForEach(loop => loop.Agents).Must(agent => Installed(agent.Value.Provider)).WithMessage((_, agent) => $"agents.{agent.Key}: provider '{agent.Value.Provider}' is not one of {string.Join(", ", LoopAgent.Providers)}");
            RuleForEach(loop => loop.States).Must(state => state.Score is null || (state.Agent is null && state.Command is null)).WithMessage((_, state) => $"states.{state.Name}: runs an agent and a score; a state runs one or the other");
            RuleForEach(loop => loop.States).Must((loop, state) => state.Agent is null || loop.Agents.ContainsKey(state.Agent)).WithMessage((_, state) => $"states.{state.Name}: agent '{state.Agent}' is not declared in agents");
            RuleForEach(loop => loop.States).Must(state => state.Provider is null || Installed(state.Provider)).WithMessage((_, state) => $"states.{state.Name}: provider '{state.Provider}' is not one of {string.Join(", ", LoopAgent.Providers)}");

            // back is a person's act, not an outcome, so it needs no max_loops; it only has to land
            // on a state the loop runs.
            RuleForEach(loop => loop.States).Must((loop, state) => state.Back is null || loop.States.Any(other => other.Name == state.Back)).WithMessage((_, state) => $"states.{state.Name}.back: '{state.Back}' is not a state of the loop");
            RuleForEach(loop => loop.States).Must(state => state.Tab is null || LoopState.Tabs.Contains(state.Tab, StringComparer.Ordinal)).WithMessage((_, state) => $"states.{state.Name}.tab: '{state.Tab}' is not one of {string.Join(", ", LoopState.Tabs)}");
            RuleForEach(loop => loop.States).Must((loop, state) => state.MaxLoops is not null || Back(loop, state) is null).WithMessage((loop, state) => $"states.{state.Name}: goes back to {Back(loop, state)} without max_loops; a state that can go back says how many times");
        }

        static bool Installed(string provider) => LoopAgent.Providers.Contains(provider, StringComparer.Ordinal);

        /// <summary>A target declared at or above this state, since the file reads top to bottom; a hold's next counts.</summary>
        static string? Back(LoopDefinition loop, LoopState state)
        {
            var index = Index(loop, state.Name);
            return state.Transitions.Select(transition => transition.Target).OfType<string>().FirstOrDefault(target => Index(loop, target) is var at && at >= 0 && at <= index);
        }

        static int Index(LoopDefinition loop, string name) => loop.States.Select(state => state.Name).ToList().IndexOf(name);
    }

    static IEnumerable<string> Unknown(Dictionary<object, object> root) =>
        Unknown(root, RootKeys, "").Concat(
            new[] { ("agents", AgentKeys), ("states", StateKeys) }.SelectMany(section =>
                root.GetValueOrDefault(section.Item1) is IDictionary<object, object> children
                    ? children.SelectMany(child => child.Value is IDictionary<object, object> declared ? Unknown(declared, section.Item2, $"{section.Item1}.{child.Key}.") : [])
                    : [])
        );

    static IEnumerable<string> Unknown(IDictionary<object, object> node, string[] known, string where) =>
        node.Keys.Select(key => $"{key}").Where(key => !known.Contains(key, StringComparer.Ordinal)).Select(key => $"{where}{key}: not a key of the loop format; it takes {string.Join(", ", known)}");

    /// <summary>One script or a list of them, or null when it is neither.</summary>
    static List<string>? Scripts(object? script) => script switch
    {
        null => [],
        string { Length: > 0 } one => [one],
        List<object> many when many.All(each => each is string { Length: > 0 }) => [.. many.Cast<string>()],
        _ => null,
    };

    static bool Counted(string? count) => count is null || Count(count) is not null;

    static int? Count(string? count) => int.TryParse(count, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0 ? value : null;

    static string? Text(string? text) => text is { Length: > 0 } ? text : null;

    static string? Text(IDictionary<object, object> map, string key) => map.TryGetValue(key, out var value) && value is string { Length: > 0 } text ? text : null;
}
