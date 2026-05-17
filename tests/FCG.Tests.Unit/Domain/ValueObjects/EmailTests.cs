using FCG.Domain.Exceptions;
using FCG.Domain.ValueObjects;
using FluentAssertions;

namespace FCG.Tests.Unit.Domain.ValueObjects;

public class EmailTests
{
    [Theory]
    [InlineData("usuario@email.com")]
    [InlineData("teste@dominio.com.br")]
    [InlineData("nome.sobrenome@empresa.org")]
    public void DeveCriarEmailValido(string endereco)
    {
        var email = Email.Criar(endereco);

        email.Endereco.Should().Be(endereco.ToLowerInvariant());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DeveRejeitarEmailVazio(string? endereco)
    {
        Func<Email> acao = () => Email.Criar(endereco!);

        acao.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("sem-arroba")]
    [InlineData("falta@")]
    [InlineData("@falta.com")]
    [InlineData("email-sem-dominio")]
    public void DeveRejeitarEmailFormatoInvalido(string endereco)
    {
        Func<Email> acao = () => Email.Criar(endereco);

        acao.Should().Throw<DomainException>();
    }

    [Fact]
    public void DeveTratarEmailComoCaseInsensitive()
    {
        var email = Email.Criar("Usuario@Email.COM");

        email.Endereco.Should().Be("usuario@email.com");
    }

    [Fact]
    public void DeveTerIgualdadeEstrutural()
    {
        var email1 = Email.Criar("teste@email.com");
        var email2 = Email.Criar("TESTE@EMAIL.COM");

        email1.Should().Be(email2);
    }
}
