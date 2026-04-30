# Coding Standards — BoardGame Collection Insights (C#)

These are guidelines, not rigid rules. The goal is consistency and clarity. When in doubt, favour readability and explicitness over cleverness.

---

## 1. Project & Folder Structure

- One type per file. The file name must match the type name exactly (e.g. `CollectedGame.cs` contains `class CollectedGame`).
- Folder structure within a project mirrors the namespace structure.
- Each bounded context (BC) is a self-contained set of four projects: `Domain`, `Application`, `Infrastructure`, and the shared `Api` composition root.

```
src/
├── Api/                              # Composition root only — no business logic
├── GameCollection.Domain/            # Aggregates, Value Objects, Events, Repository interfaces
├── GameCollection.Application/       # Commands, Queries, Handlers, DTOs
├── GameCollection.Infrastructure/    # Repository implementations, DB migrations
├── BggIntegration.Domain/            # BGG models, IBggClient, IBggTranslator (ACL)
├── BggIntegration.Application/       # BGG query handlers
└── BggIntegration.Infrastructure/    # HTTP client, XML parser
```

- **No cross-BC project references** except through the Anti-Corruption Layer. `BggIntegration` may reference `GameCollection.Application` only to produce `AddGameFromBggCommand` via `IBggTranslator`. It must never reference `GameCollection.Domain` directly.
- Infrastructure projects must not be referenced by Application or Domain projects. Dependency flow is always inward: `Api → Application → Domain`.

---

## 2. DDD Rules

### Aggregates

- The aggregate root is the **only** entry point for mutating state. Never set properties on a domain object from outside the aggregate.
- Public setters are not permitted on aggregate properties. All state changes go through explicit methods (e.g. `UpdateDetails(...)`).
- Aggregates are reconstituted from persistence using a dedicated static factory method (`Reconstitute(...)`) that bypasses domain invariant checks. The `Create(...)` factory enforces all invariants and raises domain events.

### Value Objects

- Implemented as C# `record` types.
- Validation belongs in the constructor. A Value Object must never be in an invalid state.
- Value Objects are immutable — no mutation methods.

```csharp
// Correct
public record GameName
{
    public string Value { get; }
    public GameName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Game name cannot be empty.", nameof(value));
        Value = value.Trim();
    }
}
```

### Domain Events

- Domain events are raised **inside the aggregate**, not from handlers or services.
- Events are stored in a private `_domainEvents` list on the aggregate and cleared after being dispatched.
- Event names are past tense and describe something that happened: `GameAddedToCollection`, not `AddGame`.

### Repository Interfaces

- Repository interfaces are defined in the `Domain` layer. They describe what the domain needs, in domain language.
- Implementations live in `Infrastructure`. The domain has no knowledge of Dapper, SQLite, or any persistence concern.
- Repository methods accept `CancellationToken` and are always async.

---

## 3. CQRS / MediatR Rules

### Commands vs Queries

| | Command | Query |
|---|---|---|
| **Intent** | Change state | Read state |
| **Returns** | `void`, `Guid`, or a minimal result | A DTO or list of DTOs |
| **Side effects** | Yes | None |

- Commands and Queries are plain C# `record` types implementing `IRequest<T>`.
- Never return a domain object (`CollectedGame`, Value Objects) from a Query. Always map to a DTO first.
- Never perform writes inside a Query handler.

### Naming

| Type | Convention | Example |
|---|---|---|
| Command | `<Action><Subject>Command` | `AddGameManuallyCommand` |
| Query | `<Get><Subject>Query` | `GetAllGamesQuery` |
| Handler | `<MessageName>Handler` | `AddGameManuallyCommandHandler` |
| DTO | `<Subject>Dto` | `CollectedGameDto` |

### Handler Rules

- One handler per Command or Query. No handler handles more than one message type.
- Handlers live in the `Application` layer — never in `Api` or `Infrastructure`.
- Handlers receive dependencies through constructor injection. They must not use service locator or resolve services manually.
- A handler's `Handle` method should read as a clear description of the use case. If it becomes long, extract private methods — do not add a second public method.

### Controllers

- Controllers are HTTP adapters only. They construct the message, call `_mediator.Send()`, and map the result to an HTTP response.
- No business logic, validation, or domain knowledge belongs in a controller.

```csharp
// Correct
[HttpDelete("{id:guid}")]
public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
{
    await _mediator.Send(new DeleteGameCommand(id), ct);
    return NoContent();
}

// Incorrect — business logic in the controller
[HttpDelete("{id:guid}")]
public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
{
    var game = await _repository.GetByIdAsync(GameId.From(id), ct);
    if (game is null) return NotFound();
    await _repository.DeleteAsync(GameId.From(id), ct);
    return NoContent();
}
```

---

## 4. Error Handling

- **Domain exceptions** (e.g. `ArgumentException`) are thrown from Value Object constructors and aggregate methods when an invariant is violated. These represent programming errors or invalid input, not recoverable application states.
- **`KeyNotFoundException`** is thrown from handlers when a requested entity does not exist. Controllers catch this and return `404 Not Found`.
- Controllers are responsible for translating exceptions into appropriate HTTP responses. Raw exception messages must never be returned to the client in production.
- Do not swallow exceptions silently. If an exception is caught and not rethrown, there must be a comment explaining why.
- Avoid empty `catch` blocks.

```csharp
// Correct — controller translates the domain exception
try
{
    await _mediator.Send(command, ct);
    return NoContent();
}
catch (KeyNotFoundException)
{
    return NotFound();
}
```

---

## 5. Async Conventions

- All methods that perform I/O must be `async Task` or `async Task<T>`. No synchronous wrappers around async code.
- Never use `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` on a `Task`. This risks deadlocks and defeats the purpose of async.
- `CancellationToken` must be accepted as a parameter on all async public methods and passed through to every downstream async call.
- `async void` is not permitted except in UI event handlers. It prevents exceptions from being observed and cannot be awaited.
- Suffix async method names with `Async` (e.g. `GetAllAsync`, `MigrateAsync`).

```csharp
// Correct
public async Task<CollectedGame?> GetByIdAsync(GameId id, CancellationToken cancellationToken = default)
{
    using var conn = CreateConnection();
    var row = await conn.QuerySingleOrDefaultAsync<GameRow>(...);
    return row is null ? null : MapToDomain(row);
}

// Incorrect
public CollectedGame? GetById(GameId id)
{
    return GetByIdAsync(id).Result; // deadlock risk
}
```

---

## 6. Dependency Injection

- Each `Infrastructure` project exposes a single static extension method on `IServiceCollection` in a file named `DependencyInjection.cs`. This is the only public registration surface for that project.

```csharp
// GameCollection.Infrastructure/DependencyInjection.cs
public static class DependencyInjection
{
    public static IServiceCollection AddGameCollectionInfrastructure(
        this IServiceCollection services, string connectionString)
    {
        services.AddScoped<ICollectedGameRepository>(
            _ => new SqliteCollectedGameRepository(connectionString));
        services.AddSingleton(new DatabaseMigrator(connectionString));
        return services;
    }
}
```

- `Program.cs` is the **only** place where `DependencyInjection` extension methods are called. Service registration must not be scattered across the codebase.
- Never use the service locator pattern (`IServiceProvider.GetService<T>()`) outside of `Program.cs` or factory methods that have no other option.
- Domain and Application layers must never reference `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Hosting`, or any DI container. They are container-agnostic.
- **Background services** (types inheriting `BackgroundService` or implementing `IHostedService`) belong in the **Infrastructure** layer. They interact with the hosting framework, manage DI scope lifetimes via `IServiceScopeFactory`, and drive OS-level timers — all infrastructure concerns. The application logic they trigger (e.g. syncing a collection) must be extracted into an Application service and injected as a dependency.
- Prefer `AddScoped` for services that involve a unit of work (e.g. repositories). Use `AddSingleton` only for genuinely stateless or thread-safe services.
