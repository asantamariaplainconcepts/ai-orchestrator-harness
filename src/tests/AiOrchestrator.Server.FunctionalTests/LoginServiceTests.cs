using System.Xml.Linq;
using AiOrchestrator.Server.Installed;

namespace AiOrchestrator.Server.FunctionalTests;

/// <summary>
/// #703. <c>aio service install|uninstall|status</c> against a home of its own and a fake
/// <c>launchctl</c> and <c>systemctl</c>: what the file says, what is run, and what Windows answers.
/// Nothing here reaches the real launchd.
/// </summary>
public sealed class LoginServiceTests : IDisposable
{
    const string Label = "aio.test";
    const string Command = "/Users/someone/.dotnet/tools/aio";

    readonly string home = Directory.CreateTempSubdirectory("aio-service-").FullName;
    readonly List<string> calls = [];

    /// <summary>launchd as a fake: <c>id -u</c> is 501, the label is not loaded, bootstrap and bootout work.</summary>
    CommandResult Launchd(string file, params string[] arguments)
    {
        var call = string.Join(' ', [file, .. arguments]);
        calls.Add(call);

        return call switch
        {
            "id -u" => new CommandResult(0, "501\n"),
            _ when call.StartsWith("launchctl print", StringComparison.Ordinal) => new CommandResult(113, "Could not find service"),
            _ => new CommandResult(0, ""),
        };
    }

    LoginService Service(ServiceHost host, string? command = Command) =>
        new(host, home, Label, command, new Dictionary<string, string> { ["HOME"] = home, [LoginService.ServiceVariable] = Label }, Launchd);

    [Fact]
    public void Install_writes_a_LaunchAgent_that_starts_the_tool_at_login_and_again_when_it_fails()
    {
        using var output = new StringWriter();

        Service(ServiceHost.MacOS).Answer("install", () => null, "http://127.0.0.1:48117", output, TextWriter.Null).ShouldBe(0);

        var file = Path.Combine(home, "Library", "LaunchAgents", $"{Label}.plist");
        output.ToString().ShouldContain(file);

        var plist = Read(file);
        plist["Label"].Value.ShouldBe(Label);
        plist["ProgramArguments"].Elements("string").Select(argument => argument.Value).ShouldBe([Command]);
        plist["RunAtLoad"].Name.LocalName.ShouldBe("true");
        // A clean exit (Stop console, a newer version taking over) stays stopped; a crash does not.
        var keepAlive = plist["KeepAlive"].Elements().ToList();
        keepAlive.Select(element => element.Name.LocalName).ShouldBe(["key", "false"]);
        keepAlive[0].Value.ShouldBe("SuccessfulExit");
        plist["StandardOutPath"].Value.ShouldBe(Path.Combine(home, ".aiorchestrator", "server.log"));
        plist["StandardErrorPath"].Value.ShouldBe(Path.Combine(home, ".aiorchestrator", "server.log"));
        plist["EnvironmentVariables"].Elements("string").Select(value => value.Value).ShouldContain(Label);

        calls.ShouldBe(["id -u", $"launchctl print gui/501/{Label}", $"launchctl bootstrap gui/501 {file}"]);
    }

    [Fact]
    public void Install_leaves_a_service_already_loaded_running()
    {
        var loaded = new LoginService(ServiceHost.MacOS, home, Label, Command, new Dictionary<string, string>(), (file, arguments) =>
        {
            calls.Add(string.Join(' ', [file, .. arguments]));
            return new CommandResult(0, file == "id" ? "501" : "");
        });

        loaded.Install().Succeeded.ShouldBeTrue();

        calls.ShouldNotContain(call => call.StartsWith("launchctl bootstrap", StringComparison.Ordinal));
    }

    [Fact]
    public void Uninstall_unloads_it_which_stops_the_process_and_deletes_the_file()
    {
        var service = Service(ServiceHost.MacOS);
        service.Install();
        calls.Clear();
        using var output = new StringWriter();

        service.Answer("uninstall", () => null, "http://127.0.0.1:48117", output, TextWriter.Null).ShouldBe(0);

        File.Exists(service.Definition).ShouldBeFalse();
        calls.ShouldBe(["id -u", $"launchctl bootout gui/501/{Label}"]);
        output.ToString().ShouldContain("no longer starts at login");
    }

    [Fact]
    public void Turned_off_from_the_service_itself_only_the_file_goes()
    {
        var service = Service(ServiceHost.MacOS);
        service.Install();
        calls.Clear();

        service.Uninstall(stop: false).Succeeded.ShouldBeTrue();

        File.Exists(service.Definition).ShouldBeFalse();
        calls.ShouldBeEmpty();
    }

    [Fact]
    public void Uninstall_without_the_file_still_unloads_what_Settings_left_loaded()
    {
        using var output = new StringWriter();

        Service(ServiceHost.MacOS).Answer("uninstall", () => null, "http://127.0.0.1:48117", output, TextWriter.Null).ShouldBe(0);

        calls.ShouldBe(["id -u", $"launchctl bootout gui/501/{Label}"]);
        output.ToString().ShouldContain("was not registered to start at login");
    }

    [Fact]
    public void Status_says_whether_it_is_registered_and_which_version_answers()
    {
        var service = Service(ServiceHost.MacOS);
        using var before = new StringWriter();
        service.Answer("status", () => null, "http://127.0.0.1:48117", before, TextWriter.Null).ShouldBe(0);
        before.ToString().ShouldContain("Not registered to start at login.");
        before.ToString().ShouldContain("Nothing answers at http://127.0.0.1:48117.");

        service.Install();
        using var after = new StringWriter();
        service.Answer("status", () => "0.3.3", "http://127.0.0.1:48117", after, TextWriter.Null).ShouldBe(0);
        after.ToString().ShouldContain($"Registered to start at login: {service.Definition}");
        after.ToString().ShouldContain("aio 0.3.3 answers at http://127.0.0.1:48117.");
    }

    [Fact]
    public void On_Windows_install_says_it_is_not_supported_yet_and_exits_2()
    {
        using var error = new StringWriter();

        Service(ServiceHost.Windows).Answer("install", () => null, "http://127.0.0.1:48117", TextWriter.Null, error).ShouldBe(2);

        error.ToString().ShouldContain("not supported on Windows yet");
        calls.ShouldBeEmpty();
    }

    [Fact]
    public void Started_with_dnx_there_is_no_tool_to_register_and_it_exits_2()
    {
        using var error = new StringWriter();

        Service(ServiceHost.MacOS, command: null).Answer("install", () => null, "http://127.0.0.1:48117", TextWriter.Null, error).ShouldBe(2);

        error.ToString().ShouldContain("dotnet tool install -g aio");
        Directory.Exists(Path.Combine(home, "Library")).ShouldBeFalse();
    }

    [Fact]
    public void Another_verb_is_a_usage_error()
    {
        using var error = new StringWriter();

        Service(ServiceHost.MacOS).Answer("start", () => null, "http://127.0.0.1:48117", TextWriter.Null, error).ShouldBe(2);

        error.ToString().ShouldContain("install, uninstall or status");
    }

    [Fact]
    public void On_Linux_it_is_a_systemd_user_unit_restarted_on_failure()
    {
        var service = Service(ServiceHost.Linux);

        service.Install().Succeeded.ShouldBeTrue();

        service.Definition.ShouldBe(Path.Combine(home, ".config", "systemd", "user", $"{Label}.service"));
        var unit = File.ReadAllText(service.Definition);
        unit.ShouldContain($"ExecStart=\"{Command}\"");
        unit.ShouldContain("Restart=on-failure");
        unit.ShouldContain($"StandardOutput=append:{Path.Combine(home, ".aiorchestrator", "server.log")}");
        unit.ShouldContain("WantedBy=default.target");
        calls.ShouldBe(["systemctl --user daemon-reload", $"systemctl --user enable --now {Label}.service"]);

        calls.Clear();
        service.Uninstall(stop: true);
        File.Exists(service.Definition).ShouldBeFalse();
        calls.ShouldBe([$"systemctl --user disable --now {Label}.service", "systemctl --user daemon-reload"]);
    }

    [Fact]
    public void The_tool_typed_with_service_reaches_it()
    {
        // The test host is no tool, so it is refused as dnx would be, and the label is not touched.
        using var error = new StringWriter();

        Installation.Answer(["service", "status"], TextWriter.Null, error).ShouldBe(2);

        error.ToString().ShouldContain("aio service:");
    }

    /// <summary>The plist's top dictionary, each key's value element by the key's name.</summary>
    static Dictionary<string, XElement> Read(string file)
    {
        var entries = XDocument.Load(file).Root!.Element("dict")!.Elements().ToList();
        return Enumerable.Range(0, entries.Count / 2).ToDictionary(index => entries[index * 2].Value, index => entries[index * 2 + 1]);
    }

    public void Dispose() => Directory.Delete(home, recursive: true);
}
