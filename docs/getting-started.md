# Getting started

A small project tracker — projects that own a list of tasks — built from the packages on
nuget.org, with a Blazor WebAssembly client. It takes four projects and no hand-written wiring.

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download), PostgreSQL on `localhost`
(user `postgres`; the password, if your server asks for one, in `PGPASSWORD`) and the EF Core
tools (`dotnet tool install -g dotnet-ef`).

## The shape of a solution

| Project | Holds | References |
|---|---|---|
| `Acme.Tasks.Sdk` | The messages and the models they answer — the contract | Messaging, Security |
| `Acme.Tasks.Data` | Entities, the `DbContext`, migrations, the mapper declarations | Sdk, Data |
| `Acme.Tasks.Wasm` | The Blazor client: screens, menu, texts | Sdk, Components |
| `Acme.Tasks.Web` | The host: handlers, endpoints, enforcement, serves the client | all of the above |

The Sdk is the only project both sides see. The client never references the handlers or the
entities; the host never references a screen except to serve it.

## 1. One version for every package

`Directory.Build.props` at the solution root:

```xml
<Project>

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>Acme.Tasks</RootNamespace>
    <!-- the latest version on nuget.org -->
    <NSailStackVersion>0.1.x</NSailStackVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="NSail.Messaging.SourceGenerator" Version="$(NSailStackVersion)" PrivateAssets="all" />
  </ItemGroup>

</Project>
```

- **Pin one version** for every `NSail.*` package; they are released together.
- **The generator is an analyzer, referenced by every project.** It writes only where a project
  declares a `[Generated]` holder, and `PrivateAssets="all"` keeps it out of your consumers.
- **One `RootNamespace` for all four projects.** Keys — routes, strings, permissions — derive
  from the namespace (`{Root}.{Area}.{Feature}.{Object}`), so `Acme.Tasks.Projects.CreateProject`
  is area `Tasks`, feature `Projects`, whichever project declares it. Without it each project
  gets its assembly name as root namespace and a Razor page lands in `Acme.Tasks.Wasm.Projects`,
  away from the messages it sends.

## 2. The contract: messages

`Acme.Tasks.Sdk.csproj` references `NSail.SourceGeneration.Annotations`,
`NSail.Messaging.Annotations`, `NSail.Messaging.Http` and `NSail.Security`, and brings the
attribute namespaces into scope:

```xml
<ItemGroup>
  <Using Include="NSail.Messaging.Annotations" />
  <Using Include="NSail.Messaging.Annotations.Method" Static="true" />
</ItemGroup>
```

A message is a class that names its route, carries its validation, and says what it answers:

```csharp
namespace Acme.Tasks.Projects;

[Http(Post, "api/tasks/projects")]
public class CreateProject : IMessage
{
    public required Guid Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public DateOnly? DueDate { get; set; }
}

[Http(Put, "api/tasks/projects/{id}")]
public class UpdateProject : IMessage
{
    [AsRoute]
    public required Guid Id { get; set; }

    [Unbounded("An opaque concurrency token handed back unread.")]
    public uint Version { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public DateOnly? DueDate { get; set; }

    [MaxLength(200)]
    public List<TaskItemModel> Tasks { get; set; } = [];
}

[Http(Get, "api/tasks/projects")]
public class ListProjects : PagedMessage, IMessage<DataPage<ProjectRow>>
{
    [SearchTerm]
    public string? Search { get; set; }
}
```

`GetProject` (answers a `ProjectModel`) and `DeleteProject` follow the same shape. Create and
Update are two messages, never one `Save`: each says exactly what its act may change
([Messages](messages.md)).

Two holders tell the generator what to write for the clients and for the permission system:

```csharp
public static partial class Clients
{
    [Generated(Http.Clients)]
    public static partial void AddTasksHttpClients(this IServiceCollection services);
}

public static partial class Permissions
{
    [Generated(Policies.Handlers)]
    public static partial void AddTasksPermissions(this IServiceCollection services);
}
```

## 3. Data: entities that declare their mappings

`Acme.Tasks.Data.csproj` references `NSail.Data`, `NSail.Mapping.Annotations` and
`Npgsql.EntityFrameworkCore.PostgreSQL`.

```csharp
[MapFrom(typeof(CreateProject))]
[MapFrom(typeof(UpdateProject))]
[MapTo(typeof(ProjectRow))]
[MapTo(typeof(ProjectModel))]
public class Project : IVersioned, IAudited
{
    public Guid Id { get; set; }

    public uint Version { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateOnly? DueDate { get; set; }

    [Composition]
    public List<TaskItem> Tasks { get; set; } = [];

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

[MapFrom(typeof(TaskItemModel))]
[MapTo(typeof(TaskItemModel))]
public class TaskItem
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    [Parent]
    public Project? Project { get; set; }

    public string Title { get; set; } = string.Empty;

    public bool IsDone { get; set; }
}
```

- **The entity names the message, never the reverse** — the Sdk cannot see the Data project.
- **`IVersioned`** makes `Version` the row's concurrency token (on PostgreSQL, the row's own
  `xmin`: no column is added). **`IAudited`** has the save stamp `CreatedAt`/`UpdatedAt`.
- **`[Composition]`** says the project owns its tasks: they are saved, replaced and deleted with
  it ([Mapping](mapping.md)).

The `DbContext` composes its model from `IDbContextSetup`s and takes the Stack's conventions:

```csharp
public class TasksDbContext : DbContext, ITenanted, IHasOrgScope
{
    readonly IEnumerable<IDbContextSetup> _setups;
    readonly TenancyProvider _tenancy;
    readonly OrgScopeProvider _orgs;

    public TasksDbContext(DbContextOptions<TasksDbContext> options, IEnumerable<IDbContextSetup> setups,
        TenancyProvider? tenancy = null, OrgScopeProvider? orgs = null)
        : base(options)
    {
        _setups = setups;
        _tenancy = tenancy ?? TenancyProvider.Install;
        _orgs = orgs ?? OrgScopeProvider.Ambient;
    }

    public Guid? TenantId
    {
        get { return _tenancy.RowKey; }
    }

    public OrgScope OrgScope
    {
        get { return _orgs.Current; }
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseAuditStamps();
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.ApplyTenancy();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplySetups(this, _setups);
    }
}
```

`DbContextSetup : IDbContextSetup` holds the EF configuration (tables, lengths, the
`HasMany(x => x.Tasks).WithOne(x => x.Project)` relation). A mapper holder and a `Persistence`
extension register both:

```csharp
public static partial class Mappings
{
    [Generated(Mappers.Entities)]
    public static partial void AddTasksMappings(this IServiceCollection services);
}

public static class Persistence
{
    public static IServiceCollection AddTasksData(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDbContextSetup, DbContextSetup>());
        services.AddTasksMappings();
        services.TryAddScoped<EntityMapper>();

        return services;
    }
}
```

Add an `IDesignTimeDbContextFactory<TasksDbContext>` (it builds the context with
`[new DbContextSetup()]`) and scaffold the first migration:

```bash
dotnet ef migrations add InitialCreate --project src/Acme.Tasks.Data --startup-project src/Acme.Tasks.Data
```

## 4. The handler

```csharp
[Injectable]
public sealed class ProjectHandler :
    IHandler<ListProjects, DataPage<ProjectRow>>,
    IHandler<GetProject, ProjectModel>,
    IHandler<CreateProject>,
    IHandler<UpdateProject>
{
    readonly DbContext _db;
    readonly EntityMapper _mapper;

    public ProjectHandler(DbContext db, EntityMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    public async Task<ProjectModel> Handle(GetProject message, CancellationToken cancellationToken = default)
    {
        var query = _db.Set<Project>().AsNoTracking().Where(p => p.Id == message.Id);

        return await _mapper.Project<Project, ProjectModel>(query).FirstOrDefaultAsync(cancellationToken)
            ?? throw new BusinessException(BusinessProblem.NotFound(typeof(Project), message.Id));
    }

    public async Task Handle(CreateProject message, CancellationToken cancellationToken = default)
    {
        if (await _db.Set<Project>().AnyAsync(p => p.Id == message.Id, cancellationToken))
        {
            throw new BusinessException(BusinessProblem.AlreadyExists(typeof(Project), message.Id));
        }

        await _mapper.Upsert<CreateProject, Project>(message, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task Handle(UpdateProject message, CancellationToken cancellationToken = default)
    {
        await _mapper.Map<UpdateProject, Project>(message, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    // ListProjects: filter and page the query, then _mapper.Project<Project, ProjectRow>(page)
}
```

`[Injectable]` is the registration; there is no `AddScoped` to write. `Map` loads the project
with its tasks, merges the message onto it — matching tasks by id, creating the ones with no id,
deleting the ones the message no longer carries — and rides the `Version` into the `UPDATE`, so a
stale screen answers **409 Conflict**. A missing row answers **404**, an invalid message **400**,
before the handler runs.

## 5. The host

`Acme.Tasks.Web.csproj` (`Microsoft.NET.Sdk.Web`) references `NSail.BaseServices.WebApi`,
`NSail.BaseServices.WebApp`, `NSail.Configuration`, `NSail.Localization`,
`Microsoft.AspNetCore.Components.WebAssembly.Server`, and the three other projects.

```csharp
internal static partial class Generated
{
    [Generated(Services.Registration)]
    internal static partial void AddTasksServices(this IServiceCollection services);

    [Generated(Http.Endpoints)]
    internal static partial void AddTasksEndpoints(this IServiceCollection services);

    [Generated(Http.InProcess)]
    internal static partial void AddTasksInProcessClients(this IServiceCollection services);
}
```

| Holder | Writes |
|---|---|
| `Services.Registration` | the DI registration of every `[Injectable]` |
| `Http.Endpoints` | a minimal-API endpoint per `[Http]` message |
| `Http.InProcess` | the sender that dispatches a message straight to its handler on this host (prerendering, composition) |
| `Http.Clients` (in the Sdk) | the sender that sends it over HTTP from the browser |

```csharp
public static class Web
{
    public static void AddTasksWebApi(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.Load<TasksOptions>();   // { ConnectionString } from "Tasks"

        services.AddTasksData();
        services.AddDataAccess<TasksDbContext>(options.ConnectionString, configuration.Load<TenancyOptions>());
        services.AddAuthentication();

        services.AddTasksServices();
        services.AddTasksEndpoints();
        services.AddTasksInProcessClients();

        services.AddSecurityEnforcement();
        services.AddTasksPermissions();
        services.AddTasksPolicies();

        services.AddSingleton(configuration.Load<LanguageOptions>());
    }
}
```

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddBaseWebApi();
builder.AddBaseWebApp();

builder.Services.AddTasksWebApi(builder.Configuration);
builder.Services.AddTasksWebApp();

var app = builder.Build();

await app.ApplyMigrations<TasksDbContext>();

app.UseBaseWebApi();
app.UseBaseWebApp();
app.MapRazorApp<App>();

app.Run();
```

`appsettings.json` carries `"Tasks": { "ConnectionString": "Host=localhost;Database=acme_tasks;Username=postgres" }`
and `"Language": { "Default": "es" }`. `App.razor` is `NsApp` with `NsAssets`, `NsSplash`,
`NsSetup` and the client's `Routes`, all `InteractiveWebAssembly`.

## 6. Who may send what

Every message is denied until a policy grants it — there is no "open" endpoint. An app without
sign-in grants its messages to everyone with one built-in policy, registered on both sides
(the server enforces it, the client draws by it):

```csharp
public static void AddTasksPolicies(this IServiceCollection services)
{
    services.AddBuiltInPolicy(new Policy
    {
        Name = "Tasks.Policies.Everyone",
        Messages = ["Tasks.*"],
    });
}
```

An app with sign-in composes an identity provider and authors real policies instead
([Permissions](permissions.md)).

## 7. The Blazor client

`Acme.Tasks.Wasm.csproj` (`Microsoft.NET.Sdk.BlazorWebAssembly`) references
`NSail.BaseServices.Wasm`, `NSail.Components.Mud` and the Sdk, and embeds its texts:

```xml
<ItemGroup>
  <Content Remove="strings*.json" />
  <EmbeddedResource Include="strings*.json" />
  <EmbeddedResource Update="strings.*.json">
    <DependentUpon>strings.json</DependentUpon>
    <LogicalName>$(RootNamespace).%(Filename)%(Extension)</LogicalName>
    <WithCulture>false</WithCulture>
  </EmbeddedResource>
</ItemGroup>
```

A page inherits `NsPage`, is guarded by the message it sends, and binds the message itself — the
form validates with the message's own annotations, the same ones the server checks:

```razor
@page "/projects/new"
@attribute [Authorize<CreateProject>]
@inherits NsPage

<NsForm TModel="CreateProject" Model="_model" OnSubmit="OnSubmit">
    <NsPanel>
        <Header>
            <NsTitleBar Title="@GetTitle()" />
        </Header>
        <Content>
            <NsFormGrid>
                <NsFormGridItem Grow>
                    <NsTextField @bind-Value="_model.Name" AutoFocus />
                </NsFormGridItem>
                <NsFormGridItem>
                    <NsDateField TValue="DateOnly?" @bind-Value="_model.DueDate" />
                </NsFormGridItem>
            </NsFormGrid>
        </Content>
        <Footer>
            <NsSubmit />
        </Footer>
    </NsPanel>
</NsForm>

@code {
    CreateProject _model = new() { Id = Guid.NewGuid() };

    async Task OnSubmit(SubmitEventArgs<CreateProject> args)
    {
        await Send(args.Model, args.CancellationToken);
        await Publish(new ProjectSaved { Id = args.Model.Id }, args.CancellationToken);
    }
}
```

`ProjectSaved` is a UI event (`ISaved`, declared in the client): the list page subscribes to it
and reloads, so no screen calls another ([Events](events.md)).

No field names a label: `NsTextField` bound to `CreateProject.Name` reads
`Tasks.CreateProject.Name` from `strings.json` (and `strings.es.json` for Spanish). A list is
`NsTable` over `DataPage<ProjectRow>`; the tasks of a project are an `NsListEditor` over
`context.Tasks` inside the update form, saved with it in one `UpdateProject`.

The client's composition registers the brand, the routes and texts of its assembly, the menu,
the generated HTTP senders, and the same permissions and policies as the host:

```csharp
public static void AddTasksWasm(this IServiceCollection services)
{
    services.AddSingleton<IBrandProvider>(new StaticBrandProvider(new Brand { Name = "Acme Tasks" }));
    services.AddRoutesFromAssembly();
    services.AddStringsFromAssembly();
    services.AddNavMenu<Menu>();
    services.AddTasksHttpClients();
    services.AddTasksPermissions();
    services.AddSecurity();
    services.AddMessagePolicies();
    services.AddTasksPolicies();
}
```

The host calls the server half, `AddTasksWebApp` (the brand, `AddComponentServices`, the routes
and texts), so it can render the same screens. An app without sign-in also sets the default page policy to *anyone* and an anonymous
`AuthenticationStateProvider` — the Sample's `Screens.cs` is the shape to copy.

## 8. Run it

```bash
dotnet run --project src/Acme.Tasks.Web --launch-profile http
```

The first run creates the database. From here, every new operation is the same three steps: a
message in the Sdk, a handler on the host, a screen that sends it.

## What next

- The [Sample](../src/Stack/Sample/docs/sample.md) adds a React client over the same messages.
- The [doctrine](../plugins/nsail-stack/doctrine) holds every rule; the Claude Code plugin hands it
  to an agent working on your app (see the repository [README](../README.md)).
