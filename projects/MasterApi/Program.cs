using MasterApi.Configuration;
using MasterApi.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MasterServerOptions>(
    builder.Configuration.GetSection(MasterServerOptions.SectionName));

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        if (allowedOrigins.Length == 0)
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            return;
        }

        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddDbContext<MasterDbContext>(options =>
{
    if (builder.Environment.IsEnvironment("Testing"))
    {
        options.UseInMemoryDatabase("master-api-tests");
        return;
    }

    options.UseSqlite(builder.Configuration.GetConnectionString("MasterCatalog")
        ?? throw new InvalidOperationException("Connection string 'MasterCatalog' is missing."));
});

builder.Services.AddScoped<MasterDbInitializer>();

builder.Services
    .AddGraphQLServer()
    .AddQueryType<MasterApi.Types.Query>()
    .AddMutationType<MasterApi.Types.Mutation>();

var app = builder.Build();

app.UseCors("frontend");

app.MapGet("/", () => Results.Ok(new
{
    name = "Capitalism Master API",
    graphql = "/graphql",
    health = "/healthz"
}));

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
app.MapGraphQL();

using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<MasterDbInitializer>();
    await initializer.InitializeAsync();
}

app.Run();

public partial class Program;