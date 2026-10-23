using System.Text.RegularExpressions;

namespace AiOrchestrator.ArchitectureTests;

/// <summary>
/// #500. Only the adapter behind <c>IPbiProvider</c> knows the vendor is reached through <c>gh</c>
/// and that a state is a <c>status:*</c> label. A module that named either would stop working the
/// day Azure DevOps, which has a state field and no CLI of ours, arrives through the same port.
/// <para>
/// Read in the source, because a label is a string and no reference table holds it: every module and
/// the contracts between them.
/// </para>
/// </summary>
public sealed partial class VendorTests
{
    static readonly string[] Read =
    [
        "modules",
        "shared/AiOrchestrator.BuildingBlocks",
    ];

    [GeneratedRegex("\"gh\"|\\bgh (issue|pr|api|label)\\b|status:[a-z]")]
    private static partial Regex Vendor();

    [Fact]
    public void No_module_names_gh_or_a_state_label()
    {
        var root = SourceRoot();
        var files = Read.SelectMany(folder =>
                Directory.EnumerateFiles(Path.Combine(root, folder), "*.cs", SearchOption.AllDirectories)
            )
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToArray();

        // The guard: with no files read, the assertion below holds over nothing.
        files.ShouldNotBeEmpty();

        var found =
            from file in files
            from line in File.ReadLines(file).Select((text, index) => (text, number: index + 1))
            where Vendor().IsMatch(line.text)
            select $"{Path.GetRelativePath(root, file)}:{line.number}: {line.text.Trim()}";

        found.ShouldBeEmpty(
            "gh and the status:* labels belong to Infrastructure/PbiProviders/GitHub; a module asks IPbiProvider"
        );
    }

    [Fact]
    public void The_scan_sees_what_it_looks_for()
    {
        Vendor().IsMatch("""Cli.Wrap("gh")""").ShouldBeTrue();
        Vendor().IsMatch("run gh issue edit 7").ShouldBeTrue();
        Vendor().IsMatch("the label status:holding").ShouldBeTrue();
        Vendor().IsMatch("a status: line, and the high band").ShouldBeFalse();
    }

    /// <summary>Walks up from the test binary to <c>src/</c>, from a worktree as readily as from the main checkout.</summary>
    static string SourceRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AiOrchestrator.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("No AiOrchestrator.slnx above the test binary.");
    }
}
