using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FCG.Application.DTOs;
using FCG.Application.Interfaces;
using FCG.Domain.Entities;
using FCG.Domain.ValueObjects;
using FCG.Infrastructure.Persistence;
using FCG.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace FCG.Tests.Integration.Api;

public class AuthEndpointsTests : IClassFixture<FcgApiFactory>, IAsyncLifetime
{
    private const string EmailUsuario = "login@fcg.com";
    private const string SenhaUsuario = "Senha@123";
    private const string NomeUsuario = "Usuario Login";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly FcgApiFactory _factory;
    private readonly HttpClient _client;

    public AuthEndpointsTests(FcgApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
    }

    public Task InitializeAsync() => _factory.ResetarBancoAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task DeveRetornar200ELoginResponseComAccessTokenQuandoCredenciaisValidas()
    {
        await CadastrarUsuarioAsync();

        HttpResponseMessage resposta = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(EmailUsuario, SenhaUsuario)
        );

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        LoginResponse? body = await resposta.Content.ReadFromJsonAsync<LoginResponse>(_jsonOptions);
        body.Should().NotBeNull();
        body!.AccessToken.Should().NotBeNullOrWhiteSpace();
        body.TokenType.Should().Be("Bearer");
        body.ExpiresIn.Should().BePositive();
        body.RefreshToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task DeveRetornar401QuandoEmailNaoCadastrado()
    {
        HttpResponseMessage resposta = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("inexistente@fcg.com", SenhaUsuario)
        );

        await AssertRespostaCredenciaisInvalidasAsync(resposta);
    }

    [Fact]
    public async Task DeveRetornar401QuandoSenhaIncorreta()
    {
        await CadastrarUsuarioAsync();

        HttpResponseMessage resposta = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(EmailUsuario, "Errada@123")
        );

        await AssertRespostaCredenciaisInvalidasAsync(resposta);
    }

    [Fact]
    public async Task DeveRetornar401QuandoUsuarioDesativado()
    {
        await CadastrarUsuarioAsync();
        await DesativarUsuarioNoBancoAsync(EmailUsuario);

        HttpResponseMessage resposta = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(EmailUsuario, SenhaUsuario)
        );

        await AssertRespostaCredenciaisInvalidasAsync(resposta);
    }

    [Fact]
    public async Task DeveRetornar401QuandoEmailMalFormatado()
    {
        HttpResponseMessage resposta = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("nao-eh-email", SenhaUsuario)
        );

        await AssertRespostaCredenciaisInvalidasAsync(resposta);
    }

    [Fact]
    public async Task DeveRetornar401ComMesmaMensagemParaTodasAsFalhas()
    {
        await CadastrarUsuarioAsync();

        HttpResponseMessage[] respostas = new[]
        {
            await _client.PostAsJsonAsync(
                "/api/auth/login",
                new LoginRequest("inexistente@fcg.com", SenhaUsuario)
            ),
            await _client.PostAsJsonAsync(
                "/api/auth/login",
                new LoginRequest(EmailUsuario, "Errada@123")
            ),
            await _client.PostAsJsonAsync(
                "/api/auth/login",
                new LoginRequest("formato-invalido", SenhaUsuario)
            ),
        };

        foreach (HttpResponseMessage? resposta in respostas)
        {
            resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            RespostaErro? erro = await resposta.Content.ReadFromJsonAsync<RespostaErro>(
                _jsonOptions
            );
            erro!.Errors.Should().ContainSingle().Which.Should().Be("Credenciais inválidas.");
        }
    }

    [Fact]
    public async Task DeveRetornarTokenJwtValido()
    {
        await CadastrarUsuarioAsync();

        HttpResponseMessage resposta = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(EmailUsuario, SenhaUsuario)
        );
        LoginResponse? body = await resposta.Content.ReadFromJsonAsync<LoginResponse>(_jsonOptions);

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var parametros = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = FcgApiFactory.TestIssuer,
            ValidateAudience = true,
            ValidAudience = FcgApiFactory.TestAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(FcgApiFactory.TestSigningKey)
            ),
            ClockSkew = TimeSpan.FromSeconds(5),
        };

        ClaimsPrincipal principal = handler.ValidateToken(body!.AccessToken, parametros, out _);

        principal.FindFirstValue(JwtRegisteredClaimNames.Email).Should().Be(EmailUsuario);
        principal.FindFirstValue(ClaimTypes.Role).Should().Be("Usuario");
    }

    // --- /refresh ---

    [Fact]
    public async Task DeveTrocarRefreshPorNovoParEEmitirNovoRefresh()
    {
        await CadastrarUsuarioAsync();
        LoginResponse login = await LoginAsync();

        HttpResponseMessage resposta = await _client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequest(login.RefreshToken!)
        );

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        LoginResponse? body = await resposta.Content.ReadFromJsonAsync<LoginResponse>(_jsonOptions);
        body!.AccessToken.Should().NotBeNullOrWhiteSpace();
        body.RefreshToken.Should().NotBeNullOrWhiteSpace();
        body.RefreshToken.Should().NotBe(login.RefreshToken);
    }

    [Fact]
    public async Task DeveRejeitarRefreshTokenJaUsado()
    {
        await CadastrarUsuarioAsync();
        LoginResponse login = await LoginAsync();
        HttpResponseMessage primeira = await _client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequest(login.RefreshToken!)
        );
        primeira.EnsureSuccessStatusCode();

        HttpResponseMessage segunda = await _client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequest(login.RefreshToken!)
        );

        segunda.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeveRejeitarRefreshTokenInexistente()
    {
        HttpResponseMessage resposta = await _client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequest("token-que-nao-existe")
        );

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        RespostaErro? erro = await resposta.Content.ReadFromJsonAsync<RespostaErro>(_jsonOptions);
        erro!.Errors[0].Should().Be("Refresh token inválido.");
    }

    [Fact]
    public async Task DeveRetornar400QuandoRefreshTokenVazio()
    {
        using var conteudo = new StringContent(
            "{\"refreshToken\":\"\"}",
            Encoding.UTF8,
            "application/json"
        );
        HttpResponseMessage resposta = await _client.PostAsync("/api/auth/refresh", conteudo);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeveRetornar400QuandoRefreshTokenAusente()
    {
        using var conteudo = new StringContent("{}", Encoding.UTF8, "application/json");
        HttpResponseMessage resposta = await _client.PostAsync("/api/auth/refresh", conteudo);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeveRejeitarRefreshQuandoUsuarioFoiDesativado()
    {
        await CadastrarUsuarioAsync();
        LoginResponse login = await LoginAsync();
        await DesativarUsuarioNoBancoAsync(EmailUsuario);

        HttpResponseMessage resposta = await _client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequest(login.RefreshToken!)
        );

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeveRejeitarRefreshTokenExpirado()
    {
        await CadastrarUsuarioAsync();
        LoginResponse login = await LoginAsync();
        await ExpirarRefreshTokenNoBancoAsync(login.RefreshToken!);

        HttpResponseMessage resposta = await _client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequest(login.RefreshToken!)
        );

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        RespostaErro? erro = await resposta.Content.ReadFromJsonAsync<RespostaErro>(_jsonOptions);
        erro!.Errors[0].Should().Be("Refresh token inválido.");
    }

    // --- /logout ---

    [Fact]
    public async Task DeveRevogarRefreshTokenERetornar204NoLogout()
    {
        await CadastrarUsuarioAsync();
        LoginResponse login = await LoginAsync();

        HttpResponseMessage logout = await _client.PostAsJsonAsync(
            "/api/auth/logout",
            new LogoutRequest(login.RefreshToken!)
        );

        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage refresh = await _client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequest(login.RefreshToken!)
        );
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeveRetornar204NoLogoutComTokenInexistente()
    {
        HttpResponseMessage resposta = await _client.PostAsJsonAsync(
            "/api/auth/logout",
            new LogoutRequest("token-que-nao-existe")
        );

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeveRetornar400NoLogoutComTokenVazio()
    {
        using var conteudo = new StringContent(
            "{\"refreshToken\":\"\"}",
            Encoding.UTF8,
            "application/json"
        );
        HttpResponseMessage resposta = await _client.PostAsync("/api/auth/logout", conteudo);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeveRetornar400NoLogoutComTokenAusente()
    {
        using var conteudo = new StringContent("{}", Encoding.UTF8, "application/json");
        HttpResponseMessage resposta = await _client.PostAsync("/api/auth/logout", conteudo);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeveSerIdempotenteAoChamarLogoutDuasVezes()
    {
        await CadastrarUsuarioAsync();
        LoginResponse login = await LoginAsync();
        await _client.PostAsJsonAsync("/api/auth/logout", new LogoutRequest(login.RefreshToken!));

        HttpResponseMessage segunda = await _client.PostAsJsonAsync(
            "/api/auth/logout",
            new LogoutRequest(login.RefreshToken!)
        );

        segunda.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DevePersistirRefreshTokenComoHashSha256ENaoPlaintext()
    {
        await CadastrarUsuarioAsync();
        LoginResponse login = await LoginAsync();

        using IServiceScope scope = _factory.Services.CreateScope();
        FcgDbContext contexto = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        IJwtTokenService jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();

        string hashEsperado = jwt.CalcularHashRefreshToken(login.RefreshToken!);

        RefreshToken persistido = await contexto.RefreshTokens.SingleAsync();
        persistido.TokenHash.Should().Be(hashEsperado);
        persistido.TokenHash.Should().NotBe(login.RefreshToken);
        persistido.TokenHash.Length.Should().Be(64);
    }

    private static async Task AssertRespostaCredenciaisInvalidasAsync(HttpResponseMessage resposta)
    {
        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        RespostaErro? erro = await resposta.Content.ReadFromJsonAsync<RespostaErro>(_jsonOptions);
        erro.Should().NotBeNull();
        erro!.Type.Should().Be("ErroDeAutenticacao");
        erro.Status.Should().Be(401);
        erro.Errors.Should().ContainSingle().Which.Should().Be("Credenciais inválidas.");
    }

    private async Task<LoginResponse> LoginAsync()
    {
        HttpResponseMessage resposta = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(EmailUsuario, SenhaUsuario)
        );
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<LoginResponse>(_jsonOptions))!;
    }

    private async Task CadastrarUsuarioAsync()
    {
        HttpResponseMessage resposta = await _client.PostAsJsonAsync(
            "/api/usuarios",
            new CadastrarUsuarioRequest(NomeUsuario, EmailUsuario, SenhaUsuario)
        );
        resposta.EnsureSuccessStatusCode();
    }

    private async Task DesativarUsuarioNoBancoAsync(string email)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        FcgDbContext contexto = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        var emailVo = Email.Criar(email);
        Usuario usuario = await contexto.Usuarios.SingleAsync(u => u.Email == emailVo);
        usuario.Desativar();
        await contexto.SaveChangesAsync();
    }

    private async Task ExpirarRefreshTokenNoBancoAsync(string plaintext)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        FcgDbContext contexto = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        IJwtTokenService jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        string hash = jwt.CalcularHashRefreshToken(plaintext);
        DateTime passado = DateTime.UtcNow.AddDays(-1);
        await contexto
            .RefreshTokens.Where(rt => rt.TokenHash == hash)
            .ExecuteUpdateAsync(s => s.SetProperty(rt => rt.ExpiraEm, passado));
    }

    private sealed record RespostaErro(string Type, string Title, int Status, List<string> Errors);
}
