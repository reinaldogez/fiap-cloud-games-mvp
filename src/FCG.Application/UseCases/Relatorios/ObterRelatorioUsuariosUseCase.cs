using FCG.Application.DTOs;
using FCG.Application.Interfaces;

namespace FCG.Application.UseCases.Relatorios;

public class ObterRelatorioUsuariosUseCase(IUsuarioReadRepository repositorio)
{
    public Task<RelatorioUsuariosDto> ExecutarAsync(
        CancellationToken cancellationToken = default
    ) => repositorio.ObterRelatorioAsync(cancellationToken);
}
