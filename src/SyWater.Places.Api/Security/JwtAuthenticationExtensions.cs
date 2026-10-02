using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

namespace SyWater.Places.Api.Security;

public static class JwtAuthenticationExtensions
{
    public static IServiceCollection AddIamJwtAuthentication(
        this IServiceCollection services, IConfiguration config, IHostEnvironment env)
    {
        var section = config.GetSection("Jwt");
        var issuer = section["Issuer"] ?? throw new InvalidOperationException("Missing Jwt:Issuer.");
        var keyPath = section["PublicKeyPath"] ?? throw new InvalidOperationException("Missing Jwt:PublicKeyPath.");

        var fullPath = Path.IsPathRooted(keyPath) ? keyPath : Path.Combine(AppContext.BaseDirectory, keyPath);
        var rsa = RSA.Create(); // not disposed on purpose: the key lives as long as the app
        rsa.ImportFromPem(File.ReadAllText(fullPath));

        var redisConfiguration = config["Redis:Configuration"];
        if (string.IsNullOrWhiteSpace(redisConfiguration))
            throw new InvalidOperationException("Missing Redis:Configuration (the same Redis that ms-iam uses).");

        var redisOptions = ConfigurationOptions.Parse(redisConfiguration);
        redisOptions.AbortOnConnectFail = false;
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisOptions));
        services.AddSingleton<ITokenRevocationChecker, RedisTokenRevocationChecker>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = !env.IsDevelopment();
                options.MapInboundClaims = false; // keep "sub" as "sub" (no SOAP-style claim names)
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = false, 
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new RsaSecurityKey(rsa) { KeyId = section["KeyId"] },
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    NameClaimType = "sub",
                };
                options.Events = new JwtBearerEvents { OnTokenValidated = RejectRevokedTokens };
            });

        return services;
    }

    private static async Task RejectRevokedTokens(TokenValidatedContext context)
    {
        var principal = context.Principal;
        var tokenId = principal?.FindFirstValue("jti");
        var userId = principal?.FindFirstValue("sub");
        long? issuedAt = long.TryParse(principal?.FindFirstValue("iat"), out var iat) ? iat : null;

        var checker = context.HttpContext.RequestServices.GetRequiredService<ITokenRevocationChecker>();
        try
        {
            if (await checker.IsRevokedAsync(tokenId, userId, issuedAt))
                context.Fail("The token was revoked.");
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("SyWater.Places.Api.Security.TokenRevocation")
                .LogError(ex, "Redis is not available: the token cannot be checked, the request is rejected.");
            context.Fail("The token could not be checked.");
        }
    }
}
