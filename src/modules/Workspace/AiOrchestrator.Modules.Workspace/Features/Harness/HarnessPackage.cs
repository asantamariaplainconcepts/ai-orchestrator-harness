using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Files;
using AiOrchestrator.BuildingBlocks.Ports;
using YamlDotNet.RepresentationModel;

namespace AiOrchestrator.Modules.Workspace.Features.Harness;

/// <summary>One entry of the harness package, at its path in a repository.</summary>
/// <param name="Path">From the repository's top, with forward slashes.</param>
/// <param name="Content">The bytes to write; empty for a link.</param>
/// <param name="Executable">Written with the execute bit: the scripts and the hook.</param>
/// <param name="Target">Where a symbolic link points, from the link's directory; null for a file.</param>
sealed record HarnessFile(string Path, byte[] Content, bool Executable, string? Target);

/// <summary>
/// The harness package this build carries (#563): what <c>Features/Harness/Package/package.props</c> embeds in this
/// assembly, read back as the files and links a repository receives, and the version it says it is.
/// </summary>
static class HarnessPackage
{
    const string Prefix = "harness/";
    const string Links = "harness-links";
    const string VersionFile = ".aio/version";
    const string LoopFile = ".aio/loop.yaml";

    /// <summary>
    /// The files a repository makes its own once installed, all at the root of <c>.aio/</c> (#729,
    /// #779): its loop, its gate (#566), its issue and epic templates, which are its Definition of
    /// Ready (#633), and its config, what it captures (#709), what it measures of a change (#746) and the
    /// paths a person always reviews. A reinstall writes them only where the repository does not have
    /// them yet. Every other file of the package starts with <see cref="Managed"/> and every update
    /// overwrites it.
    /// </summary>
    public static readonly IReadOnlySet<string> Owned = new HashSet<string>(StringComparer.Ordinal)
    {
        LoopFile,
        Gate,
        Config,
        ".aio/templates/issue.md",
        ".aio/templates/epic.md",
    };

    /// <summary>The gate, the one script at the root the package writes: the repository owns it.</summary>
    public const string Gate = ".aio/gate";

    /// <summary>What three steps read from the repository: evidence, quality and the protected paths (#779).</summary>
    public const string Config = ".aio/config.json";

    /// <summary>The line a file of the package starts with, after its shebang when it has one.</summary>
    public const string Managed = "Managed by aio";

    /// <summary>The root of the package's folder, which holds the repository's files too.</summary>
    const string Root = ".aio/";

    /// <summary>Files of the repository's that the package does not carry and that move out of <c>.harness/</c>.</summary>
    public static readonly IReadOnlyList<BranchMove> Moves = [new(".harness/context.md", ".aio/context.md")];

    /// <summary>
    /// What an update over harness 36 or older takes out once everything has moved: the package's old
    /// folders, with whatever else a person left in them, and the three files, and the config that
    /// <see cref="Fold"/> reads, that <c>.aio/config.json</c> gathers.
    /// </summary>
    public static readonly IReadOnlyList<string> Removed =
    [
        ".harness/aio",
        ".harness/scripts",
        ".harness/evidence.json",
        ".harness/quality.json",
        ".harness/protected-paths",
        ".harness/config.json",
    ];

    /// <summary>
    /// Where a file of the package was before, nearest layout first, so an update over an old layout
    /// moves it and leaves no copy behind. Harness 37 (#779) moved everything from <c>.harness/</c>
    /// to <c>.aio/</c>: the root's files stayed at the root, the package's steps went from
    /// <c>.harness/aio/</c> to <c>steps/</c>, and <c>checks/</c>, <c>lib/</c>, <c>schemas/</c> and
    /// <c>scorers/</c> rose one level. Before harness 36 (#759) a tool was straight in <c>.harness/aio/</c>,
    /// and before harness 24 (#729) the scripts were in <c>.harness/scripts/</c> and <c>lib/</c> and
    /// <c>schemas/</c> at the root. Empty for a file that has not moved.
    /// </summary>
    public static IReadOnlyList<string> Former(string path)
    {
        if (!path.StartsWith(Root, StringComparison.Ordinal) || path == Config)
        {
            return [];
        }

        var rest = path[Root.Length..];
        var name = rest[(rest.LastIndexOf('/') + 1)..];
        return rest switch
        {
            "gate" => [".harness/gate", ".harness/scripts/gate"],
            "aio" => [".harness/aio/aio", ".harness/scripts/aio"],
            _ when rest.StartsWith("steps/", StringComparison.Ordinal) => [$".harness/aio/{name}", $".harness/scripts/{name}"],
            _ when rest.StartsWith("scorers/", StringComparison.Ordinal) => [$".harness/aio/{rest}", $".harness/aio/{name}"],
            _ when rest.StartsWith("checks/", StringComparison.Ordinal) => [$".harness/aio/{rest}"],
            _ when rest.StartsWith("lib/", StringComparison.Ordinal) || rest.StartsWith("schemas/", StringComparison.Ordinal) => [$".harness/aio/{rest}", $".harness/{rest}"],
            _ => [$".harness/{rest}"],
        };
    }

    /// <summary>
    /// The scripts and the hook: the gate, <c>aio</c>, a file with no extension in <c>steps/</c> or in
    /// a tool's folder <c>scorers/&lt;tool&gt;/</c>, and <c>.claude/hooks/</c>.
    /// </summary>
    static bool Executable(string path) =>
        path == Gate
        || path == ".aio/aio"
        || path.StartsWith(".claude/hooks/", StringComparison.Ordinal)
        || (path.StartsWith(".aio/scorers/", StringComparison.Ordinal) && Script(path[".aio/scorers/".Length..].Split('/') is [_, var tool] ? tool : "/"))
        || (path.StartsWith(".aio/steps/", StringComparison.Ordinal) && Script(path[".aio/steps/".Length..]));

    /// <summary>A name with no folder and no extension.</summary>
    static bool Script(string name) =>
        !name.Contains('/', StringComparison.Ordinal) && !name.Contains('.', StringComparison.Ordinal);

    /// <summary>
    /// <c>.aio/config.json</c> for a repository that still has harness 36's three files (and, maybe, the
    /// <c>.harness/config.json</c> of <c>feedback</c>): <c>evidence</c>, <c>quality</c> and
    /// <c>protected</c> hold what each had, comments included, and any other key of the old config
    /// stays as it was. Null when there is none of them, which writes the example instead.
    /// <paramref name="read"/> answers a file of the default branch by its path, or null.
    /// </summary>
    public static byte[]? Fold(Func<string, string?> read)
    {
        var evidence = read(".harness/evidence.json");
        var quality = read(".harness/quality.json");
        var protectedPaths = read(".harness/protected-paths");
        var config = read(".harness/config.json");
        if (new[] { evidence, quality, protectedPaths, config }.All(string.IsNullOrWhiteSpace))
        {
            return null;
        }

        var parts = new List<string>();
        AddObject(parts, "evidence", evidence);
        AddObject(parts, "quality", quality);
        if (!string.IsNullOrWhiteSpace(protectedPaths))
        {
            parts.Add(Patterns(protectedPaths));
        }

        foreach (var (key, value) in Others(config))
        {
            parts.Add($"  {Json(key)}: {value}");
        }

        return Encoding.UTF8.GetBytes(
            "// Gathered by Install harness from this repository's .harness/evidence.json, quality.json, protected-paths and\n"
                + "// config.json. Whole lines starting with // are comments.\n{\n"
                + string.Join(",\n\n", parts)
                + "\n}\n"
        );
    }

    /// <summary>One key whose value is the old file's object: its leading comment lines go above the key, the rest below it.</summary>
    static void AddObject(List<string> parts, string key, string? old)
    {
        if (string.IsNullOrWhiteSpace(old))
        {
            return;
        }

        var lines = old.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd().Split('\n');
        var body = lines.SkipWhile(line => line.Trim().Length == 0 || line.TrimStart().StartsWith("//", StringComparison.Ordinal)).ToList();
        var comments = lines.Take(lines.Length - body.Count).Where(line => line.Trim().Length > 0);
        parts.Add(
            string.Concat(comments.Select(line => $"  {line.TrimStart()}\n"))
                + $"  {Json(key)}: "
                + string.Join("\n  ", body).TrimStart()
        );
    }

    /// <summary>The old <c>protected-paths</c> as the array <c>protected</c>: a pattern per line, <c>#</c> comments becoming <c>//</c> ones.</summary>
    static string Patterns(string old)
    {
        var lines = old.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Select(line => line.Trim()).ToList();
        var last = lines.FindLastIndex(line => line.Length > 0 && !line.StartsWith('#'));
        var rows = new List<string>();
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            if (line.Length == 0)
            {
                rows.Add("");
            }
            else if (line.StartsWith('#'))
            {
                rows.Add($"    //{line.TrimStart('#')}");
            }
            else
            {
                rows.Add($"    {Json(line.Split('#', 2)[0].Trim())}{(index == last ? "" : ",")}");
            }
        }

        return $"  \"protected\": [\n{string.Join("\n", rows).TrimEnd()}\n  ]";
    }

    /// <summary>The keys of the old <c>config.json</c> the new one does not name, with their values as written.</summary>
    static IEnumerable<(string Key, string Value)> Others(string? old)
    {
        if (string.IsNullOrWhiteSpace(old))
        {
            yield break;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(old);
        }
        catch (JsonException)
        {
            yield break;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                yield break;
            }

            foreach (var property in document.RootElement.EnumerateObject().Where(property => property.Name is not ("evidence" or "quality" or "protected")))
            {
                yield return (property.Name, property.Value.GetRawText());
            }
        }
    }

    static string Json(string text) => JsonSerializer.Serialize(text, Relaxed);

    static readonly JsonSerializerOptions Relaxed = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    static readonly Lazy<IReadOnlyList<HarnessFile>> Carried = new(Read);

    /// <summary>The package's every entry, ordered by path.</summary>
    public static IReadOnlyList<HarnessFile> Files => Carried.Value;

    /// <summary>The first line of <c>.aio/version</c>, which an installed repository keeps too.</summary>
    public static string Version =>
        Encoding.UTF8.GetString(Files.Single(file => file.Path == VersionFile).Content).Trim();

    /// <summary>
    /// Every state the package's loop names, in the order the file names them: each state it
    /// declares, then each target no state declares, such as <c>done</c>. Installing creates them at
    /// the vendor, so the first move of a PBI finds its state there.
    /// </summary>
    public static IReadOnlyList<string> States => LoopStates.Value;

    static readonly Lazy<IReadOnlyList<string>> LoopStates = new(() =>
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(Encoding.UTF8.GetString(Files.Single(file => file.Path == LoopFile).Content)));
        var states = (YamlMappingNode)((YamlMappingNode)stream.Documents[0].RootNode).Children[new YamlScalarNode("states")];

        var named = states.Children.Keys.Select(name => ((YamlScalarNode)name).Value!).ToList();
        foreach (var body in states.Children.Values.OfType<YamlMappingNode>())
        {
            named.AddRange(Targets(body));
        }
        return [.. named.Distinct(StringComparer.Ordinal)];
    });

    /// <summary>Where a state goes: its <c>next</c>, each outcome of its <c>on</c>, and a hold's <c>next</c>.</summary>
    static IEnumerable<string> Targets(YamlMappingNode state)
    {
        foreach (var (key, value) in state.Children)
        {
            switch (((YamlScalarNode)key).Value, value)
            {
                case ("next", YamlScalarNode next):
                    yield return next.Value!;
                    break;
                case ("on", YamlMappingNode outcomes):
                    foreach (var target in outcomes.Children.Values)
                    {
                        if (target is YamlScalarNode to)
                        {
                            yield return to.Value!;
                        }
                        else if (target is YamlMappingNode hold && hold.Children.TryGetValue(new YamlScalarNode("next"), out var after))
                        {
                            yield return ((YamlScalarNode)after).Value!;
                        }
                    }
                    break;
            }
        }
    }

    static List<HarnessFile> Read()
    {
        var assembly = typeof(HarnessPackage).Assembly;
        var files = assembly
            .GetManifestResourceNames()
            .Where(name => name.StartsWith(Prefix, StringComparison.Ordinal))
            .Select(name =>
            {
                var path = name[Prefix.Length..];
                return new HarnessFile(
                    path,
                    Bytes(assembly.GetManifestResourceStream(name)!),
                    Executable(path),
                    null
                );
            })
            .ToList();

        var links = Encoding.UTF8.GetString(Bytes(assembly.GetManifestResourceStream(Links)!));
        foreach (var line in links.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith('#'))
            {
                continue;
            }
            var parts = line.Split(" -> ", 2, StringSplitOptions.TrimEntries);
            files.Add(new HarnessFile(parts[0], [], false, parts[1]));
        }

        files.Sort((left, right) => string.CompareOrdinal(left.Path, right.Path));
        return files;
    }

    static byte[] Bytes(Stream stream)
    {
        using (stream)
        using (var copy = new MemoryStream())
        {
            stream.CopyTo(copy);
            return copy.ToArray();
        }
    }
}
