using System.Net.Http.Json;
using System.Text.Json;
using FCG.Application.DTOs;
using FCG.Tests.Bdd.Support;
using FluentAssertions;
using Reqnroll;

namespace FCG.Tests.Bdd.Steps;

[Binding]
public class AutenticacaoSteps(HttpClient client, CenarioEstado estado)
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    [Given(@"que existe um usuario com email ""(.*)"" e senha ""(.*)""")]
    public async Task DadoQueExisteUmUsuarioComEmailESenha(string email, string senha)
    {
        var request = new CadastrarUsuarioRequest("Usuario Teste", email, senha);
        HttpResponseMessage resposta = await client.PostAsJsonAsync("/api/usuarios", request);
        resposta
            .IsSuccessStatusCode.Should()
            .BeTrue(
                $"pré-condição: cadastro de '{email}' deveria ter retornado 2xx, mas retornou {(int)resposta.StatusCode}"
            );
    }

    [Given(@"que tenho um refresh token valido para ""(.*)"" com senha ""(.*)""")]
    public async Task DadoQueTenhoUmRefreshTokenValidoPara(string email, string senha)
    {
        await DadoQueExisteUmUsuarioComEmailESenha(email, senha);
        await QuandoEuFacoLoginComEmailESenha(email, senha);

        estado
            .UltimaResposta!.IsSuccessStatusCode.Should()
            .BeTrue($"pré-condição: login de '{email}' deveria ter retornado 2xx");

        string json = await estado.UltimaResposta.Content.ReadAsStringAsync();
        LoginResponse? loginResponse = JsonSerializer.Deserialize<LoginResponse>(
            json,
            _jsonOptions
        );
        loginResponse.Should().NotBeNull();

        estado.TokenAcesso = loginResponse!.AccessToken;
        estado.RefreshToken = loginResponse.RefreshToken;
    }

    [When(@"eu faco login com email ""(.*)"" e senha ""(.*)""")]
    public async Task QuandoEuFacoLoginComEmailESenha(string email, string senha)
    {
        var request = new LoginRequest(email, senha);
        estado.UltimaResposta = await client.PostAsJsonAsync("/api/auth/login", request);
    }

    [When(@"eu uso o refresh token para renovar o acesso")]
    public async Task QuandoEuUsoORefreshTokenParaRenovarOAcesso()
    {
        estado
            .RefreshToken.Should()
            .NotBeNullOrEmpty(
                "pré-condição: refresh token deve estar disponível no estado do cenário"
            );

        estado.RefreshTokenAnterior = estado.RefreshToken;

        var request = new RefreshTokenRequest(estado.RefreshToken!);
        estado.UltimaResposta = await client.PostAsJsonAsync("/api/auth/refresh", request);
    }

    [Then(@"a resposta contem um access token e um refresh token")]
    public async Task EntaoARespostaContemUmAccessTokenEUmRefreshToken()
    {
        estado.UltimaResposta.Should().NotBeNull();
        string json = await estado.UltimaResposta!.Content.ReadAsStringAsync();
        LoginResponse? loginResponse = JsonSerializer.Deserialize<LoginResponse>(
            json,
            _jsonOptions
        );
        loginResponse.Should().NotBeNull();
        loginResponse!.AccessToken.Should().NotBeNullOrWhiteSpace();
        loginResponse.RefreshToken.Should().NotBeNullOrWhiteSpace();
    }

    [Then(@"recebo um novo par de tokens")]
    public async Task EntaoReceboUmNovoParDeTokens()
    {
        estado.UltimaResposta.Should().NotBeNull();
        string json = await estado.UltimaResposta!.Content.ReadAsStringAsync();
        LoginResponse? loginResponse = JsonSerializer.Deserialize<LoginResponse>(
            json,
            _jsonOptions
        );
        loginResponse.Should().NotBeNull();
        loginResponse!.AccessToken.Should().NotBeNullOrWhiteSpace();
        loginResponse.RefreshToken.Should().NotBeNullOrWhiteSpace();

        estado.RefreshToken = loginResponse.RefreshToken;
    }

    [Then(@"o refresh token anterior nao e mais aceito")]
    public async Task EntaoORefreshTokenAnteriorNaoEMaisAceito()
    {
        estado
            .RefreshTokenAnterior.Should()
            .NotBeNullOrEmpty(
                "pré-condição: refresh token anterior deve estar salvo no estado do cenário"
            );

        var request = new RefreshTokenRequest(estado.RefreshTokenAnterior!);
        HttpResponseMessage resposta = await client.PostAsJsonAsync("/api/auth/refresh", request);
        ((int)resposta.StatusCode).Should().Be(401);
    }
}
