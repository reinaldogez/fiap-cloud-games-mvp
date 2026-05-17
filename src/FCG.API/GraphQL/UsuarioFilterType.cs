using FCG.Domain.Entities;
using HotChocolate.Data.Filters;

namespace FCG.API.GraphQL;

public class UsuarioFilterType : FilterInputType<Usuario>
{
    protected override void Configure(IFilterInputTypeDescriptor<Usuario> descriptor)
    {
        descriptor.BindFieldsExplicitly();

        descriptor.Field(u => u.Id);
        descriptor.Field(u => u.Nome);
        descriptor.Field(u => u.Tipo);
        descriptor.Field(u => u.DataCriacao);
        descriptor.Field(u => u.Ativo);
    }
}
