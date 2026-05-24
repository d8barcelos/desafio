# Decisões Arquiteturais

Cada decisão é registrada como um ADR curto: contexto, decisão, consequências.

## ADR-001: Clean Architecture com 4 projetos

**Contexto.** O serviço combina uma API REST, persistência, lógica de domínio e autenticação. Queremos limites claros que mantenham o modelo de domínio alheio às preocupações de HTTP, EF Core e JWT.

**Decisão.** Dividir a solução em `Domain`, `Application`, `Infrastructure` e `Api`. As dependências fluem apenas para dentro (Api → Infrastructure → Application → Domain). As interfaces de repositório, password hasher, gerador de JWT e current-user ficam em Application; os adaptadores concretos ficam em Infrastructure.

**Consequências.** Fica fácil testar Domain e Application isoladamente (sem I/O). Adicionar um novo transporte (e.g., gRPC) ou uma nova persistência (e.g., SQL Server) exige mudanças apenas nos anéis externos. O trade-off é mais arquivos e indireções do que em um layout monoprojeto — aceitável para o escopo deste desafio.

## ADR-002: `Result<T>` em vez de exceções para falhas de negócio previsíveis

**Contexto.** Operações de negócio têm modos de falha bem conhecidos (entrada inválida, recurso ausente, estado conflitante, acesso proibido). Em C# o caminho idiomático seria sinalizar isso com exceções — e exceções têm uma vantagem real sobre códigos de retorno: obrigam o chamador a tratar a falha ou propagá-la explicitamente, em vez de simplesmente ignorá-la. O problema é que C#, ao contrário de Java, não tem _checked exceptions_, então a única forma de saber o que um método pode lançar é ler a documentação humana (que pode estar desatualizada) ou o código-fonte. Além disso, C# não separa "erros de lógica/programação" (estado inválido, precondição quebrada, bug) de "falhas previsíveis no fluxo normal" (entrada inválida, recurso não encontrado, conflito de estado): tudo herda de `Exception`. Na prática isso empurra para dois antipadrões — `catch (Exception)` genéricos que engolem bugs junto com falhas legítimas, e `try/catch` espalhado pelo fluxo de regra de negócio prejudicando legibilidade. A decisão do design de C# de não adotar _checked exceptions_ (Anders Hejlsberg argumentou que aumentavam verbosidade e fricção sem entregar tanto benefício, dado o quanto de código Java acaba em `catch (Exception) { log }`) é razoável, mas deixa um vácuo no caso específico de erros de negócio previsíveis.

**Decisão.** Adotar uma divisão explícita: para falhas previsíveis nas camadas de domínio e Application, retornar `Result` / `Result<T>`; para situações *inesperadas* (estado corrompido, falhas de infraestrutura, violação de precondição) continuar usando exceções. Isso recupera o benefício que faltava — a assinatura do método deixa explícito no tipo de retorno quais falhas existem, e o consumidor é obrigado pelo compilador a desconstruir o `Result` antes de acessar o valor — sem pagar o custo de _checked exceptions_. A divisão tem precedente em Rust e Go, que vão por essa mesma rota de "código de retorno para erros de domínio + exceções/panics para o inesperado". O helper `ResultExtensions` na camada de API mapeia os códigos de erro para status codes HTTP em um único lugar.

**Consequências.** As assinaturas dos serviços ficam honestas sobre o que pode dar errado, sem depender de documentação humana ou da leitura do corpo do método. A camada de API tem um mapeamento único, table-driven, de código de erro para status code, eliminando `try/catch` repetitivos nos controllers. Erros de domínio são traduzidos para erros de Application antes de serem retornados (e.g., um `invalid_transition` de domínio vira um `conflict`), mantendo a camada externa desacoplada do vocabulário do domínio. O trade-off é uma camada extra de tipos (`Result`, `Error`) e mais verbosidade no call-site do que um `throw` + `try/catch` global — aceitável porque a maioria das operações de domínio aqui tem múltiplos modos de falha previsíveis, e tratar cada um explicitamente é exatamente o ponto.

## ADR-003: Concorrência otimista sobre estoque via `xmin` do PostgreSQL

**Contexto.** Dois confirmes concorrentes sobre o mesmo pedido — ou duas escritas sobre o mesmo produto — poderiam disputar e decrementar o estoque duas vezes. Lock pessimista via `SELECT ... FOR UPDATE` serializaria produtos populares e prejudicaria a vazão.

**Decisão.** Mapear a system column `xmin` do PostgreSQL como um token de concorrência `uint` em `Order` e `Product`. O EF Core compara o valor no UPDATE; se mudou, o update afeta zero linhas e o EF lança `DbUpdateConcurrencyException`. O `UnitOfWork` da infraestrutura captura essa exceção e a relança como `ConcurrencyConflictException` (mais amigável ao domínio). O serviço de Application captura essa última e retorna `Result.Failure(ApplicationErrors.Conflict("Concurrent modification, please retry."))`, que é mapeado para HTTP 409.

**Consequências.** Sem overhead de lock no caso comum. Conflitos de concorrência chegam ao chamador como um erro claro e retentável. O custo é que os clientes precisam tratar 409 com retry — apropriado para uma API REST stateless.

## ADR-004: Idempotência baseada em estado para confirm/cancel

**Contexto.** Retries de rede são rotineiros. Precisamos que `confirm` e `cancel` sejam seguros de chamar mais de uma vez sem dupla baixa de estoque ou estado inconsistente.

**Decisão.** A idempotência é codificada na entidade de domínio. `Order.Confirm()` em um pedido já `Confirmed` retorna sucesso sem mudança de estado; `Order.Cancel()` em um pedido já `Canceled` retorna sucesso sem mudança de estado. O serviço de Application pula os efeitos colaterais de estoque quando a transição é um no-op. Sem cabeçalhos `Idempotency-Key`, sem store separado de deduplicação de requisições.

**Consequências.** Mais simples do que idempotência por cabeçalho e suficiente para as operações envolvidas (state machines com estados terminais). O trade-off: a idempotência só é garantida para transições de estado, não para `POST /orders` (criar um pedido duas vezes com o mesmo payload cria dois pedidos). Idempotência de criação exigiria uma chave explícita — fora do escopo deste desafio.

## ADR-005: JWT com chave simétrica

**Contexto.** O desafio exige autenticação e autorização baseada em papéis. Precisamos identificar o chamador e restringir acesso por papel (Admin / Customer) e por posse do cliente.

**Decisão.** Emitir JWTs de curta duração assinados com HMAC-SHA256 e uma chave simétrica configurada via `Jwt:Key`. As claims incluem o id do usuário, o papel e (para clientes) uma claim `customer_id`. `ICurrentUser` lê as claims de `HttpContext.User`; a camada de Application a usa para aplicar regras de posse.

**Consequências.** Setup trivial, sem necessidade de provedor de identidade externo. O trade-off é o gerenciamento do segredo compartilhado: a mesma chave precisa estar configurada onde quer que a API rode (instância única é tranquilo; para um setup multi-tenant ou federado, chaves assimétricas via JWKS seriam o caminho certo). Tokens não podem ser revogados antes de expirar — aceitável dado o tempo de vida padrão de 60 minutos.
