using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FCG.API.Authorization;
using FCG.Domain.Enums;
using HotChocolate.Authorization;
using HotChocolate.Resolvers;
using Microsoft.AspNetCore.Authorization;

namespace FCG.API.GraphQL.Authorization;

// Variante GraphQL do OwnerOrAdminHandler. O handler REST original lê o {id} da rota via
// HttpContextAccessor, que não existe em /graphql. Aqui pegamos o id dos argumentos do field
// quando o resolver passa o IResolverContext como Resource (ver UsuarioQueries).
public class OwnerOrAdminGraphQLHandler : AuthorizationHandler<OwnerOrAdminRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OwnerOrAdminRequirement requirement
    )
    {
        if (context.User.IsInRole(TipoUsuario.Administrador.ToString()))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (context.Resource is not IMiddlewareContext resolverContext)
        {
            return Task.CompletedTask;
        }

        string? subClaim =
            context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        Guid? argumentoId = TentarLerArgumentoId(resolverContext, requirement.RouteParameterName);

        if (
            subClaim is not null
            && argumentoId is not null
            && Guid.TryParse(subClaim, out Guid usuarioIdToken)
            && usuarioIdToken == argumentoId.Value
        )
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static Guid? TentarLerArgumentoId(
        IMiddlewareContext resolverContext,
        string nomeArgumento
    )
    {
        try
        {
            return resolverContext.ArgumentValue<Guid>(nomeArgumento);
        }
        catch
        {
            return null;
        }
    }
}
