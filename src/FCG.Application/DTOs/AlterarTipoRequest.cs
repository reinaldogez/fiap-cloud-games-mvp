using System.ComponentModel.DataAnnotations;
using FCG.Domain.Enums;

namespace FCG.Application.DTOs;

public record AlterarTipoRequest([Required] TipoUsuario Tipo);
