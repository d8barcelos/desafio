# Order Service API

API REST para gestão de pedidos, construída com .NET 8, ASP.NET Core e PostgreSQL. Implementa autenticação JWT, autorização baseada em papéis (Admin/Customer), transições de estado idempotentes e concorrência otimista em alterações de estoque.

## Stack técnica

- .NET 8 / C# 12 (nullable habilitado, warnings tratados como erros)
- ASP.NET Core Web API (controllers)
- Entity Framework Core 8 + Npgsql
- PostgreSQL 16
- Autenticação JWT Bearer (chave simétrica)
- `Microsoft.AspNetCore.Identity.PasswordHasher<TUser>` para hash de senhas
- Testes: xUnit, FluentAssertions, NSubstitute
- Containers: Dockerfile multi-stage + docker-compose

## Pré-requisitos

- Docker Desktop (ou Docker Engine + Docker Compose v2) — para o fluxo `docker compose up`
- .NET 8 SDK — necessário apenas se você quiser rodar os testes localmente com `dotnet test`

## Como executar

```bash
docker compose up -d --build
```

Isso sobe:
- `postgres` (acessível apenas pela rede interna do Compose)
- `api` em `localhost:8080`

A API aplica as migrations e popula os dados padrão automaticamente na primeira execução.

Depois que estiver no ar:

- Swagger UI: <http://localhost:8080/swagger>
- OpenAPI JSON: <http://localhost:8080/swagger/v1/swagger.json>

Para acompanhar os logs sem manter o processo em foreground:

```bash
docker compose logs -f api
```

Para parar e remover os volumes:

```bash
docker compose down -v
```

> **Observação (Windows):** as portas são publicadas explicitamente em `0.0.0.0` no [`docker-compose.yml`](docker-compose.yml), em vez do atalho padrão `8080:8080`. Isso é proposital: o atalho também faz bind no wildcard IPv6 (`[::]:8080`), e no Windows o `localhost` resolve primeiro para IPv6 enquanto o forwarding IPv6 do Docker Desktop pode ser instável, causando `ERR_SOCKET_NOT_CONNECTED`. Publicar apenas em IPv4 faz o Happy Eyeballs do navegador cair imediatamente para IPv4, então `http://localhost:8080` funciona sem editar o arquivo de hosts. Se ainda assim você bater nesse erro, use `http://127.0.0.1:8080/swagger`.

## Como rodar os testes

```bash
dotnet test
```

Roda os dois projetos de testes: `OrderService.Domain.Tests` e `OrderService.Application.Tests`. Não é necessário banco de dados — os testes usam mocks com NSubstitute e asserções puras de domínio.

## Credenciais padrão

Populadas na primeira execução:

| Papel | Usuário | Senha |
|---|---|---|
| Admin | `admin` | `Admin@123` |
| Customer | `customer` | `Customer@123` |

O usuário `customer` tem `customerId = 11111111-1111-1111-1111-111111111111`. Usuários `Admin` não têm `customerId` e podem operar sobre pedidos de qualquer cliente.

## Testando no Swagger

A maneira mais fácil de exercitar a API em qualquer sistema operacional:

1. Abra <http://localhost:8080/swagger>.
2. Faça `POST /auth/token` com uma das credenciais acima; copie o valor de `token` da resposta.
3. Clique no botão **Authorize** (canto superior direito), cole `Bearer <token>` (com a palavra literal `Bearer` e um espaço antes do token) e confirme.
4. Todos os endpoints de `/orders` passarão a enviar o token automaticamente.

## Inspecionando o banco de dados

A porta do Postgres não é publicada no host de propósito (veja [`docker-compose.yml`](docker-compose.yml)). Para rodar queries ad-hoc:

```bash
docker exec -it desafio-postgres-1 psql -U postgres -d orders
```

Se quiser conectar um cliente GUI (DBeaver, pgAdmin, etc.), adicione um mapeamento de porta no serviço `postgres` do `docker-compose.yml`, por exemplo `"0.0.0.0:5433:5432"`, e conecte em `localhost:5433`.

## Requisições de exemplo

Os exemplos abaixo usam sintaxe bash (continuação de linha com `\`, JSON em aspas simples, `jq`). No Windows PowerShell, rode-os no Git Bash / WSL, troque `\` por backticks e ajuste as aspas, ou pule esta seção e use o Swagger UI (veja acima). O `curl` que vem no Windows é um alias para `Invoke-WebRequest` — use `curl.exe` explicitamente para usar o `curl` de verdade.

Execute as chamadas contra `http://localhost:8080`. Substitua `$TOKEN` pelo valor retornado por `/auth/token`.

### 1. Login

```bash
curl -X POST http://localhost:8080/auth/token \
  -H "Content-Type: application/json" \
  -d '{"username":"customer","password":"Customer@123"}'
```

Resposta:

```json
{ "token": "eyJhbGciOi...", "expiresAt": "2026-05-23T16:30:00+00:00" }
```

Exporte o token para as próximas chamadas:

```bash
TOKEN=$(curl -sX POST http://localhost:8080/auth/token \
  -H "Content-Type: application/json" \
  -d '{"username":"customer","password":"Customer@123"}' | jq -r .token)
```

### 2. Criar um pedido

```bash
curl -X POST http://localhost:8080/orders \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "currency": "USD",
    "items": [
      { "productId": "aaaaaaaa-0000-0000-0000-000000000001", "quantity": 2 },
      { "productId": "aaaaaaaa-0000-0000-0000-000000000003", "quantity": 1 }
    ]
  }'
```

Retorna `201 Created` com o corpo do pedido e o cabeçalho `Location`. Guarde o id do pedido (`$ORDER_ID`) para os próximos passos.

### 3. Confirmar um pedido

```bash
curl -X POST http://localhost:8080/orders/$ORDER_ID/confirm \
  -H "Authorization: Bearer $TOKEN"
```

Decrementa o estoque de cada item. Idempotente: confirmar um pedido já confirmado retorna o mesmo corpo sem alterar o estoque novamente.

### 4. Consultar um pedido

```bash
curl http://localhost:8080/orders/$ORDER_ID \
  -H "Authorization: Bearer $TOKEN"
```

Clientes só veem seus próprios pedidos; pedir um pedido de outro cliente retorna `404` (a existência não é vazada).

### 5. Listar pedidos

```bash
curl "http://localhost:8080/orders?status=Confirmed&page=1&pageSize=20" \
  -H "Authorization: Bearer $TOKEN"
```

Parâmetros de query: `customerId`, `status` (`Placed`/`Confirmed`/`Canceled`), `from` / `to` (timestamps ISO), `page` (padrão 1), `pageSize` (padrão 20, limitado a no máximo 100). Clientes são automaticamente escopados ao próprio id.

### 6. Cancelar um pedido

```bash
curl -X POST http://localhost:8080/orders/$ORDER_ID/cancel \
  -H "Authorization: Bearer $TOKEN"
```

Cancela a partir de `Placed` ou `Confirmed`. Se estiver cancelando um pedido `Confirmed`, o estoque é restaurado. Idempotente: cancelar um pedido já cancelado retorna sucesso sem nenhuma mudança adicional.

## Estrutura do projeto

```
OrderService.sln
├── src/
│   ├── OrderService.Domain/          # Entidades, enums, Result/Error, regras de domínio — sem refs externas
│   ├── OrderService.Application/     # Interfaces e implementações de serviços, DTOs, ports — referencia Domain
│   ├── OrderService.Infrastructure/  # EF Core, repositórios, gerador de JWT, password hasher — referencia Application
│   └── OrderService.Api/             # Controllers, middleware, Program.cs — referencia Application + Infrastructure
└── tests/
    ├── OrderService.Domain.Tests/        # Testes puros de regras de domínio
    └── OrderService.Application.Tests/   # Testes de serviços com mocks NSubstitute
```

A direção das dependências é estrita: `Domain` ← `Application` ← `Infrastructure` ← `Api`. Domain não tem referências; a API depende apenas de Application e Infrastructure.

Veja [`docs/decisions.md`](docs/decisions.md) para as decisões arquiteturais por trás dessa estrutura.
