using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using FCG.Application.DTOs;
using FCG.Application.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FCG.API.Controllers;

[ApiController]
[Route("api/usuarios")]
[EnableRateLimiting("fixed")]
public class UsuarioController(
    CadastrarUsuarioUseCase cadastrarUsuarioUseCase,
    ObterUsuarioPorIdUseCase obterUsuarioPorIdUseCase,
    ListarUsuariosUseCase listarUsuariosUseCase,
    AtualizarUsuarioUseCase atualizarUsuarioUseCase,
    AlterarSenhaUseCase alterarSenhaUseCase,
    DesativarUsuarioUseCase desativarUsuarioUseCase,
    AtivarUsuarioUseCase ativarUsuarioUseCase,
    AlterarTipoUsuarioUseCase alterarTipoUsuarioUseCase
) : ControllerBase
{
    /// <summary>
    /// Cadastra um novo usuário na plataforma.
    /// </summary>
    /// <param name="request">Dados do usuário: nome, e-mail e senha.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <response code="201">Usuário cadastrado com sucesso. O header Location aponta para o recurso criado.</response>
    /// <response code="400">Dados inválidos (e-mail mal formatado, senha fraca ou nome vazio).</response>
    /// <response code="409">E-mail já cadastrado.</response>
    /// <response code="429">Limite de requisições excedido.</response>
    /// <response code="500">Erro interno no servidor.</response>
    [HttpPost]
    [ProducesResponseType(typeof(UsuarioResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CadastrarAsync(
        [FromBody] CadastrarUsuarioRequest request,
        CancellationToken cancellationToken
    )
    {
        UsuarioResponse resposta = await cadastrarUsuarioUseCase.ExecutarAsync(
            request,
            cancellationToken
        );
        return CreatedAtRoute("ObterUsuarioPorId", new { id = resposta.Id }, resposta);
    }

    /// <summary>
    /// Obtém os dados de um usuário pelo seu identificador.
    /// </summary>
    /// <param name="id">Identificador único do usuário.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <response code="200">Usuário encontrado.</response>
    /// <response code="401">Requisição sem token ou com token inválido.</response>
    /// <response code="403">Usuário autenticado não é o próprio dono nem administrador.</response>
    /// <response code="404">Usuário não localizado.</response>
    /// <response code="429">Limite de requisições excedido.</response>
    /// <response code="500">Erro interno no servidor.</response>
    [HttpGet("{id:guid}", Name = "ObterUsuarioPorId")]
    [Authorize(Policy = "OwnerOrAdmin")]
    [ProducesResponseType(typeof(UsuarioResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        UsuarioResponse? resposta = await obterUsuarioPorIdUseCase.ExecutarAsync(
            id,
            cancellationToken
        );

        if (resposta is null)
            return NotFound();

        return Ok(resposta);
    }

    /// <summary>
    /// Lista usuários de forma paginada.
    /// </summary>
    /// <param name="pagina">Número da página (mínimo: 1).</param>
    /// <param name="tamanhoPagina">Quantidade de itens por página (1 a 100).</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <response code="200">Lista paginada de usuários.</response>
    /// <response code="400">Parâmetros de paginação inválidos.</response>
    /// <response code="429">Limite de requisições excedido.</response>
    /// <response code="401">Requisição sem token ou com token inválido.</response>
    /// <response code="403">Apenas administradores podem listar usuários.</response>
    /// <response code="500">Erro interno no servidor.</response>
    [HttpGet]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(typeof(ListarUsuariosResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ListarAsync(
        [FromQuery]
        [Range(1, int.MaxValue, ErrorMessage = "A página deve ser maior ou igual a 1.")]
            int pagina = 1,
        [FromQuery]
        [Range(1, 100, ErrorMessage = "O tamanho da página deve estar entre 1 e 100.")]
            int tamanhoPagina = 20,
        CancellationToken cancellationToken = default
    )
    {
        ListarUsuariosResponse resposta = await listarUsuariosUseCase.ExecutarAsync(
            pagina,
            tamanhoPagina,
            cancellationToken
        );
        return Ok(resposta);
    }

    /// <summary>
    /// Atualiza o nome e o e-mail de um usuário.
    /// </summary>
    /// <param name="id">Identificador único do usuário.</param>
    /// <param name="request">Novos dados: nome e e-mail.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <response code="200">Usuário atualizado com sucesso.</response>
    /// <response code="400">Dados inválidos (e-mail mal formatado ou nome vazio).</response>
    /// <response code="404">Usuário não localizado.</response>
    /// <response code="409">E-mail já cadastrado por outro usuário.</response>
    /// <response code="429">Limite de requisições excedido.</response>
    /// <response code="401">Requisição sem token ou com token inválido.</response>
    /// <response code="403">Usuário autenticado não é o próprio dono nem administrador.</response>
    /// <response code="500">Erro interno no servidor.</response>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "OwnerOrAdmin")]
    [ProducesResponseType(typeof(UsuarioResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AtualizarAsync(
        Guid id,
        [FromBody] AtualizarUsuarioRequest request,
        CancellationToken cancellationToken
    )
    {
        UsuarioResponse? resposta = await atualizarUsuarioUseCase.ExecutarAsync(
            id,
            request,
            cancellationToken
        );
        if (resposta is null)
            return NotFound();

        return Ok(resposta);
    }

    /// <summary>
    /// Altera a senha de um usuário.
    /// </summary>
    /// <param name="id">Identificador único do usuário.</param>
    /// <param name="request">Senha atual e nova senha.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <response code="204">Senha alterada com sucesso.</response>
    /// <response code="400">Senha atual incorreta ou nova senha não atende aos requisitos.</response>
    /// <response code="404">Usuário não localizado.</response>
    /// <response code="429">Limite de requisições excedido.</response>
    /// <response code="401">Requisição sem token ou com token inválido.</response>
    /// <response code="403">Usuário autenticado não é o próprio dono nem administrador.</response>
    /// <response code="500">Erro interno no servidor.</response>
    [HttpPost("{id:guid}/alterar-senha")]
    [Authorize(Policy = "OwnerOrAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AlterarSenhaAsync(
        Guid id,
        [FromBody] AlterarSenhaRequest request,
        CancellationToken cancellationToken
    )
    {
        bool encontrado = await alterarSenhaUseCase.ExecutarAsync(id, request, cancellationToken);
        if (!encontrado)
            return NotFound();

        return NoContent();
    }

    /// <summary>
    /// Desativa um usuário (soft delete). Operação idempotente.
    /// </summary>
    /// <param name="id">Identificador único do usuário.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <response code="204">Usuário desativado (ou já estava desativado).</response>
    /// <response code="404">Usuário não localizado.</response>
    /// <response code="429">Limite de requisições excedido.</response>
    /// <response code="401">Requisição sem token ou com token inválido.</response>
    /// <response code="403">Apenas administradores podem desativar usuários.</response>
    /// <response code="500">Erro interno no servidor.</response>
    [HttpPatch("{id:guid}/desativar")]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DesativarAsync(Guid id, CancellationToken cancellationToken)
    {
        bool encontrado = await desativarUsuarioUseCase.ExecutarAsync(id, cancellationToken);
        if (!encontrado)
            return NotFound();

        return NoContent();
    }

    /// <summary>
    /// Ativa um usuário (reverte o soft delete). Operação idempotente.
    /// </summary>
    /// <param name="id">Identificador único do usuário.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <response code="204">Usuário ativado (ou já estava ativo).</response>
    /// <response code="404">Usuário não localizado.</response>
    /// <response code="429">Limite de requisições excedido.</response>
    /// <response code="401">Requisição sem token ou com token inválido.</response>
    /// <response code="403">Apenas administradores podem ativar usuários.</response>
    /// <response code="500">Erro interno no servidor.</response>
    [HttpPatch("{id:guid}/ativar")]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AtivarAsync(Guid id, CancellationToken cancellationToken)
    {
        bool encontrado = await ativarUsuarioUseCase.ExecutarAsync(id, cancellationToken);
        if (!encontrado)
            return NotFound();

        return NoContent();
    }

    /// <summary>
    /// Altera o tipo (perfil) de um usuário.
    /// </summary>
    /// <param name="id">Identificador único do usuário.</param>
    /// <param name="request">Novo tipo: "Usuario" ou "Administrador".</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <response code="200">Tipo alterado com sucesso.</response>
    /// <response code="400">Tipo inválido.</response>
    /// <response code="404">Usuário não localizado.</response>
    /// <response code="429">Limite de requisições excedido.</response>
    /// <response code="401">Requisição sem token ou com token inválido.</response>
    /// <response code="403">Apenas administradores podem alterar o tipo de um usuário.</response>
    /// <response code="500">Erro interno no servidor.</response>
    [HttpPatch("{id:guid}/tipo")]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(typeof(UsuarioResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AlterarTipoAsync(
        Guid id,
        [FromBody] AlterarTipoRequest request,
        CancellationToken cancellationToken
    )
    {
        var solicitanteId = Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
        UsuarioResponse? resposta = await alterarTipoUsuarioUseCase.ExecutarAsync(
            id,
            solicitanteId,
            request,
            cancellationToken
        );
        if (resposta is null)
            return NotFound();

        return Ok(resposta);
    }
}
