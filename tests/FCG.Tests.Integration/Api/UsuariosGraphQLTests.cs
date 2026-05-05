using System.Net.Http.Json;
using System.Text.Json;
using FCG.Domain.Enums;
using FCG.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FCG.Tests.Integration.Api;

public class UsuariosGraphQLTests(FcgApiFactory factory)
    : IClassFixture<FcgApiFactory>,
        IAsyncLifetime
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly FcgApiFactory _factory = factory;

    public Task InitializeAsync() => _factory.ResetarBancoAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task DeveListarUsuariosComoAdminFiltrandoPorAtivos()
    {
        await _factory.CriarUsuarioAutenticadoAsync("ativo1@fcg.com", "Ativo Um");
        await _factory.CriarUsuarioAutenticadoAsync("ativo2@fcg.com", "Ativo Dois");

        (Guid _, string adminToken) = await _factory.CriarUsuarioAutenticadoAsync(
            "admin-graphql@fcg.com",
            tipo: TipoUsuario.Administrador
        );
        HttpClient adminClient = _factory.CreateAuthenticatedClient(adminToken);

        const string query = """
            query {
              usuarios(first: 10, where: { ativo: { eq: true } }) {
                nodes { id email ativo }
                totalCount
              }
            }
            """;

        using JsonDocument? doc = await EnviarQueryAsync(adminClient, query);

        doc.Should().NotBeNull();
        JsonElement root = doc!.RootElement;
        root.TryGetProperty("errors", out _).Should().BeFalse("a query deve ter sucesso");

        JsonElement usuarios = root.GetProperty("data").GetProperty("usuarios");
        usuarios.GetProperty("totalCount").GetInt32().Should().Be(3);

        JsonElement nodes = usuarios.GetProperty("nodes");
        nodes.GetArrayLength().Should().Be(3);
        foreach (JsonElement node in nodes.EnumerateArray())
        {
            node.GetProperty("ativo").GetBoolean().Should().BeTrue();
        }
    }

    [Fact]
    public async Task DeveObterProprioUsuarioPorId()
    {
        (Guid id, string token) = await _factory.CriarUsuarioAutenticadoAsync("dono@fcg.com");
        HttpClient client = _factory.CreateAuthenticatedClient(token);

        string query = $$"""
            query {
              usuario(id: "{{id}}") {
                id
                email
              }
            }
            """;

        using JsonDocument? doc = await EnviarQueryAsync(client, query);

        doc.Should().NotBeNull();
        JsonElement root = doc!.RootElement;
        root.TryGetProperty("errors", out _).Should().BeFalse();

        JsonElement usuario = root.GetProperty("data").GetProperty("usuario");
        usuario.GetProperty("email").GetString().Should().Be("dono@fcg.com");
    }

    [Fact]
    public async Task DeveRecusarConsultaDeOutroUsuarioComoComum()
    {
        (Guid idAlvo, string _) = await _factory.CriarUsuarioAutenticadoAsync("alvo@fcg.com");
        (Guid _, string tokenOutro) = await _factory.CriarUsuarioAutenticadoAsync("outro@fcg.com");
        HttpClient client = _factory.CreateAuthenticatedClient(tokenOutro);

        string query = $$"""
            query {
              usuario(id: "{{idAlvo}}") {
                id
                email
              }
            }
            """;

        using JsonDocument? doc = await EnviarQueryAsync(client, query);

        doc.Should().NotBeNull();
        JsonElement root = doc!.RootElement;
        root.TryGetProperty("errors", out JsonElement errors).Should().BeTrue();
        errors.GetArrayLength().Should().BeGreaterThan(0);

        JsonElement primeiroErro = errors[0];
        string? code = primeiroErro.GetProperty("extensions").GetProperty("code").GetString();
        code.Should().Be("ERRO_DE_AUTENTICACAO");
    }

    [Fact]
    public async Task DeveRecusarListagemSemTokenAdmin()
    {
        // Listar admin-only: usuario comum deve ser barrado
        (Guid _, string token) = await _factory.CriarUsuarioAutenticadoAsync("comum@fcg.com");
        HttpClient client = _factory.CreateAuthenticatedClient(token);

        const string query = """
            query {
              usuarios(first: 5) {
                totalCount
              }
            }
            """;

        using JsonDocument? doc = await EnviarQueryAsync(client, query);

        doc.Should().NotBeNull();
        JsonElement root = doc!.RootElement;
        root.TryGetProperty("errors", out JsonElement errors).Should().BeTrue();
        errors.GetArrayLength().Should().BeGreaterThan(0);
    }

    private static async Task<JsonDocument?> EnviarQueryAsync(HttpClient client, string query)
    {
        HttpResponseMessage resposta = await client.PostAsJsonAsync(
            "/graphql",
            new { query },
            _jsonOptions
        );

        // GraphQL pode retornar 200 mesmo com errors no body — não validamos status code aqui.
        Stream stream = await resposta.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }
}
