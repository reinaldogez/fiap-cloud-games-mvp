using System.ComponentModel.DataAnnotations;

namespace FCG.Application.DTOs;

public record AtualizarUsuarioRequest([Required] string Nome, [Required] string Email);
