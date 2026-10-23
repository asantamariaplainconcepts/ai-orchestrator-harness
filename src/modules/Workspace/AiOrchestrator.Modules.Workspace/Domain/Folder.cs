using AiOrchestrator.BuildingBlocks.Domain;

namespace AiOrchestrator.Modules.Workspace.Domain;

/// <summary>
/// A local directory this product may act on: a checkout the loop walks and a person opens
/// sessions in.
/// <para>
/// <b>Usually a git checkout, and not necessarily one.</b> See <see cref="IsRepository"/>.
/// Everything below that is <i>about</i> git — branch and remote — is meaningless for such a folder rather than merely empty, and that one flag is what says which.
/// </para>
/// <para>
/// The row holds what the app knows <i>about</i> the folder — where it is, and the agent and model it
/// defaults to. What the folder <i>is</i> — its branch, its remote — is read from disk when it is
/// added and cached here, so a list renders without asking git about every repository.
/// </para>
/// </summary>
sealed class Folder : Aggregate
{
    Folder() { }

    Folder(string name, string path, string defaultAgentId)
    {
        Name = name;
        Path = path;
        DefaultAgentId = defaultAgentId;
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Absolute, or <c>~</c>-prefixed as the user typed it. Unique across folders.</summary>
    public string Path { get; private set; } = string.Empty;

    /// <summary>Which agent a run here starts with unless the person picks another.</summary>
    public string DefaultAgentId { get; private set; } = string.Empty;

    /// <summary>
    /// The model a session here starts with unless the person picks another, or null for the
    /// agent CLI's own default. Set in Settings beside <see cref="DefaultAgentId"/> (#515).
    /// </summary>
    public string? DefaultModel { get; private set; }

    /// <summary>
    /// Whether git describes this directory, decided once, when the folder is added. <c>AddFolder</c>
    /// is the only writer, because it is the only place with a person present to be told they got
    /// the path wrong.
    /// <para>
    /// True by default, and the column defaults to true: every row stored before this existed came
    /// through a gate that refused anything git could not describe.
    /// </para>
    /// </summary>
    public bool IsRepository { get; private set; } = true;

    // Probed state, cached when the folder is added. Both are meaningless rather than empty when
    // `IsRepository` is false.
    public string Branch { get; private set; } = string.Empty;

    /// <summary>
    /// Where <c>origin</c> points, or null for a checkout with no remote. It is the only field that
    /// identifies the repository itself rather than one person's copy of it, since
    /// <see cref="Name"/> and <see cref="Path"/> are both chosen by whoever cloned it: Settings and
    /// the loop read the vendor's repository from it.
    /// </summary>
    public string? RemoteUrl { get; private set; }

    /// <summary>
    /// A new folder. <paramref name="isRepository"/> defaults to true because that is what almost
    /// every folder is, and because the seed and the tests both add real checkouts.
    /// </summary>
    public static Folder Create(
        string name,
        string path,
        string defaultAgentId,
        bool isRepository = true
    ) => new(name, path, defaultAgentId) { IsRepository = isRepository };

    /// <summary>
    /// Changes both defaults at once, because a model belongs to an agent: a model kept across a
    /// change of agent would name something the new CLI does not have.
    /// </summary>
    public void ChooseDefaults(string agentId, string? model)
    {
        DefaultAgentId = agentId;
        DefaultModel = string.IsNullOrWhiteSpace(model) ? null : model;
    }

    /// <summary>What git said about the checkout when it was added.</summary>
    public void RecordProbe(string branch, string? remoteUrl)
    {
        Branch = branch;
        RemoteUrl = remoteUrl;
    }
}
