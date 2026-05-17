using System.ComponentModel.DataAnnotations;

namespace FCG.Application.DTOs;

public record CadastrarUsuarioRequest(
    [Required] string Nome,
    [Required] string Email,
    [Required] string Senha
);
