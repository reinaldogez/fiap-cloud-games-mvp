using FCG.Domain.Entities;
using HotChocolate.Data.Sorting;

namespace FCG.API.GraphQL;

public class UsuarioSortType : SortInputType<Usuario>
{
    protected override void Configure(ISortInputTypeDescriptor<Usuario> descriptor)
    {
        descriptor.BindFieldsExplicitly();

        descriptor.Field(u => u.Id);
        descriptor.Field(u => u.Nome);
        descriptor.Field(u => u.Tipo);
        descriptor.Field(u => u.DataCriacao);
        descriptor.Field(u => u.Ativo);
    }
}
