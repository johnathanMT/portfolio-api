# PortfolioApi — Architecture Guide

A concise, enforceable convention for this .NET 8 API. Read this before adding a
new endpoint. The goal is a codebase that stays predictable as it grows: every
feature looks like every other feature, and responsibilities never blur.

---

## 1. The 3-Layer Convention

Requests flow in one direction only:

```
HTTP request → Controller → Service → Repository → EF Core / SQL → Database
```

Each layer has a single responsibility and depends only on the layer directly
below it (via an interface, never a concrete type).

**Controller — the HTTP boundary.**
Owns routing, model binding, auth attributes (`[Authorize]`, `[EnableRateLimiting]`),
input validation (FluentValidation), reading request headers, and shaping the HTTP
response (status code + envelope). It contains **no business rules and no data
access**. A controller action should read like a short script: validate → call a
service method → map the result to a response.

**Service — the business layer.**
Owns the rules: sanitization, hashing/identity, authorization decisions (e.g.
privacy masking), domain calculations (e.g. plot-layout maths), and orchestration
across one or more repositories. It accepts DTOs / primitives and returns domain
entities or typed view models. It **never** touches `HttpContext` and **never**
writes EF queries.

**Repository — the data layer.**
Owns persistence only: EF Core (or raw SQL) reads and writes. Returns entities or
simple value records. It knows nothing about HTTP, validation, hashing, or business
rules. Keep methods narrow and intention-revealing (`FindByOperatorAsync`,
`CountAsync`), not generic `IQueryable` leaks.

> **Reference implementations** (study these before writing a new one):
> - `PoetryController` / `PoemService` / `PoemRepository` — simple CRUD.
> - `SanctuaryController` / `MemoryService` / `MemoryRepository` — logic-heavy
>   (SHA-256 ownership, server-side privacy masking, one-row-per-operator).
> - `FarewellController` / `FarewellService` / `FarewellRepository` — domain logic
>   (ring-layout plot assignment, conditional fields).
> - `VisitorsController` / `VisitorService` / `VisitorRepository` — raw-SQL storage.

---

## 2. Rule of Thumb — when to add layers

Be deliberate. Inconsistency-by-accident is the thing to avoid, not the existence
of a simpler path.

- **Use the full 3-layer stack** whenever a feature has *any* real logic: input
  sanitization, hashing, authorization/masking decisions, multi-step writes,
  transactions, calculations, or rules that will plausibly grow. **Default to this.**
- **A thin controller that talks to a repository directly (skipping the service)**
  is acceptable *only* when the action is genuinely mechanical — a pass-through read
  or a trivial write with no rules. The moment a second rule appears, introduce the
  service.
- **Never** put EF Core / raw SQL directly in a controller, regardless of how simple
  the feature is. Data access always lives behind a repository interface so it can
  be swapped, mocked, and reasoned about in isolation.

If you are unsure, add the service. The cost of an extra interface is small; the
cost of business logic leaking into controllers is high.

---

## 3. Standardization

- **DbContext is banned in controllers.** Controllers depend on `IXxxService`
  (and, for thin reads, may depend on `IXxxRepository`) — never on `AppDbContext`.
  This is a hard rule; a controller referencing `_db` is a review blocker.

- **DTOs and view models are shared, not inline.** Request payloads live in
  `DTOs/` (e.g. `PoemDto`, `CreateMemoryDto`); response/read shapes are typed
  records in `DTOs/` (e.g. `MemoryView`, `FarewellWriteResult`). Do not declare
  DTO/validator classes inside a controller file.

- **Validation is declarative.** One `AbstractValidator<TDto>` per request DTO in
  `Validators/`, auto-registered via `AddValidatorsFromAssembly`. Controllers call
  `await _validator.ValidateAsync(dto)` and return `400` on failure.

- **Standard response envelope: `ApiResponse<T>` (`Common/ApiResponse.cs`).**
  Every endpoint should return the consistent shape:

  ```csharp
  return Ok(ApiResponse<PoemView>.Ok(poem));           // 200 + data
  return Ok(ApiResponse<object>.Fail("Not found", 404));
  ```

  The envelope guarantees the frontend always receives
  `{ success, message, data, errors, statusCode }`.

  > **Known deviation / convergence target:** the Sanctuary-cluster controllers
  > (memories, farewell, poetry, visitors) currently emit ad-hoc anonymous
  > envelopes (`new { success = true, … }`) to preserve an existing frontend
  > contract. New endpoints should prefer `ApiResponse<T>`; migrate the older ones
  > only alongside a coordinated frontend change.

- **DI registration is centralized** in `Program.cs`, repositories then services,
  one `AddScoped<IInterface, Impl>()` per line, grouped and aligned.

- **Sanitization rule:** strip control chars + `<` / `>`, hard length-cap, and do
  **not** HTML-encode (React renders as auto-escaped text). This logic lives in the
  service, never the controller.

---

## 4. Checklist — adding a new feature/controller

Work bottom-up (data → business → HTTP), registering as you go:

1. **Model** — add/confirm the entity in `Models/`; register `DbSet<T>` and any
   config in `Data/AppDbContext.cs`. Create an EF migration if the schema changed
   (`dotnet ef migrations add <Name>`).
2. **DTOs** — request DTO + typed read/result records in `DTOs/`.
3. **Validator** — `AbstractValidator<TDto>` in `Validators/`.
4. **Repository** — `IXxxRepository` in `Interfaces/` + `XxxRepository` in
   `Repositories/` (EF/SQL only).
5. **Service** — `IXxxService` in `Interfaces/` + `XxxService` in `Services/`
   (rules, sanitization, mapping, orchestration).
6. **Controller** — thin `XxxController` in `Controllers/`: auth/rate-limit
   attributes, validate, call the service, return an `ApiResponse<T>`.
7. **DI** — register the repository **and** service in `Program.cs`.
8. **Verify** — confirm the controller has **zero** `AppDbContext` / `_db`
   references, then `dotnet build` and smoke-test the endpoints.

### Definition of done
- [ ] Controller contains no EF Core / raw SQL and no business rules.
- [ ] All data access sits behind a repository interface.
- [ ] Business rules (sanitize, hash, auth decisions, calculations) live in the service.
- [ ] DTOs/validators are in `DTOs/` and `Validators/`, not inline.
- [ ] Response uses the standard envelope.
- [ ] Repository **and** service are registered in `Program.cs`.
- [ ] `dotnet build` passes; protected endpoints reject anonymous callers.
