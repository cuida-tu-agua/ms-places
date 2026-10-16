using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using SyWater.Places.Api.Composition;
using SyWater.Places.Api.Errors;
using SyWater.Places.Api.Security;
using SyWater.Places.Infrastructure.Persistence;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(
        new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper))); // CubicMeters <-> "CUBIC_METERS"

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, context, cancellationToken) =>
{
    var serverUrl = builder.Configuration["OpenApi:ServerUrl"];
    if (!string.IsNullOrWhiteSpace(serverUrl))
        document.Servers = [new OpenApiServer { Url = serverUrl }];
    return Task.CompletedTask;
}));

builder.Services.AddPlacesApplication();
builder.Services.AddPlacesInfrastructure(builder.Configuration);

builder.Services.AddIamJwtAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    // HU-063/064: the tips are managed by administrators. ms-iam puts the roles of the user in the claim "roles"
    .AddPolicy(AuthorizationPolicies.Admin, policy => policy.RequireClaim("roles", AuthorizationPolicies.AdminRole));

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddHealthChecks().AddDbContextCheck<PlacesDbContext>("database");

var app = builder.Build();

app.UseExceptionHandler();

// Development: always. Other environments: only when OpenApi:Enabled=true (set by infra).
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("OpenApi:Enabled"))
{
    app.MapOpenApi().AllowAnonymous(); // GET /openapi/v1.json
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();