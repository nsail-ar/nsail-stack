# Injection

Dependency-injection registration by declaration. A class says `[Injectable]`; the source
generator writes the `services.Add...` calls into a partial method, so nobody keeps a list of
registrations in step with the classes.

## Packages

| Package | What it is | Referenced by |
|---|---|---|
| `NSail.Injection.Annotations` | `[Injectable]`, in namespace `NSail.Injection.Annotations` | Web (handlers and services), any project that declares an injectable |

It depends only on `Microsoft.Extensions.DependencyInjection.Abstractions`. The code that reads the
attribute is the `Services.Registration` target of `NSail.Messaging.SourceGenerator`
([Generation](../Generation/README.md)).

## Use it

Mark the class, and declare once per project the partial method that receives the registrations:

```csharp
[Injectable]
public sealed class ProjectHandler : IHandler<GetProject, ProjectModel> { ... }

internal static partial class Dependencies
{
    [Generated(Services.Registration)]
    internal static partial void AddTasksServices(this IServiceCollection services);
}
```

Call `services.AddTasksServices()` from the project's composition (`AddTasksWebApi`).

## Configuration

`[Injectable]` is allowed more than once on a class (`AllowMultiple`), and each use registers on its
own:

| Property | Default | Meaning |
|---|---|---|
| `As` | none | `Type[]`: register the class only as these service types |
| `Lifetime` | `Scoped` when omitted | `ServiceLifetime.Singleton`, `Scoped` or `Transient` |

What is emitted for one use:

- **No `As`:** the class as itself, plus every interface it implements, each resolved from the
  class's own registration (`sp.GetRequiredService<Class>()`), so one instance serves all of them.
  If the base class is also `[Injectable]`, the base type is aliased the same way.
- **With `As`:** one `Add{Lifetime}<Service, Class>()` per listed type, and nothing else.

```csharp
[Injectable(Lifetime = ServiceLifetime.Singleton)]
[Injectable(As = [typeof(IValidator<UpdateProduct>)])]   // a second use, its own registration
```

Scope of the scan is the holder's, not the attribute's. By default it is every non-abstract class of
the compilation that declares the holder. `[Source(Assembly = "...", Pattern = "...")]` on the
partial method replaces that with the named referenced assembly (repeatable); `Pattern` matches the
full type name segment by segment, `*` for one segment and `**` for any number:

```csharp
[Generated(Services.Registration)]
[Source(Assembly = "Acme.Tasks.WebApi", Pattern = "Acme.Tasks.**")]
private static partial void AddTasksServices(this IServiceCollection services);
```

Abstract classes are skipped.

## How it fits

- Handlers are `[Injectable]`; `Http.Endpoints` and `Http.InProcess` then find them:
  [Messaging](../Messaging/README.md). A rule another module adds to a message is registered the
  same way with `As = [typeof(IValidator<T>)]`.
- The generator, the `[Generated]` and `[Source]` attributes and the other targets:
  [Generation](../Generation/README.md).

## Going deeper

- Guide: [Getting started](../../../docs/getting-started.md) (step 4, the handler)
- Doctrine: [generation.md](../../../plugins/nsail-stack/doctrine/generation.md) (Injectable)
- Sample: [Dependencies.cs](../Sample/NSail.Sample.Web/Dependencies.cs) and
  [ContactHandler.cs](../Sample/NSail.Sample.Web/Contacts/ContactHandler.cs)
- Tests: [Services registration](../../../test/Stack/Generation/NSail.SourceGenerator.Tests/Services)
