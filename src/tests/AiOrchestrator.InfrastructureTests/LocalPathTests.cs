using AiOrchestrator.BuildingBlocks.Folders;

namespace AiOrchestrator.InfrastructureTests;

public sealed class LocalPathTests : IDisposable
{
    readonly string _directory = Directory.CreateTempSubdirectory("aio-local-path-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void A_path_through_a_linked_directory_is_the_same_as_its_target()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        // The shape of macOS's /var → /private/var: the link sits above the folder, not on it.
        var real = Directory.CreateDirectory(Path.Combine(_directory, "private", "var")).FullName;
        var link = Path.Combine(_directory, "var");
        Directory.CreateSymbolicLink(link, real);
        Directory.CreateDirectory(Path.Combine(real, "repo"));

        LocalPath.Same(Path.Combine(link, "repo"), Path.Combine(real, "repo") + "/").ShouldBeTrue();
        LocalPath.Same(Path.Combine(link, "repo"), Path.Combine(real, "other")).ShouldBeFalse();
    }

    [Fact]
    public void A_path_that_does_not_exist_compares_as_written()
    {
        var missing = Path.Combine(_directory, "nowhere", "repo");

        LocalPath.Same(missing, missing + Path.DirectorySeparatorChar).ShouldBeTrue();
        LocalPath.Same(missing, Path.Combine(_directory, "nowhere", "other")).ShouldBeFalse();
    }
}
