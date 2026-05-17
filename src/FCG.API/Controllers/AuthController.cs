using FCG.Application.DTOs;
using FCG.Application.UseCases;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FCG.API.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("fixed")]
public class AuthController(
    LoginUseCase loginUseCase,
    RefreshTokenUseCase refreshTokenUseCase,
    LogoutUseCase logoutUseCase
) : ControllerBase
{
    /// <summary>
    /// Autentica um usuário e retorna um access token JWT junto de um refresh token.
    /// </summary>
    /// <param name="request">Credenciais do usuário: e-mail e senha.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <response code="200">Autenticação bem-sucedida. Retorna access token, refresh token e dados de expiração.</response>
    /// <response code="401">Credenciais inválidas (e-mail inexistente, senha incorreta ou usuário desativado).</response>
    /// <response code="429">Limite de requisições excedido.</response>
    /// <response code="500">Erro interno no servidor.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> LoginAsync(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken
    )
    {
        LoginResponse resposta = await loginUseCase.ExecutarAsync(request, cancellationToken);
        return Ok(resposta);
    }

    /// <summary>
    /// Troca um refresh token válido por um novo par (access + refresh).
    /// O refresh token apresentado é revogado (rotação).
    /// </summary>
    /// <param name="request">Refresh token atual.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <response code="200">Novo par de tokens emitido com sucesso.</response>
    /// <response code="401">Refresh token inválido, revogado, expirado ou usuário desativado.</response>
    /// <response code="429">Limite de requisições excedido.</response>
    /// <response code="500">Erro interno no servidor.</response>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RefreshAsync(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken
    )
    {
        LoginResponse resposta = await refreshTokenUseCase.ExecutarAsync(
            request,
            cancellationToken
        );
        return Ok(resposta);
    }

    /// <summary>
    /// Revoga um refresh token. Não invalida access tokens já emitidos (que continuam válidos até expirar).
    /// Operação idempotente: tokens inexistentes ou já revogados também retornam 204.
    /// </summary>
    /// <param name="request">Refresh token a revogar.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <response code="204">Logout efetuado (ou token já estava inválido).</response>
    /// <response code="429">Limite de requisições excedido.</response>
    /// <response code="500">Erro interno no servidor.</response>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> LogoutAsync(
        [FromBody] LogoutRequest request,
        CancellationToken cancellationToken
    )
    {
        await logoutUseCase.ExecutarAsync(request, cancellationToken);
        return NoContent();
    }
}
