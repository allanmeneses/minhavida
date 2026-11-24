using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Circulos.Api.Data;
using Circulos.Api.Domain;
using Circulos.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Circulos.Api.Tests;

public sealed class RecordFlexTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public RecordFlexTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public void Suggester_FraseEmPtBr_SugereCamposDeVacina()
    {
        var suggester = new RuleBasedRecordSuggester();
        var gallery = RecordGallery.Seeds();
        var suggestion = suggester.SuggestType(
            "quero anotar as vacinas do meu filho, com data, posto e lote",
            [],
            gallery);
        Assert.Equal("Vacinas", suggestion.Name);
        Assert.Contains(suggestion.Fields, field => field.Kind == RecordFieldKind.Date);
        Assert.Contains(suggestion.Fields, field => field.Id == "lote" || field.Name.Contains("Lote", StringComparison.OrdinalIgnoreCase));
        var fill = suggester.SuggestRecord(
            "Tomei a vacina da hepatite B no dia 10 de março de 2024 na UBS Centro",
            [],
            gallery);
        Assert.Equal("vacinas", fill.Type.Slug);
        Assert.Equal("2024-03-10", fill.Fields["when"]);
        Assert.Contains("UBS", fill.Fields["where"] ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Suggester_NaoGravaSozinho_SoSugere()
    {
        var suggester = new RuleBasedRecordSuggester();
        var suggestion = suggester.SuggestRecord("comprei pão", [], RecordGallery.Seeds());
        Assert.False(string.IsNullOrWhiteSpace(suggestion.Summary));
        Assert.Contains("Sugerimos", suggestion.Summary);
    }

    [Fact]
    public async Task Tipo_PorFrase_CopiaGaleria_ERegistroRapido()
    {
        var client = _factory.CreateClient();
        client.WithToken((await AuthHelper.RegisterAsync(client)).Token);
        var suggested = await client.PostAsJsonAsync("/api/record-types/suggest", new
        {
            sentence = "quero anotar as vacinas do meu filho, com data, posto e lote",
        });
        Assert.Equal(HttpStatusCode.OK, suggested.StatusCode);
        using var doc = JsonDocument.Parse(await suggested.Content.ReadAsStringAsync());
        Assert.Equal("Vacinas", doc.RootElement.GetProperty("name").GetString());

        var created = await client.PostAsJsonAsync("/api/record-types", new
        {
            name = "Vacinas do Pedro",
            fields = new object[]
            {
                new { id = "title", name = "Título", kind = "text", required = true, order = 0 },
                new { id = "when", name = "Quando", kind = "date", required = true, order = 1 },
                new { id = "where", name = "Onde", kind = "place", required = false, order = 2 },
            },
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var type = await created.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(3, type.GetProperty("quickFields").GetArrayLength());
        Assert.True(type.GetProperty("quickFields").GetArrayLength() <= 3);

        var gallery = await client.GetFromJsonAsync<List<JsonElement>>("/api/record-types/gallery", Json);
        var vacinas = gallery!.Single(item => item.GetProperty("name").GetString() == "Vacinas");
        var copied = await client.PostAsJsonAsync("/api/record-types/from-gallery", new { templateId = vacinas.GetProperty("id").GetGuid() });
        Assert.Equal(HttpStatusCode.Created, copied.StatusCode);
        var copy = await copied.Content.ReadFromJsonAsync<JsonElement>(Json);
        var record = await client.PostAsJsonAsync("/api/records", new
        {
            typeId = copy.GetProperty("id").GetGuid(),
            fields = new { title = "Hepatite B", when = "2026-02-01", where = "UBS Centro" },
        });
        Assert.Equal(HttpStatusCode.Created, record.StatusCode);
        using var saved = JsonDocument.Parse(await record.Content.ReadAsStringAsync());
        Assert.Equal("Hepatite B", saved.RootElement.GetProperty("title").GetString());
        Assert.Equal("2026-02-01", saved.RootElement.GetProperty("occurredOn").GetString());
    }

    [Fact]
    public async Task CapturaLivre_AceitaOuRecusa_NadaGravaEscondido()
    {
        var client = _factory.CreateClient();
        client.WithToken((await AuthHelper.RegisterAsync(client)).Token);
        var suggest = await client.PostAsJsonAsync("/api/records/suggest", new
        {
            text = "Tomei a vacina da hepatite B no dia 10 de março de 2024 na UBS Centro",
        });
        Assert.Equal(HttpStatusCode.OK, suggest.StatusCode);
        using var suggestion = JsonDocument.Parse(await suggest.Content.ReadAsStringAsync());
        Assert.Equal("Vacinas", suggestion.RootElement.GetProperty("type").GetProperty("name").GetString());
        var typesBefore = await client.GetFromJsonAsync<List<JsonElement>>("/api/record-types", Json);
        Assert.Empty(typesBefore!);

        var rejectedStillEmpty = await client.GetFromJsonAsync<List<JsonElement>>("/api/record-types", Json);
        Assert.Empty(rejectedStillEmpty!);

        var gallery = await client.GetFromJsonAsync<List<JsonElement>>("/api/record-types/gallery", Json);
        var vacinas = gallery!.Single(item => item.GetProperty("slug").GetString() == "vacinas");
        var copy = await (await client.PostAsJsonAsync("/api/record-types/from-gallery", new { templateId = vacinas.GetProperty("id").GetGuid() }))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        var accepted = await client.PostAsJsonAsync("/api/records", new
        {
            typeId = copy.GetProperty("id").GetGuid(),
            fields = suggestion.RootElement.GetProperty("fields"),
        });
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
    }

    [Fact]
    public async Task Voz_FakeTranscriber_MesmoFluxoDeAceite_EApagaAudio()
    {
        var client = _factory.CreateClient();
        client.WithToken((await AuthHelper.RegisterAsync(client)).Token);
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes("Tomei a vacina da hepatite B no dia 10 de março de 2024 na UBS Centro"));
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        form.Add(file, "file", "teste-voz.txt");
        var transcribed = await client.PostAsync("/api/records/transcribe", form);
        Assert.Equal(HttpStatusCode.OK, transcribed.StatusCode);
        using var doc = JsonDocument.Parse(await transcribed.Content.ReadAsStringAsync());
        Assert.Contains("hepatite", doc.RootElement.GetProperty("transcript").GetString()!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Vacinas", doc.RootElement.GetProperty("type").GetProperty("name").GetString());

        using var scope = _factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<InspectableMemoryFileStorage>();
        Assert.DoesNotContain(storage.Snapshot().Keys, key => key.Contains("/audio/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Lembrete_DeCampoData_NaMesmaTransacao_SobrepostoCriaUm()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAsync(client);
        client.WithToken(auth.Token);
        var gallery = await client.GetFromJsonAsync<List<JsonElement>>("/api/record-types/gallery", Json);
        var vacinas = gallery!.Single(item => item.GetProperty("slug").GetString() == "vacinas");
        var copy = await (await client.PostAsJsonAsync("/api/record-types/from-gallery", new { templateId = vacinas.GetProperty("id").GetGuid() }))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        var created = await (await client.PostAsJsonAsync("/api/records", new
        {
            typeId = copy.GetProperty("id").GetGuid(),
            fields = new { title = "Dose anual", when = "2026-11-30", where = "UBS" },
        })).Content.ReadFromJsonAsync<JsonElement>(Json);
        var recordId = created.GetProperty("id").GetGuid();

        var a = _factory.CreateClient();
        a.WithToken(auth.Token);
        var b = _factory.CreateClient();
        b.WithToken(auth.Token);
        var responses = await Task.WhenAll(
            a.PostAsJsonAsync($"/api/records/{recordId}/remind", new { fieldId = "when" }),
            b.PostAsJsonAsync($"/api/records/{recordId}/remind", new { fieldId = "when" }));
        Assert.All(responses, item => Assert.Equal(HttpStatusCode.OK, item.StatusCode));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.Reminders.CountAsync(item => item.UserId == auth.UserId && item.Title == "Dose anual"));
        var stored = await db.LifeRecords.SingleAsync(item => item.Id == recordId);
        Assert.NotNull(stored.ReminderId);
    }

    [Fact]
    public async Task Compartilhar_RegistroNoCirculo_RespeitaMembro()
    {
        var dono = _factory.CreateClient();
        var donoAuth = await AuthHelper.RegisterAsync(dono, name: "Dono");
        dono.WithToken(donoAuth.Token);
        var fora = _factory.CreateClient();
        fora.WithToken((await AuthHelper.RegisterAsync(fora, name: "Fora")).Token);
        var gallery = await dono.GetFromJsonAsync<List<JsonElement>>("/api/record-types/gallery", Json);
        var diario = gallery!.Single(item => item.GetProperty("slug").GetString() == "diario");
        var copy = await (await dono.PostAsJsonAsync("/api/record-types/from-gallery", new { templateId = diario.GetProperty("id").GetGuid() }))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        var record = await (await dono.PostAsJsonAsync("/api/records", new
        {
            typeId = copy.GetProperty("id").GetGuid(),
            fields = new { title = "Corrida 200m", body = "hoje" },
        })).Content.ReadFromJsonAsync<JsonElement>(Json);
        var eu = (await dono.GetFromJsonAsync<List<JsonElement>>("/api/circles", Json))!
            .Single(item => item.GetProperty("name").GetString() == "Eu").GetProperty("id").GetGuid();
        var shared = await dono.PostAsJsonAsync($"/api/records/{record.GetProperty("id").GetGuid()}/share", new { circleIds = new[] { eu } });
        Assert.Equal(HttpStatusCode.OK, shared.StatusCode);
        var detail = await dono.GetFromJsonAsync<JsonElement>($"/api/circles/{eu}", Json);
        Assert.Contains(detail.GetProperty("sharedRecords").EnumerateArray(), item => item.GetProperty("title").GetString() == "Corrida 200m");

        var forbidden = await fora.PostAsJsonAsync($"/api/records/{record.GetProperty("id").GetGuid()}/share", new { circleIds = new[] { eu } });
        Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);
    }

    [Fact]
    public async Task Migracao_CopiaAreasAntigasSemPerderDado()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterWithLegacyAreasAsync(client, _factory);
        client.WithToken(auth.Token);
        var areas = await client.GetFromJsonAsync<List<JsonElement>>("/api/life-areas", Json);
        var vacinas = areas!.Single(item => item.GetProperty("kind").GetString() == "vaccine").GetProperty("id").GetGuid();
        (await client.PostAsJsonAsync($"/api/life-areas/{vacinas}/entries", new
        {
            title = "Hepatite B",
            occurredOn = "2026-02-01",
            location = "UBS Centro",
        })).EnsureSuccessStatusCode();
        var types = await client.GetFromJsonAsync<List<JsonElement>>("/api/record-types", Json);
        var migrated = types!.Single(item => item.GetProperty("name").GetString() == "Vacinas");
        var records = await client.GetFromJsonAsync<List<JsonElement>>($"/api/record-types/{migrated.GetProperty("id").GetGuid()}/records", Json);
        Assert.Contains(records!, item => item.GetProperty("title").GetString() == "Hepatite B");
        var again = await client.GetFromJsonAsync<List<JsonElement>>("/api/record-types", Json);
        Assert.Equal(types.Count, again!.Count);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.LifeEntries.CountAsync(item => item.UserId == auth.UserId && item.Title == "Hepatite B"));
        Assert.Equal(1, await db.LifeRecords.CountAsync(item => item.UserId == auth.UserId && item.Title == "Hepatite B"));
    }

    [Fact]
    public async Task CalendarioDoTipo_ListaData()
    {
        var client = _factory.CreateClient();
        client.WithToken((await AuthHelper.RegisterAsync(client)).Token);
        var gallery = await client.GetFromJsonAsync<List<JsonElement>>("/api/record-types/gallery", Json);
        var vacinas = gallery!.Single(item => item.GetProperty("slug").GetString() == "vacinas");
        var copy = await (await client.PostAsJsonAsync("/api/record-types/from-gallery", new { templateId = vacinas.GetProperty("id").GetGuid() }))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        (await client.PostAsJsonAsync("/api/records", new
        {
            typeId = copy.GetProperty("id").GetGuid(),
            fields = new { title = "Gripe", when = "2026-11-10", where = "UBS" },
        })).EnsureSuccessStatusCode();
        var calendar = await client.GetFromJsonAsync<List<JsonElement>>(
            $"/api/record-types/{copy.GetProperty("id").GetGuid()}/calendar?from=2026-11-01&to=2026-11-30",
            Json);
        Assert.Contains(calendar!, item => item.GetProperty("title").GetString() == "Gripe");
    }

    [Fact]
    public async Task CampoNovo_EmRegistroLivre_EntraNoEsquema()
    {
        var client = _factory.CreateClient();
        client.WithToken((await AuthHelper.RegisterAsync(client)).Token);
        var created = await client.PostAsJsonAsync("/api/records", new
        {
            createTypeIfMissing = true,
            newType = new
            {
                name = "Livre da mesa",
                fields = new object[]
                {
                    new { id = "title", name = "Título", kind = "text", required = true, order = 0 },
                },
            },
            fields = new { title = "Anotação solta" },
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var record = await created.Content.ReadFromJsonAsync<JsonElement>(Json);
        var typeId = record.GetProperty("typeId").GetGuid();
        var updated = await client.PostAsJsonAsync($"/api/record-types/{typeId}/fields", new { name = "Peso", kind = "number" });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        using var doc = JsonDocument.Parse(await updated.Content.ReadAsStringAsync());
        Assert.Contains(doc.RootElement.GetProperty("fields").EnumerateArray(), field => field.GetProperty("name").GetString() == "Peso");
    }

    [Fact]
    public async Task Permissao_RegistroAlheio_NaoVaza()
    {
        var dono = _factory.CreateClient();
        dono.WithToken((await AuthHelper.RegisterAsync(dono, name: "Dono")).Token);
        var fora = _factory.CreateClient();
        fora.WithToken((await AuthHelper.RegisterAsync(fora, name: "Fora")).Token);
        var created = await (await dono.PostAsJsonAsync("/api/records", new
        {
            createTypeIfMissing = true,
            newType = new { name = "Privado" },
            fields = new { title = "Segredo" },
        })).Content.ReadFromJsonAsync<JsonElement>(Json);
        var recordId = created.GetProperty("id").GetGuid();
        var typeId = created.GetProperty("typeId").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await fora.GetAsync($"/api/record-types/{typeId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await fora.PostAsJsonAsync($"/api/records/{recordId}/remind", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await fora.GetAsync($"/api/record-types/{typeId}/calendar")).StatusCode);
    }

    [Fact]
    public async Task Schema_MaximoTresCamposNaCapturaRapida()
    {
        var schema = RecordGallery.SchemaForArea(LifeAreaKind.Vaccine);
        Assert.True(schema.OrderedFields().Count > 3);
        Assert.Equal(3, schema.QuickFields().Count);
    }
}

public sealed class RecordSuggesterUnitTests
{
    [Fact]
    public async Task FakeTranscriber_LeTextoUtf8_ECaiNoPadrao()
    {
        var transcriber = new FakeTranscriber();
        await using var text = new MemoryStream(Encoding.UTF8.GetBytes("Terminei o livro O Alienista"));
        var fromText = await transcriber.TranscribeAsync(text, "nota.txt", "text/plain", CancellationToken.None);
        Assert.Contains("Alienista", fromText.Text);
        await using var binary = new MemoryStream([1, 2, 3, 4, 5, 0, 9]);
        var fromBinary = await transcriber.TranscribeAsync(binary, "voz.webm", "audio/webm", CancellationToken.None);
        Assert.Equal(FakeTranscriber.DefaultText, fromBinary.Text);
    }
}
