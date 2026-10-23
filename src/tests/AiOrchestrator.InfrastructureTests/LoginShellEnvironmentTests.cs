using AiOrchestrator.Infrastructure;

namespace AiOrchestrator.InfrastructureTests;

/// <summary>
/// A fake <c>$SHELL</c> stands in for the user's: a script that talks around the question the way
/// a real interactive startup does, then answers it with a PATH it chose. POSIX only, like
/// <see cref="ShellQuotingTests"/> — Windows never asks.
/// </summary>
public sealed class LoginShellEnvironmentTests : IDisposable
{
    readonly string _directory = Directory.CreateTempSubdirectory("aio-login-shell-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void The_path_between_the_markers_is_the_answer_whatever_the_startup_prints()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        // $4 is the script after `-i -l -c`; a banner before it and a goodbye after it are the
        // noise a motd or a version manager makes.
        var shell = FakeShell(
            """
            echo "Welcome to fake shell"
            echo "motd: PATH=/not/this" >&2
            PATH=/opt/fake/bin:/usr/bin:/bin
            eval "$4"
            echo "logout"
            """
        );

        LoginShellEnvironment.Ask(shell, TimeSpan.FromSeconds(10)).ShouldBe("/opt/fake/bin:/usr/bin:/bin");
    }

    [Fact]
    public void A_shell_that_never_answers_recovers_nothing()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var shell = FakeShell("exec sleep 30");

        LoginShellEnvironment.Ask(shell, TimeSpan.FromSeconds(1)).ShouldBeNull();
    }

    [Fact]
    public void A_shell_that_prints_no_markers_recovers_nothing()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var shell = FakeShell("echo /usr/bin:/bin");

        LoginShellEnvironment.Ask(shell, TimeSpan.FromSeconds(10)).ShouldBeNull();
    }

    [Fact]
    public void A_shell_that_is_not_there_recovers_nothing()
    {
        LoginShellEnvironment
            .Ask(Path.Combine(_directory, "no-such-shell"), TimeSpan.FromSeconds(10))
            .ShouldBeNull();
    }

    // #475. The shell asked is the account's, read from the user database, because $SHELL is
    // whatever the parent passed down — the installer's sudo passed /bin/sh.
    [Fact]
    public void The_directory_answer_names_the_shell()
    {
        LoginShellEnvironment.FromDirectory("UserShell: /bin/zsh\n").ShouldBe("/bin/zsh");
    }

    [Fact]
    public void A_directory_answer_with_no_shell_names_none()
    {
        LoginShellEnvironment.FromDirectory("No such key: UserShell\n").ShouldBeNull();
    }

    [Fact]
    public void The_passwd_line_of_that_user_names_the_shell()
    {
        const string passwd = """
            root:x:0:0:root:/root:/bin/bash
            pat:x:1000:1000:Pat:/home/pat:/usr/bin/fish
            """;

        LoginShellEnvironment.FromPasswd(passwd, "pat").ShouldBe("/usr/bin/fish");
        LoginShellEnvironment.FromPasswd(passwd, "nobody").ShouldBeNull();
    }

    [Fact]
    public void This_account_has_a_recorded_shell_that_exists()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var shell = LoginShellEnvironment.RecordedShell(Environment.UserName);

        shell.ShouldNotBeNull();
        File.Exists(shell).ShouldBeTrue();
    }

    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    string FakeShell(string body)
    {
        var path = Path.Combine(_directory, "fake-shell");
        File.WriteAllText(path, $"#!/bin/sh\n{body}\n");
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return path;
    }
}
