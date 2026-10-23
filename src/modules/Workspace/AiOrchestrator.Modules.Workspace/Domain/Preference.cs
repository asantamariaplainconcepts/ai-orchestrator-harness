using AiOrchestrator.BuildingBlocks.Domain;

namespace AiOrchestrator.Modules.Workspace.Domain;

/// <summary>
/// One machine-wide choice the person made, rather than one the operator configured.
/// <para>
/// <b>Why a table and not <c>IConfiguration</c>.</b> Both of the values stored here already had a
/// configuration key — <c>Desktop:Editor</c>, and the worktree default the run dialog assumes —
/// and a configuration key is something you edit in a file and restart. The settings screen showed
/// both as if they were editable and neither was: the toggle was a <c>useState</c> that sprang
/// back on reload, and the editor had no control at all. A row is what makes the screen's claim
/// true. Configuration stays underneath as the fallback, so an operator who set
/// <c>Desktop:Editor</c> in <c>appsettings</c> still gets it until somebody chooses otherwise here.
/// </para>
/// <para>
/// <b>Keyed by name, and the names are a closed set</b> — see the use cases in
/// <c>Features/Preferences</c>, which read and write named fields rather than accepting whatever
/// key a caller invents. This is a preferences table, not a key-value store the API hands out.
/// </para>
/// </summary>
sealed class Preference : Aggregate
{
    Preference() { }

    Preference(string name, string value)
    {
        Name = name;
        Value = value;
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Always a string, including for the boolean one. The alternative is a column per type and a
    /// nullable triple per row; two callers that both know what they stored is cheaper.
    /// </summary>
    public string Value { get; private set; } = string.Empty;

    public static Preference Create(string name, string value) => new(name, value);

    public void Set(string value) => Value = value;
}

/// <summary>
/// The names, in one place, because two use cases and a migration all have to agree on them.
/// </summary>
static class PreferenceNames
{
    /// <summary>Which editor "Open in" launches. A command line name — <c>code</c>, <c>rider</c>.</summary>
    public const string Editor = "desktop.editor";

    /// <summary>
    /// <c>true</c> once the requirements screen has been shown with every required CLI present
    /// (#680); until then the console opens on it.
    /// </summary>
    public const string RequirementsSeen = "requirements.seen";
}

/// <summary>
/// What an editor may be called, when the caller is a browser rather than the operator.
/// <para>
/// <b>This is a security boundary, not tidiness.</b> An editor name reaches
/// <c>DesktopIntegration.OpenInEditor</c>, and that method has a branch which hands anything
/// containing a space to <c>/bin/sh -c</c> — deliberately, because
/// <c>Desktop:Editor</c> in <c>appsettings</c> is written by whoever runs the server and
/// <c>code --wait</c> is a legitimate thing for them to write. An HTTP caller is not that person.
/// So a value arriving over the API is a <b>bare command name</b>: no spaces, no separators, no
/// shell metacharacters, which cannot reach that branch and cannot be a path either.
/// </para>
/// <para>
/// The operator's configuration keeps its full expressive power. Only the choice made on a screen
/// is narrowed, which is the one that a page in a browser can be talked into making.
/// </para>
/// </summary>
static class EditorName
{
    const int MaxLength = 60;

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= MaxLength
        && value.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.'
        );
}
