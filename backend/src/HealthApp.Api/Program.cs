using HealthApp.Application.Services;\nusing System.Text;
using HealthApp.Api.Middleware;
using HealthApp.Application.Abstractions;
using HealthApp.Infrastructure;
using HealthApp.Infrastructure.Authentication;
using HealthApp.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var details = string.Join(
            " | ",
            context.ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .SelectMany(x => x.Value!.Errors.Select(error =>
                    string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? error.Exception?.Message ?? "Invalid value."
                        : error.ErrorMessage))
                .Take(20));

        context.HttpContext.Items["HealthApp.ModelValidationErrors"] =
            details.Length > 3800 ? details[..3800] : details;

        return new BadRequestObjectResult(new ValidationProblemDetails(context.ModelState));
    };
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "HealthApp API", Version = "v1", Description = "HealthApp Meal Subscription API" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: Bearer {token}",
        Name = "Authorization", In = ParameterLocation.Header, Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT"
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddInfrastructure(builder.Configuration);\nbuilder.Services.AddScoped<ITrialService, TrialLifecycleService>();

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    { ValidateIssuer=true, ValidIssuer=jwt.Issuer, ValidateAudience=true, ValidAudience=jwt.Audience, ValidateIssuerSigningKey=true, IssuerSigningKey=key, ValidateLifetime=true, ClockSkew=TimeSpan.FromSeconds(30) };
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
});
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
var tenantDomainSettings = builder.Configuration.GetSection("TenantDomains").Get<TenantDomainSettings>() ?? new TenantDomainSettings();
var platformBaseDomain = tenantDomainSettings.PlatformBaseDomain.Trim().TrimEnd('.').ToLowerInvariant();

builder.Services.AddCors(options => options.AddPolicy("WebApps", policy =>
{
    // Local development can use different Vite ports/hostnames. Keep CORS
    // intentionally permissive only in Development. Production continues to use
    // the explicit tenant-domain allow-list below.
    if (builder.Environment.IsDevelopment())
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        return;
    }

    policy
        .SetIsOriginAllowed(origin =>
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                return false;

            if (allowedOrigins.Any(x => string.Equals(x, origin, StringComparison.OrdinalIgnoreCase)))
                return true;

            var originHost = uri.Host.TrimEnd('.').ToLowerInvariant();
            return !string.IsNullOrWhiteSpace(platformBaseDomain) &&
                   originHost.EndsWith("." + platformBaseDomain);
        })
        .AllowAnyHeader()
        .AllowAnyMethod();
}));

var app = builder.Build();
await DatabaseInitializer.InitializeAsync(app.Services);
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "geolocation=(self), camera=(), microphone=()";
    await next();
});
app.UseStaticFiles();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseRouting();

// CORS must wrap exception handling so 4xx/5xx API responses still include
// Access-Control-Allow-Origin. Otherwise browser clients can report a real
// server error as a misleading CORS error.
app.UseCors("WebApps");
app.UseMiddleware<ExceptionMiddleware>();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<TenantContextMiddleware>();
app.UseMiddleware<OutletActivationMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status="ok", service="HealthApp.Api", framework=".NET 10", database="SQL Server / EF Core 10.0.12", time=DateTime.UtcNow }));
app.Run();
