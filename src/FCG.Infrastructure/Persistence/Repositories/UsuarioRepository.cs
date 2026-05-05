using FCG.Domain.Entities;
using FCG.Domain.Interfaces;
using FCG.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace FCG.Infrastructure.Persistence.Repositories;

public class UsuarioRepository(FcgDbContext contexto) : IUsuarioRepository
{
    public async Task<Usuario?> ObterPorEmailAsync(
        Email email,
        CancellationToken cancellationToken = default
    )
    {
        return await contexto.Usuarios.FirstOrDefaultAsync(
            u => u.Email == email,
            cancellationToken
        );
    }

    public async Task<Usuario?> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    ) => await contexto.Usuarios.FindAsync([id], cancellationToken);

    public async Task AdicionarAsync(
        Usuario usuario,
        CancellationToken cancellationToken = default
    ) => await contexto.Usuarios.AddAsync(usuario, cancellationToken);

    public async Task<bool> ExisteComEmailAsync(
        Email email,
        CancellationToken cancellationToken = default
    ) => await contexto.Usuarios.AnyAsync(u => u.Email == email, cancellationToken);

    public void Atualizar(Usuario usuario) => contexto.Usuarios.Update(usuario);

    public async Task<(IReadOnlyList<Usuario> Items, int Total)> ListarPaginadoAsync(
        int pagina,
        int tamanhoPagina,
        CancellationToken cancellationToken = default
    )
    {
        IOrderedQueryable<Usuario> query = contexto
            .Usuarios.AsNoTracking()
            .OrderBy(u => u.DataCriacao);

        int total = await query.CountAsync(cancellationToken);

        List<Usuario> items = await query
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public IQueryable<Usuario> Query() => contexto.Usuarios.AsNoTracking();
}
