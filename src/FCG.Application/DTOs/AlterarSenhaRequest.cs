using System.ComponentModel.DataAnnotations;

namespace FCG.Application.DTOs;

public record AlterarSenhaRequest([Required] string SenhaAtual, [Required] string NovaSenha);
