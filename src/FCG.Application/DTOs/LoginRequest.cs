using System.ComponentModel.DataAnnotations;

namespace FCG.Application.DTOs;

public record LoginRequest([Required] string Email, [Required] string Senha);
