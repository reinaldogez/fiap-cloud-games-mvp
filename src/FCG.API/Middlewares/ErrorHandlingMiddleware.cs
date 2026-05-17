using System.Diagnostics;
using FCG.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace FCG.API.Middlewares;

public class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (DomainConflictException ex)
        {
            string traceId = Activity.Current?.TraceId.ToHexString() ?? context.TraceIdentifier;
            logger.LogWarning(
                ex,
                "Conflito de domínio: {Mensagem} | Path: {Path}",
                ex.Message,
                context.Request.Path
            );
            await EscreverRespostaAsync(
                context,
                CriarProblemDetails(
                    StatusCodes.Status409Conflict,
                    "ErroDeNegocio",
                    ex.Message,
                    traceId
                )
            );
        }
        catch (DomainAuthException ex)
        {
            string traceId = Activity.Current?.TraceId.ToHexString() ?? context.TraceIdentifier;
            logger.LogWarning(
                ex,
                "Falha de autenticação: {Mensagem} | Path: {Path}",
                ex.Message,
                context.Request.Path
            );
            await EscreverRespostaAsync(
                context,
                CriarProblemDetails(
                    StatusCodes.Status401Unauthorized,
                    "ErroDeAutenticacao",
                    ex.Message,
                    traceId
                )
            );
        }
        catch (DomainException ex)
        {
            string traceId = Activity.Current?.TraceId.ToHexString() ?? context.TraceIdentifier;
            logger.LogWarning(
                ex,
                "Erro de domínio: {Mensagem} | Path: {Path}",
                ex.Message,
                context.Request.Path
            );
            await EscreverRespostaAsync(
                context,
                CriarProblemDetails(
                    StatusCodes.Status400BadRequest,
                    "ErroDeValidacao",
                    ex.Message,
                    traceId
                )
            );
        }
        catch (OperationCanceledException ex) when (context.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation(
                ex,
                "Requisição cancelada pelo cliente. Path: {Path}",
                context.Request.Path
            );
            context.Response.StatusCode = 499;
        }
        catch (Exception ex)
        {
            string traceId = Activity.Current?.TraceId.ToHexString() ?? context.TraceIdentifier;
            logger.LogError(
                ex,
                "Erro inesperado: {Mensagem} | Path: {Path}",
                ex.Message,
                context.Request.Path
            );
            await EscreverRespostaAsync(
                context,
                CriarProblemDetails(
                    StatusCodes.Status500InternalServerError,
                    "ErroInterno",
                    "Ocorreu um erro interno no servidor.",
                    traceId
                )
            );
        }
    }

    private static ProblemDetails CriarProblemDetails(
        int status,
        string tipo,
        string mensagem,
        string traceId
    )
    {
        ProblemDetails problem = new()
        {
            Type = tipo,
            Title = "Erro ao processar requisição",
            Status = status,
        };
        problem.Extensions["errors"] = new[] { mensagem };
        problem.Extensions["traceId"] = traceId;
        return problem;
    }

    private static async Task EscreverRespostaAsync(HttpContext context, ProblemDetails problem)
    {
        context.Response.StatusCode = problem.Status!.Value;
        await context.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: "application/problem+json"
        );
    }
}
