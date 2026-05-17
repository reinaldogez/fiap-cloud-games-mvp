# FIAP Cloud Games MVP
MVP da plataforma FIAP Cloud Games. API em .NET 10 desenvolvida como Tech Challenge da Pós-Graduação em Arquitetura de Sistemas .NET (FIAP), utilizando DDD, EF Core e Autenticação JWT.

## Table of Contents
- [FIAP Cloud Games MVP](#fiap-cloud-games-mvp)
  - [Table of Contents](#table-of-contents)
  - [Sobre o projeto](#sobre-o-projeto)
  - [Stack](#stack)
  - [Estrutura de pastas](#estrutura-de-pastas)
    - [Modelagem do Domain](#modelagem-do-domain)
  - [Configuração local (primeira vez)](#configuração-local-primeira-vez)
    - [Pré-requisitos](#pré-requisitos)
    - [1. Criar o arquivo de variáveis de ambiente](#1-criar-o-arquivo-de-variáveis-de-ambiente)
    - [2. Subir o SQL Server via Docker](#2-subir-o-sql-server-via-docker)
    - [3. Configurar os secrets da aplicação](#3-configurar-os-secrets-da-aplicação)
    - [4. Aplicar as migrations](#4-aplicar-as-migrations)
    - [5. Rodar a API](#5-rodar-a-api)
  - [CI/CD (GitHub Actions)](#cicd-github-actions)
    - [Secrets no repositório](#secrets-no-repositório)
  - [Como rodar os testes](#como-rodar-os-testes)
  - [Autenticação e Autorização](#autenticação-e-autorização)
    - [Access token, claims e configuração JWT](#access-token-claims-e-configuração-jwt)
    - [Fluxo](#fluxo)
    - [Endpoints de `UsuarioController`](#endpoints-de-usuariocontroller)
    - [Policy `OwnerOrAdmin`](#policy-owneroradmin)
    - [Rate Limiting](#rate-limiting)
    - [Smoke test pelo Scalar ou Swagger](#smoke-test-pelo-scalar-ou-swagger)
  - [Tratamento de Erros](#tratamento-de-erros)
    - [Hierarquia de Exceptions](#hierarquia-de-exceptions)
    - [Middleware Global](#middleware-global)
  - [Observabilidade](#observabilidade)
  - [Qualidade de código](#qualidade-de-código)
    - [Analyzers (build e IDE)](#analyzers-build-e-ide)
      - [.editorconfig + Roslyn built-in](#editorconfig--roslyn-built-in)
      - [StyleCop.Analyzers](#stylecopanalyzers)
      - [SonarAnalyzer.CSharp](#sonaranalyzercsharp)
    - [Formatação (CSharpier)](#formatação-csharpier)
    - [Pre-commit hook (Husky)](#pre-commit-hook-husky)
    - [Análise de cobertura e qualidade agregada (SonarCloud)](#análise-de-cobertura-e-qualidade-agregada-sonarcloud)
  - [GraphQL (HotChocolate)](#graphql-hotchocolate)
    - [Por que GraphQL para leitura?](#por-que-graphql-para-leitura)
    - [Queries disponíveis](#queries-disponíveis)
    - [Autenticação](#autenticação)
    - [Nitro IDE (desenvolvimento)](#nitro-ide-desenvolvimento)
    - [Exemplos de queries](#exemplos-de-queries)
    - [Erros GraphQL](#erros-graphql)
  - [Relatório Administrativo (Dapper)](#relatório-administrativo-dapper)
    - [Por que Dapper aqui?](#por-que-dapper-aqui)
    - [Endpoint](#endpoint)
    - [Resposta](#resposta)
    - [Seed de desenvolvimento](#seed-de-desenvolvimento)

## Sobre o projeto

A FIAP Cloud Games (FCG) será uma plataforma de venda de jogos digitais e gestão de servidores para partidas online. Esta entrega é a **Fase 1** do Tech Challenge, focada em estabelecer a base da plataforma com **cadastro de usuários e autenticação JWT**, garantindo persistência de dados, qualidade de software e boas práticas de desenvolvimento que servirão de fundação para as próximas fases (matchmaking, biblioteca de jogos, gestão de servidores).

**Escopo desta entrega:**

- **Cadastro de usuários** identificados por nome, e-mail e senha. O e-mail é validado quanto ao formato e a senha precisa ter pelo menos 8 caracteres, com letras, números e caracteres especiais. As senhas nunca são guardadas em texto puro — apenas o hash BCrypt vai para o banco.
- **Autenticação via JWT** com dois níveis de acesso: usuário comum (acessa a plataforma) e administrador (administra usuários). O login devolve um access token de 1 hora e um refresh token de 7 dias, que pode ser trocado por um novo par sem precisar fazer login de novo. A cada renovação o refresh token anterior é revogado, e o logout invalida o refresh token apresentado.
- **Gestão de perfil:** atualizar dados, trocar de senha, desativar a conta (soft delete), reativar a conta e alterar o tipo de usuário. As regras de quem pode fazer o quê são aplicadas via políticas de autorização (por exemplo: o próprio usuário ou um administrador podem alterar dados; só administradores podem desativar contas).
- **API REST com Controllers MVC** em .NET 10, documentada com OpenAPI (Scalar) — os endpoints podem ser explorados diretamente pelo navegador em `https://localhost:7222/scalar/v1`.
- **Middleware global de erros** que captura exceções e devolve respostas padronizadas (formato `ProblemDetails`, RFC 7807) com um `traceId` em cada resposta para facilitar a correlação com logs.
- **Persistência com Entity Framework Core** (Code-First) e migrations versionadas, usando SQL Server.
- **Testes automatizados** em três níveis: unitários para as regras de negócio, Behavior-Driven Development (BDD) com Reqnroll e Gherkin para os módulos de cadastro e autenticação, e testes de integração end-to-end com SQL Server real via Testcontainers.
- **Modelagem em DDD:** entidades, value objects, domain services e exceptions de domínio organizados em camadas independentes (Domain, Application, Infrastructure, API), preservando a regra de dependência de dentro para fora.

## Stack

- **.NET 10 / C# 14** — runtime e linguagem
- **SQL Server + EF Core (Code-First com Migrations)** — persistência relacional
- **BCrypt.Net-Next** — hashing de senhas
- **Microsoft.AspNetCore.Authentication.JwtBearer + System.IdentityModel.Tokens.Jwt** — JWT HS256
- **Scalar + Microsoft.AspNetCore.OpenApi** — documentação interativa da API
- **Swashbuckle.AspNetCore.SwaggerUI** — UI alternativa do Swagger apontada para a mesma spec OpenAPI
- **xUnit + FluentAssertions + Moq** — testes unitários
- **Microsoft.AspNetCore.Mvc.Testing + Testcontainers.MsSql** — testes de integração
- **Reqnroll 3.3.4 (xUnit)** — testes BDD com cenários Gherkin em português
- **Docker Compose** — SQL Server local para desenvolvimento
- **Serilog + Serilog.Formatting.Compact** — logs estruturados JSON (CLEF) com enriquecimento automático (TraceId, SpanId, MachineName)
- **OpenTelemetry SDK (AspNetCore)** — rastreamento distribuído W3C, fundação para Tempo/Grafana

## Estrutura de pastas

DDD + Clean Architecture em quatro camadas (cada uma é um `.csproj` separado), seguindo a regra de dependência `API → Infrastructure → Application → Domain`:

```
src/
  FCG.Domain          → Entidades, Value Objects, Enums, Exceptions, Interfaces e Domain Services. ZERO dependências externas.
  FCG.Application     → Use Cases (orquestração), DTOs e contratos de serviços externos.
  FCG.Infrastructure  → EF Core (DbContext, Configs, Repositórios), serviços (BCrypt, JWT) e Migrations.
  FCG.API             → Controllers MVC, Middlewares (erro, rate limit), Authorization handlers e composição da aplicação (Program.cs).
tests/
  FCG.Tests.Unit          → Unitários para Domain, Application e Middlewares.
  FCG.Tests.Integration   → Integração end-to-end com WebApplicationFactory + Testcontainers (SQL Server real em Docker).
  FCG.Tests.Bdd           → BDD com Reqnroll: cenários Gherkin (PT-BR) para os módulos de cadastro e autenticação.
```

### Modelagem do Domain

O Domain concentra três padrões DDD aplicados de forma deliberada:

- **Value Objects imutáveis** (`Email`, `Senha`, `SenhaHash`) implementados como `record` com factory method estático que valida o conteúdo antes do objeto existir. Um e-mail malformado faz `Email.Criar` lançar `DomainException` antes da entidade `Usuario` ser instanciada — a validação não chega ao banco nem aos use cases. Para a materialização vinda do EF Core, cada VO expõe um par `Criar`/`Validar` (valida) + `Reconstituir` (sem validação, para dados já confiáveis no banco).
- **Entidades ricas** com construtor privado e factory method estático (`Usuario.Criar(...)`). Um `Usuario` em estado inválido é impossível por construção, e regras invariantes vivem dentro da entidade — por exemplo, `AlterarTipoSolicitadoPor(novoTipo, solicitanteId)` impede que um administrador rebaixe a si mesmo, lançando `DomainException` antes de qualquer toque no banco.
- **Domain Services** para regras que exigem acesso ao repositório (e portanto não cabem dentro da entidade). `IUsuarioDomainService.RegistrarAsync` é o exemplo: a unicidade de e-mail é uma regra de negócio que precisa consultar o `IUsuarioRepository`, e fica encapsulada num serviço de domínio em vez de poluir o use case com `if`s de regra.

## Configuração local (primeira vez)

Secrets nunca ficam no repositório. Configure via `.env` (Docker) e .NET User Secrets (aplicação).

### Pré-requisitos

*   **SDK do .NET 10** ou superior.
*   **Docker Desktop** (ou daemon equivalente) para o SQL Server.
*   **Entity Framework Core CLI** (`dotnet tool install --global dotnet-ef`).

### 1. Criar o arquivo de variáveis de ambiente

```bash
cp .env.example .env
```

Edite o `.env` e defina uma senha forte para o SQL Server.

### 2. Subir o SQL Server via Docker

```bash
docker compose up -d
```

### 3. Configurar os secrets da aplicação

**Desenvolvimento local (User Secrets):**
```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=FcgDb;User Id=sa;Password=<SA_PASSWORD>;TrustServerCertificate=True;" --project src/FCG.API
dotnet user-secrets set "AdminSeed:DefaultPassword" "<SenhaDoAdmin>" --project src/FCG.API
dotnet user-secrets set "Jwt:SigningKey" "<chave-aleatoria-com-mais-de-32-caracteres>" --project src/FCG.API
```

Substitua `<SA_PASSWORD>` pela senha definida no `.env`. Para `Jwt:SigningKey`, use uma string aleatória com pelo menos 32 caracteres (ex: `openssl rand -base64 64`). A API valida no startup e falha imediatamente se a chave for menor.

> **Windows:** o `openssl` não está disponível no PowerShell por padrão. Use o **Git Bash** (incluído com o [Git para Windows](https://git-scm.com/download/win)) ou instale o [OpenSSL para Windows](https://slproweb.com/products/Win32OpenSSL.html) separadamente.

**CI/produção (variáveis de ambiente):**
```bash
ConnectionStrings__DefaultConnection=Server=...;Database=...;User Id=...;Password=...;
AdminSeed__DefaultPassword=SuaSenhaAqui
Jwt__SigningKey=ChaveAleatoriaComMaisDe32Caracteres
```

Precedência do .NET (do menor pro maior): `appsettings.json` → `appsettings.Development.json` → User Secrets → Variáveis de ambiente.

### 4. Aplicar as migrations

Antes de executar o EF, faça restore da solução (isso gera os arquivos `obj/project.assets.json` de todos os projetos, inclusive `FCG.API` e `FCG.Infrastructure`):

```bash
dotnet restore FCG.slnx
dotnet build src/FCG.API/FCG.API.csproj
```

Depois aplique as migrations:

```bash
dotnet ef database update -p src/FCG.Infrastructure -s src/FCG.API
```

Se aparecer o erro `NETSDK1004: Assets file ... project.assets.json not found`, rode os comandos abaixo e tente novamente:

```bash
dotnet restore src/FCG.Infrastructure/FCG.Infrastructure.csproj
dotnet restore src/FCG.API/FCG.API.csproj
dotnet build src/FCG.API/FCG.API.csproj
dotnet ef database update -p src/FCG.Infrastructure -s src/FCG.API --verbose
```

### 5. Rodar a API

```bash
dotnet run --project src/FCG.API
```

## CI/CD (GitHub Actions)

O workflow em [`.github/workflows/ci.yml`](.github/workflows/ci.yml) roda automaticamente em todo `push` para `main` ou `feature/**` e em Pull Requests para `main`. Ele executa quatro jobs:

1. **Testes unitários**, **testes de integração** e **testes BDD** — rodam em paralelo, cada um gerando um relatório de cobertura OpenCover como artefato.
2. **SonarCloud** — aguarda os três jobs anteriores (`needs`), baixa os artefatos de cobertura e envia a análise consolidada ao SonarCloud (ver seção [Análise de cobertura e qualidade agregada (SonarCloud)](#análise-de-cobertura-e-qualidade-agregada-sonarcloud)).

Os testes de integração e BDD usam Testcontainers, que sobe um SQL Server real via Docker — o runner `ubuntu-latest` já tem Docker, então funciona sem configuração extra.

### Secrets no repositório

O CI **não depende de nenhum secret do GitHub** para rodar os testes. A `FcgApiFactory` define todas as configurações de JWT necessárias via variáveis de ambiente no construtor estático (valores hardcoded de teste), e o `AdminSeedService` é removido do DI durante os testes — portanto `AdminSeed:DefaultPassword` nunca é consumido.

> Para um ambiente de produção real, configure `ConnectionStrings__DefaultConnection`, `Jwt__SigningKey`, `Jwt__Issuer`, `Jwt__Audience` e `AdminSeed__DefaultPassword` como secrets ou variáveis de ambiente no servidor de deploy.

## Como rodar os testes

```bash
dotnet test                                      # Roda todas as suítes
dotnet test tests/FCG.Tests.Unit/                # Apenas unitários (rápido, sem dependências)
dotnet test tests/FCG.Tests.Integration/         # Integração end-to-end (requer Docker rodando)
dotnet test tests/FCG.Tests.Bdd/                 # BDD Reqnroll: 7 cenários Gherkin (requer Docker rodando)
```

Os testes de integração e BDD usam `Testcontainers.MsSql` para subir uma instância efêmera de SQL Server por execução — o Docker Desktop (ou daemon equivalente) precisa estar ativo. As migrations são aplicadas automaticamente no container antes de cada cenário, e o banco é descartado ao final.

## Autenticação e Autorização

A API usa **JWT Bearer** com dois níveis de acesso (`Usuario` e `Administrador`) e refresh tokens com **rotação**.

### Access token, claims e configuração JWT

O access token é assinado em **HS256** com a chave de `Jwt:SigningKey` e carrega as claims:

| Claim | Conteúdo |
|---|---|
| `sub` | `Id` do usuário (`Guid`) — usado pela policy `OwnerOrAdmin` |
| `email` | E-mail do usuário |
| `name` | Nome do usuário |
| `jti` | Identificador único do token (`Guid`) |
| `role` | `Usuario` ou `Administrador` |

A assinatura simétrica é adequada ao cenário de monolito MVP, em que o emissor e o validador do token são o mesmo serviço. Em uma futura evolução para microsserviços, a transição natural seria para **RS256** (par de chaves assimétricas) — cada serviço validaria o token apenas com a chave pública, sem precisar compartilhar o segredo de assinatura.

> **Detalhe de configuração:** o `JwtBearerHandler` é configurado com `MapInboundClaims = false` e `NameClaimType = JwtRegisteredClaimNames.Sub`. Sem isso, o ASP.NET Core mapeia `sub` para `ClaimTypes.NameIdentifier` por padrão, e a policy `OwnerOrAdmin` (que lê `JwtRegisteredClaimNames.Sub` diretamente) silenciosamente deixa de funcionar.

### Fluxo

1. **Login** — `POST /api/auth/login` com `{ "email", "senha" }` retorna:
   ```json
   {
     "accessToken": "eyJhbGc...",
     "tokenType": "Bearer",
     "expiresIn": 3600,
     "refreshToken": "Y3Jp..."
   }
   ```
   Access token vale 1 hora, refresh token 7 dias.
2. **Chamadas autenticadas** — adicione `Authorization: Bearer <accessToken>` no header.
3. **Renovar** — `POST /api/auth/refresh` com `{ "refreshToken" }` retorna **novo par** (access + refresh). O refresh apresentado é revogado e marcado como substituído pelo novo (rotação).
4. **Logout** — `POST /api/auth/logout` com `{ "refreshToken" }` revoga o refresh atual. Operação **idempotente**: tokens inexistentes ou já revogados também retornam 204. Access tokens já emitidos continuam válidos até expirar.

Falhas de autenticação retornam **401** com mensagem genérica `"Credenciais inválidas."` (ou `"Refresh token inválido."`) — não vazamos se foi o e-mail, a senha, o status do usuário ou o token.

### Endpoints de `UsuarioController`

| Método | Rota | Acesso |
|---|---|---|
| `POST` | `/api/usuarios` | público |
| `GET` | `/api/usuarios/{id}` | próprio dono **ou** `Administrador` (policy `OwnerOrAdmin`) |
| `GET` | `/api/usuarios` | `Administrador` |
| `PUT` | `/api/usuarios/{id}` | próprio dono **ou** `Administrador` (policy `OwnerOrAdmin`) |
| `POST` | `/api/usuarios/{id}/alterar-senha` | próprio dono **ou** `Administrador` |
| `PATCH` | `/api/usuarios/{id}/desativar` | `Administrador` |
| `PATCH` | `/api/usuarios/{id}/ativar` | `Administrador` (reverte o soft delete) |
| `PATCH` | `/api/usuarios/{id}/tipo` | `Administrador` (admin não pode rebaixar a si mesmo → 400) |

### Policy `OwnerOrAdmin`

Endpoints com a marca *próprio dono **ou** `Administrador`* na tabela acima usam uma policy customizada em vez de `if`s de autorização espalhados pelos controllers. O `OwnerOrAdminHandler` (em `src/FCG.API/Authorization/`) resolve o requisito da seguinte forma:

- Se o token tem `role = Administrador`, o acesso é liberado.
- Caso contrário, o handler compara o claim `sub` do token com o parâmetro `{id}` da rota — se forem iguais, libera; senão, retorna 403.

Concentrar a regra em um único handler facilita manutenção e auditoria: qualquer mudança no critério de "próprio dono" reflete automaticamente em todos os endpoints decorados com `[Authorize(Policy = "OwnerOrAdmin")]`. Em endpoints exclusivamente administrativos, a marcação direta `[Authorize(Roles = "Administrador")]` é suficiente e dispensa a policy.

### Rate Limiting

Todos os controllers REST estão decorados com `[EnableRateLimiting("fixed")]`, que aplica uma policy Fixed Window com particionamento híbrido:

- **Requisições autenticadas** — partição pelo claim `sub` do JWT, ou seja, cada usuário consome a própria janela.
- **Requisições anônimas** — partição pelo header `X-Forwarded-For` (ou `RemoteIpAddress` quando ausente), ou seja, o limite é por endereço de origem.

A separação evita que um usuário legítimo seja bloqueado pelo abuso de outro na mesma rede corporativa, e ao mesmo tempo impede que um cliente anônimo distribua tentativas entre múltiplos endpoints sem ser limitado.

Os limites são lidos da seção `RateLimit` do `appsettings.json`:

```json
{
  "RateLimit": {
    "PermitLimit": 10,
    "WindowInSeconds": 60
  }
}
```

Ao exceder o limite a API retorna **429 Too Many Requests**. Em testes de integração o `PermitLimit` é sobrescrito para `int.MaxValue` via `FcgApiFactory`, evitando flakiness por janela compartilhada entre cenários. No pipeline de middlewares, `UseAuthentication()` vem **antes** de `UseRateLimiter()` para garantir que `httpContext.User` esteja populado quando a policy resolve a chave de particionamento.

### Smoke test pelo Scalar ou Swagger

Em desenvolvimento, dois clientes estão disponíveis:

- **Scalar** — `https://localhost:7222/scalar/v1`. O botão **Authorize** usa o SecurityScheme Bearer (configurado via `BearerSecuritySchemeTransformer`): cole apenas o `accessToken` (sem o prefixo `Bearer`) e os endpoints protegidos passam a enviar o header automaticamente.
- **Swagger UI** — `https://localhost:7222/swagger`. Aponta para a mesma spec (`/openapi/v1.json`); use o botão **Authorize** da mesma forma.

Casos prontos em `src/FCG.API/FCG.API.http` (login → refresh → logout, e Authorization header já preenchido nos endpoints protegidos).

## Tratamento de Erros

O projeto utiliza uma estratégia de tratamento de erros centralizada no Domain, capturada por um middleware global na camada de API. Isso garante que as regras de negócio controlem o fluxo de erro sem vazar detalhes de infraestrutura.

### Hierarquia de Exceptions

As exceções de domínio herdam de `DomainException` e são mapeadas para códigos HTTP específicos:

| Exception | Status | Categoria | Uso comum |
|---|---|---|---|
| `DomainException` | 400 | `ErroDeValidacao` | Dados inválidos, falha em VOs, senha fraca. |
| `DomainConflictException` | 409 | `ErroDeNegocio` | Conflito de estado (ex: e-mail já cadastrado). |
| `DomainAuthException` | 401 | `ErroDeAutenticacao` | Credenciais inválidas, refresh token expirado. |
| `Outras (Inesperadas)` | 500 | `ErroInterno` | Falhas de banco, rede ou bugs não mapeados. |

### Middleware Global

O `ErrorHandlingMiddleware` intercepta todas as exceções e retorna um JSON estruturado seguindo o padrão **RFC 7807 (Problem Details)**. Elementos-chave da resposta:

- **`type`**: Categoria estável do erro para o cliente, como `ErroDeValidacao`, `ErroDeNegocio`, `ErroDeAutenticacao` ou `ErroInterno`.
- **`title`**: Título padronizado da resposta. No middleware atual ele é fixo como `Erro ao processar requisição`.
- **`status`**: Código HTTP correspondente ao tipo da falha.
- **`errors`**: Lista com as mensagens específicas do erro.
- **`traceId`**: Identificador único do OpenTelemetry para correlação com logs.

**Exemplo de resposta de erro (400):**
```json
{
  "type": "ErroDeValidacao",
  "title": "Erro ao processar requisição",
  "status": 400,
  "errors": [
    "O formato do e-mail é inválido."
  ],
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736"
}
```

## Observabilidade

A API usa **Serilog** para logs estruturados com correlação de rastreamento via **OpenTelemetry** (TraceId/SpanId W3C). O formato de saída varia por ambiente:

| Ambiente | Formato | Motivo |
|---|---|---|
| `Development` | Console colorido (texto legível) | DX — fácil de acompanhar durante desenvolvimento |
| `Production` | Console JSON (CLEF, uma linha por evento) | Pronto para em etapas futuras utilizar Promtail/Alloy → Loki |

Todo evento de log carrega automaticamente `TraceId`, `SpanId`, `Application`, `MachineName` e `Environment` como propriedades estruturadas. Exemplo de linha em produção:

```json
{"@t":"2026-04-29T14:32:01.123Z","@mt":"HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms","RequestMethod":"POST","RequestPath":"/api/auth/login","StatusCode":200,"Elapsed":84.3,"TraceId":"4bf92f3577b34da6a3ce929d0e0e4736","SpanId":"00f067aa0ba902b7","Application":"FCG.API"}
```

Respostas de erro (4xx/5xx) incluem o mesmo `traceId` no corpo (`ProblemDetails.Extensions["traceId"]`), permitindo correlacionar um erro reportado pelo cliente diretamente com o evento de log correspondente.

## Qualidade de código

A análise estática e a formatação são aplicadas em três camadas complementares, configuradas em [`Directory.Build.props`](Directory.Build.props) e propagadas automaticamente para todos os 7 projetos.

### Analyzers (build e IDE)

| Pacote | Versão | Função |
|---|---|---|
| **StyleCop.Analyzers** | 1.2.0-beta.556 | Convenções de estilo e nomenclatura C# |
| **SonarAnalyzer.CSharp** | 10.25.0.139117 | Detecção de bugs, code smells e vulnerabilidades |
| **Roslyn built-in** | (`AnalysisMode=All`) | Regras de performance, confiabilidade e uso da BCL |

Todas as regras rodam em tempo de build — warnings aparecem no IDE e no output do `dotnet build`. Nenhuma tem CLI própria; o ponto de entrada é sempre:

```bash
dotnet build                             # roda todos os analyzers
dotnet build -warnaserror                # trata warnings como erro (simula CI)
dotnet format --verify-no-changes        # verifica regras do .editorconfig sem modificar arquivos
```

#### .editorconfig + Roslyn built-in

O [`.editorconfig`](.editorconfig) define **o que** o compilador verifica. Ele atua em duas frentes:

- **Estilo visual** (indentação, chaves, espaços) — lido apenas pelo IDE; sem efeito no build.
- **Diagnósticos Roslyn** (`IDE*` e `CA*`) — como `IDE0005` (using desnecessário), `IDE0290` (primary constructors), `CA1822` (membro pode ser `static`). Esses afetam o build porque o projeto define `EnforceCodeStyleInBuild=true` e `AnalysisMode=All` em [`Directory.Build.props`](Directory.Build.props).

As **convenções de naming** também vivem no `.editorconfig` (prefixo `_` em campos privados, sufixo `Async`, `I` em interfaces, etc.) e são validadas pelo Roslyn como `warning`.

Regras `CA*` suprimidas com justificativa estão em [`Directory.Build.props`](Directory.Build.props) (ex: `CA2007` é desnecessário em ASP.NET Core; `CA1812` dá falso positivo com injeção de dependência).

#### StyleCop.Analyzers

Foca em convenções de **escrita** C#: ordem de membros, posição de `using`, modificadores. Regras com prefixo `SA*`.

A configuração extra fica em [`stylecop.json`](stylecop.json) (ex: `allowUnderscorePrefix: true` para compatibilizar com `_camelCase`). Regras que conflitam com as convenções do projeto estão suprimidas com justificativa em [`Directory.Build.props`](Directory.Build.props) (ex: `SA1309` proíbe `_` em campos, `SA1200` exige usings dentro do namespace).

Para filtrar só os warnings StyleCop no output:

```powershell
dotnet build 2>&1 | Select-String "SA\d{4}"
```

#### SonarAnalyzer.CSharp

Detecta bugs, anti-patterns de segurança e code smells. Regras com prefixo `S*` (ex: `S2259` null dereference, `S1481` variável não usada). É distinto do SonarCloud (CI): o pacote NuGet roda **localmente no build** um subconjunto das mesmas regras, antes de chegar ao CI.

Para filtrar só os warnings Sonar no output:

```powershell
dotnet build 2>&1 | Select-String "\[S\d+\]"
```

### Formatação (CSharpier)

**CSharpier** (1.2.6) é o formatador oficial do projeto. Seguindo o modelo do Prettier, ele aplica um estilo único e não-configurável, eliminando debates de formatação:

```bash
dotnet tool restore                 # instala a versão fixada no manifesto
dotnet csharpier format .           # formata todos os arquivos .cs
dotnet csharpier check .            # verifica sem modificar (usado no CI)
```

A versão está fixada em [`.config/dotnet-tools.json`](.config/dotnet-tools.json). Qualquer desenvolvedor que rodar `dotnet tool restore` obtém exatamente a mesma versão.

### Pre-commit hook (Husky)

O Husky intercepta cada `git commit` e executa automaticamente:

1. `dotnet csharpier check .` — rejeita o commit se algum arquivo `.cs` não estiver formatado
2. `dotnet build --no-incremental` — rejeita o commit se o build falhar

As tarefas estão definidas em [`.husky/task-runner.json`](.husky/task-runner.json). Para configurar após clonar o repositório:

```bash
dotnet tool restore       # instala csharpier e husky
dotnet husky install      # registra o hook no git local
```

> O `dotnet husky install` precisa rodar uma vez por clone. Em equipes, recomenda-se adicionar esse comando ao script de setup do projeto.

### Análise de cobertura e qualidade agregada (SonarCloud)

O projeto está integrado ao **SonarCloud** para histórico de análises, métricas de cobertura e quality gates por PR. A análise é executada pelo job `sonar` dentro do [`.github/workflows/ci.yml`](.github/workflows/ci.yml).

A cada push ou PR o job:

1. Aguarda os três jobs de teste finalizarem (`needs: [unit-tests, integration-tests, bdd-tests]`)
2. Baixa os relatórios de cobertura OpenCover gerados por cada job
3. Inicia a análise com `dotnet-sonarscanner begin`
4. Faz o build completo em modo `Release`
5. Finaliza com `dotnet-sonarscanner end` e envia a cobertura consolidada ao SonarCloud

Migrations, `obj/` e `bin/` são excluídos da análise via `sonar.exclusions`. DTOs, Options, OpenApi, Logging, `Program.cs` e Requirements são excluídos apenas da métrica de cobertura via `sonar.coverage.exclusions` (o Sonar ainda os analisa para code smells). O secret `SONAR_TOKEN` precisa estar configurado em `Settings → Secrets → Actions` do repositório.

## GraphQL (HotChocolate)

A API expõe uma superfície de **leitura** em `/graphql` usando **HotChocolate v16** com Nitro IDE embutida. A escrita continua exclusivamente via REST, onde a validação de domínio nos use cases já está consolidada.

### Por que GraphQL para leitura?

Permite que clientes admin peçam exatamente os campos que precisam (sem over-fetching), filtrem e ordenem em qualquer combinação sem exigir novos endpoints REST, e paguem apenas pelos dados retornados. Consultas como "listar todos os administradores ativos, do mais recente para o mais antigo, trazendo só `id`, `nome` e `email`" são expressas diretamente na query.

### Queries disponíveis

| Query | Acesso | Descrição |
|---|---|---|
| `usuarios(first, after, where, order)` | `Administrador` | Paginação cursor-based, filtragem e ordenação dinâmicas sobre todos os usuários |
| `usuario(id)` | próprio dono **ou** `Administrador` | Retorna um usuário pelo ID |

O tipo `Usuario` expõe: `id`, `nome`, `email` (achatado do VO), `tipo`, `dataCriacao`, `ativo`. O campo `SenhaHash` **nunca** é exposto.

### Autenticação

Igual ao REST: obtenha o `accessToken` em `POST /api/auth/login` e envie o header `Authorization: Bearer <accessToken>` em cada requisição ao `/graphql`.

### Nitro IDE (desenvolvimento)

Com a API rodando localmente, abra `https://localhost:7222/graphql` no navegador. Na aba **Headers** adicione:

```
Authorization: Bearer <accessToken>
```

### Exemplos de queries

O arquivo [`FCG.API.graphql`](FCG.API.graphql) na raiz do repositório contém queries prontas para copiar no Nitro:

```graphql
# Listar admins ativos do mais recente para o mais antigo
query AdminsAtivosMaisRecentes {
  usuarios(
    first: 10
    where: { ativo: { eq: true }, tipo: { eq: ADMINISTRADOR } }
    order: { dataCriacao: DESC }
  ) {
    nodes { id nome email tipo dataCriacao }
    pageInfo { hasNextPage endCursor }
    totalCount
  }
}

# Obter usuário pelo ID (owner ou admin)
query ObterUsuarioPorId {
  usuario(id: "00000000-0000-0000-0000-000000000000") {
    id nome email tipo dataCriacao ativo
  }
}
```

### Erros GraphQL

Erros de domínio são mapeados para o campo `errors` da resposta com um `code` estável em `extensions`:

| `extensions.code` | Origem |
|---|---|
| `ERRO_DE_AUTENTICACAO` | Não autenticado ou acesso negado |
| `ERRO_DE_NEGOCIO` | Regra de negócio violada |
| `ERRO_DE_VALIDACAO` | Dados inválidos |

```json
{
  "errors": [{
    "message": "Acesso negado.",
    "extensions": { "code": "ERRO_DE_AUTENTICACAO" }
  }]
}
```

## Relatório Administrativo (Dapper)

O endpoint `GET /api/admin/relatorios/usuarios` utiliza Dapper para consolidar todos os indicadores em uma única viagem ao banco de dados. Essa estratégia evita múltiplas consultas pequenas (problema do N+1) e garante que apenas o resultado final seja trafegado, tornando a geração do relatório extremamente rápida.

### Por que Dapper aqui?

O EF Core é ideal para garantir a integridade das regras de negócio em operações de escrita (cadastro, alteração). Já o Dapper é utilizado em relatórios para otimizar a leitura: ele executa SQL puro em uma única viagem ao banco, garantindo alta performance em contagens e agrupamentos de dados.

A separação é visível na estrutura de pastas (CQRS-lite):

```
Infrastructure/
  Persistence/   ← EF Core (write side): DbContext, Configs, Repositórios, Migrations
  Dapper/        ← Dapper (read side): SqlConnectionFactory, ReadRepositories, Sql
```

### Endpoint

| Método | Rota | Acesso |
|---|---|---|
| `GET` | `/api/admin/relatorios/usuarios` | `Administrador` |

### Resposta

```json
{
  "totalUsuarios": 52,
  "totalAtivos": 48,
  "totalInativos": 4,
  "porTipo": {
    "usuario": 50,
    "administrador": 2
  },
  "cadastrosUltimos30Dias": 12,
  "cadastrosPorMes": [
    { "mes": "2025-12", "total": 8 },
    { "mes": "2026-01", "total": 15 },
    { "mes": "2026-02", "total": 11 },
    { "mes": "2026-03", "total": 9 },
    { "mes": "2026-04", "total": 6 },
    { "mes": "2026-05", "total": 3 }
  ]
}
```

### Seed de desenvolvimento

Para ter dados realistas durante a demonstração, habilite o `DevSeedService` em `appsettings.Development.json`:

```json
{
  "DevSeed": {
    "Enabled": true
  }
}
```

Na próxima inicialização da API (em ambiente `Development`) serão criados 50 usuários com datas de cadastro distribuídas nos últimos 6 meses. O seed é **idempotente** — se já existirem usuários suficientes, nada é criado.
