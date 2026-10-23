using System.Net;
using System.Net.Http.Json;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.Modules.Loop.Engine;

namespace AiOrchestrator.Modules.Loop.FunctionalTests;

/// <summary>
/// #708. A sub-issue waits for the ones its epic's graph puts before it. On aio-dogfood#29 #32 and #33
/// were labelled by hand once #31 reached done, because nothing else would have kept them back: they
/// use what #31 builds without sharing a file with it in flight.
/// </summary>
static class EpicOrder
{
    /// <summary>The body of aio-dogfood#29 as it was closed on 14 October 2026.</summary>
    public const string Dogfood29 = """
        ## Objetivo

        Que cada día de la previsión tenga su propia página: se abre desde su tarjeta, se puede enlazar y enseña algo que la lista no enseña, la humedad. Hoy la app es una sola vista con valores aleatorios en cada llamada, así que un día no se puede ni enlazar ni volver a pedir igual. Es también la vuelta completa del orquestador 0.3.2 en este repositorio: cuatro sub-issues con `wip: 4`, implement en Docker sandbox, y el journey y las grabaciones en el vendor.

        ## El grafo

        ```mermaid
        flowchart LR
          e29[El detalle de un día]
          i30[#30 · la API devuelve un día suelto]
          i31[#31 · el componente del detalle]
          i32[#32 · la tarjeta abre el detalle]
          i33[#33 · copiar el enlace del día]
          e29 --> i30
          e29 --> i31
          i31 --> i32
          i31 --> i33
        ```

        Una flecha es "va después de". #32 usa el componente de #31; #33 toca `DayDetail.tsx` y `DayDetail.css`, que crea #31. #30 y #31 van a la vez: el contrato de la API está escrito en las dos.

        ## Sub-issues

        | Issue | Qué | Dónde cae |
        | --- | --- | --- |
        | #30 | `GET /api/weatherforecast/{date}`, estable por fecha, 404 fuera de rango | `AioDogfood.Server/Program.cs`, `AioDogfood.Tests/` |
        | #31 | `DayDetail` con temperatura, resumen y humedad | `frontend/src/DayDetail.tsx`, `DayDetail.css` |
        | #32 | La tarjeta abre el detalle con `?day=` | `frontend/src/App.tsx`, `App.css` |
        | #33 | Botón "Copiar enlace" en el detalle | `frontend/src/DayDetail.tsx`, `DayDetail.css` |

        ## Criterios de aceptación

        - [ ] Abrir `/?day=<mañana>` enseña el detalle de mañana, y dos visitas enseñan lo mismo.
        - [ ] Cada sub-issue tiene su comentario `aio:journey` y sus grabaciones en la rama `aio/evidence`.
        - [ ] El motor cierra esta épica cuando cierra la última.

        ## Lo que no es

        No añade datos reales de tiempo ni un router.
        """;

    public sealed record Line(string Kind, string From, string? To, string Why);

    public static void Epic(LoopFixture fixture, string id, string body)
    {
        fixture.Vendor.Pbis.Add(new Pbi(new PbiRef(FakeFolders.Repository, id), $"PBI {id}", null, false, null));
        fixture.Vendor.Bodies[id] = body;
    }

    public static void Child(LoopFixture fixture, string id, string parent)
    {
        fixture.Vendor.Pbis.Add(new Pbi(new PbiRef(FakeFolders.Repository, id), $"PBI {id}", "ready-for-proposal", false, null));
        fixture.Vendor.Parents[id] = parent;
    }

    public static void Close(LoopFixture fixture, string id)
    {
        var index = fixture.Vendor.Pbis.FindIndex(pbi => pbi.Ref.Id == id);
        fixture.Vendor.Pbis[index] = fixture.Vendor.Pbis[index] with { Closed = true };
    }

    public static string[] Started(LoopFixture fixture) => [.. fixture.Runs.Started.Select(run => run.Request.Pbi.Id)];

    public static async Task RunOnce(HttpClient client)
    {
        using var response = await client.PostAsync("/api/loop/run-once", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    /// <summary>A PBI's journal, oldest first.</summary>
    public static async Task<List<Line>> Journal(HttpClient client, string id)
    {
        var lines = await client.GetFromJsonAsync<List<Line>>($"/api/loop/journal?repository={FakeFolders.Repository}&pbi={id}");
        lines.ShouldNotBeNull().Reverse();
        return lines;
    }
}

public sealed class EpicOrderWaitTests(LoopFixture fixture) : IClassFixture<LoopFixture>
{
    [Fact]
    public async Task The_four_sub_issues_of_aio_dogfood_29_labelled_at_once_start_in_the_order_of_its_graph()
    {
        EpicOrder.Epic(fixture, "29", EpicOrder.Dogfood29);
        foreach (var id in new[] { "30", "31", "32", "33" })
        {
            EpicOrder.Child(fixture, id, "29");
        }

        using var client = fixture.CreateClient();
        await EpicOrder.RunOnce(client);
        await EpicOrder.RunOnce(client);

        EpicOrder.Started(fixture).ShouldBe(["30", "31"], "e29 --> i30 and e29 --> i31 make nobody wait");
        foreach (var id in new[] { "32", "33" })
        {
            var waits = await EpicOrder.Journal(client, id);
            waits.Select(line => (line.Kind, line.Why)).ShouldBe([("waited", "#31 goes first (the epic's graph)")], "one line however many passes it lasts");
        }

        // #31 merges and closes; the next pass starts both that came after it.
        EpicOrder.Close(fixture, "31");
        await EpicOrder.RunOnce(client);

        EpicOrder.Started(fixture).ShouldBe(["30", "31", "32", "33"]);
        (await EpicOrder.Journal(client, "32")).Last().Kind.ShouldBe("started");
        (await EpicOrder.Journal(client, "33")).Last().Kind.ShouldBe("started");
    }
}

public sealed class EpicOrderNoGraphTests(LoopFixture fixture) : IClassFixture<LoopFixture>
{
    [Fact]
    public async Task Sub_issues_of_an_epic_with_no_graph_both_start()
    {
        EpicOrder.Epic(fixture, "40", "Sub-issues #41 and #42, in any order.");
        EpicOrder.Child(fixture, "41", "40");
        EpicOrder.Child(fixture, "42", "40");

        using var client = fixture.CreateClient();
        await EpicOrder.RunOnce(client);

        EpicOrder.Started(fixture).ShouldBe(["41", "42"]);
        (await EpicOrder.Journal(client, "42")).ShouldHaveSingleItem().Why.ShouldNotContain("graph");
    }
}

public sealed class EpicOrderBrokenGraphTests(LoopFixture fixture) : IClassFixture<LoopFixture>
{
    const string Unread = "the graph of #60 was not read, so nothing waits on it: \"i61 --> #62\" is not an arrow between e<n> and i<n> nodes";

    [Fact]
    public async Task A_graph_that_cannot_be_read_holds_nobody_back_and_the_journal_says_so_once()
    {
        EpicOrder.Epic(fixture, "60", "```mermaid\nflowchart LR\n  e60 --> i61\n  i61 --> #62\n```\n");
        EpicOrder.Child(fixture, "61", "60");
        EpicOrder.Child(fixture, "62", "60");

        using var client = fixture.CreateClient();
        await EpicOrder.RunOnce(client);

        EpicOrder.Started(fixture).ShouldBe(["61", "62"]);
        var started = (await EpicOrder.Journal(client, "62")).ShouldHaveSingleItem();
        started.Kind.ShouldBe("started");
        started.Why.ShouldEndWith($"; {Unread}");

        // #62's refine says ready, and its next run, the proposal, starts without saying it again.
        fixture.Scripts.Answers["file-overlap"] = (0, """{"clear": true, "why": "no file shared with a PBI in flight."}""");
        fixture.Scripts.Answers["claim"] = (0, """{"claimed": true, "why": "change/62 pushed."}""");
        fixture.Runs.End(1, "ready", "The issue meets the Definition of Ready.");
        await EpicOrder.RunOnce(client);
        await EpicOrder.RunOnce(client);

        var lines = await EpicOrder.Journal(client, "62");
        lines.Count(line => line.Kind == "started" && line.Why.StartsWith("asked for", StringComparison.Ordinal)).ShouldBe(2, "the refine run and the proposal run");
        lines.Count(line => line.Why.Contains(Unread, StringComparison.Ordinal)).ShouldBe(1);
    }
}

/// <summary>#708. What <see cref="LoopEngine.ReadGraph"/> reads from an epic's body.</summary>
public sealed class EpicGraphReadTests
{
    [Fact]
    public void The_arrows_of_aio_dogfood_29_are_read_with_their_labels_left_out()
    {
        var graph = LoopEngine.ReadGraph(EpicOrder.Dogfood29);

        graph.Problem.ShouldBeNull();
        graph.Arrows.ShouldBe([("e29", "i30"), ("e29", "i31"), ("i31", "i32"), ("i31", "i33")]);
    }

    [Fact]
    public void A_chain_with_labelled_nodes_and_an_edge_label_is_each_of_its_arrows()
    {
        var graph = LoopEngine.ReadGraph("~~~mermaid\ngraph TD\n%% a comment\n  i1[#1 · one] -->|uses| i2(#2) --> i3;\n  i3 --- i4\n~~~");

        graph.Problem.ShouldBeNull();
        graph.Arrows.ShouldBe([("i1", "i2"), ("i2", "i3")]);
    }

    [Theory]
    [InlineData("No graph here, only #1 and #2.", null)]
    [InlineData("```mermaid\nflowchart LR\n  i1 --> i2\n", "its mermaid block is not closed")]
    [InlineData("```mermaid\nsequenceDiagram\n  i1 ->> i2: hi\n```", "its mermaid block is not a flowchart")]
    [InlineData("```mermaid\nflowchart LR\n  A --> B\n```", "\"A --> B\" is not an arrow between e<n> and i<n> nodes")]
    public void A_body_with_no_block_has_no_arrows_and_a_broken_one_says_why(string body, string? problem)
    {
        var graph = LoopEngine.ReadGraph(body);

        graph.Arrows.ShouldBeEmpty();
        graph.Problem.ShouldBe(problem);
    }
}
