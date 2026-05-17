using FCG.Domain.Entities;
using FCG.Domain.Exceptions;
using FCG.Domain.Interfaces;
using HotChocolate;
using HotChocolate.Data;
using HotChocolate.Resolvers;
using HotChocolate.Types;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using GraphQLAuthorize = HotChocolate.Authorization.AuthorizeAttribute;

namespace FCG.API.GraphQL;

[ExtendObjectType("Query")]
public class UsuarioQueries
{
    /// <summary>
    /// Lista usuários com filtragem dinâmica, ordenação e paginação cursor-based.
    /// Apenas administradores podem acessar.
    /// </summary>
    [GraphQLAuthorize(Roles = new[] { "Administrador" })]
    [UsePaging(IncludeTotalCount = true, MaxPageSize = 100, DefaultPageSize = 20)]
    [UseFiltering<UsuarioFilterType>]
    [UseSorting<UsuarioSortType>]
    public IQueryable<Usuario> GetUsuarios([Service] IUsuarioRepository repositorio) =>
        repositorio.Query();

    /// <summary>
    /// Obtém um usuário específico. Acessível pelo próprio dono ou por administradores.
    /// </summary>
    public async Task<Usuario?> GetUsuarioAsync(
        Guid id,
        [Service] IUsuarioRepository repositorio,
        [Service] IAuthorizationService authorizationService,
        IResolverContext resolverContext,
        CancellationToken cancellationToken
    )
    {
        if (resolverContext.GetUser() is not { Identity.IsAuthenticated: true } user)
        {
            throw new DomainAuthException("Autenticação obrigatória.");
        }

        AuthorizationResult resultado = await authorizationService.AuthorizeAsync(
            user,
            resolverContext,
            "OwnerOrAdmin"
        );

        if (!resultado.Succeeded)
        {
            throw new DomainAuthException("Acesso negado.");
        }

        return await repositorio.Query().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }
}
