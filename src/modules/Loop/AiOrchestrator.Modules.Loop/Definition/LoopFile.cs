namespace AiOrchestrator.Modules.Loop.Definition;

/// <summary>
/// #669. <c>.aio/loop.yaml</c> as YamlDotNet deserialises it, before any rule: every field a
/// string or a plain mapping, so a broken value reaches <see cref="LoopReader"/> to be named rather
/// than failing the read.
/// </summary>
sealed class LoopFile
{
    public string? Name { get; set; }
    public string? Wip { get; set; }
    public Dictionary<string, AgentFile?>? Agents { get; set; }
    public Dictionary<string, StateFile?>? States { get; set; }
}

sealed class AgentFile
{
    public string? Provider { get; set; }
    public string? Model { get; set; }
}

sealed class StateFile
{
    public object? Script { get; set; }
    public string? Agent { get; set; }
    public string? Command { get; set; }
    public string? Score { get; set; }
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public string? MaxLoops { get; set; }
    public Dictionary<string, object?>? On { get; set; }
    public string? Next { get; set; }
    public string? Pr { get; set; }
    public string? Back { get; set; }
    public string? Tab { get; set; }
}
