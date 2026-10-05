using System.Text;
using HealthApp.Api.Middleware;
using HealthApp.Infrastructure;
using HealthApp.Infrastructure.Authentication;
using HealthApp.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
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
builder.Services.AddInfrastructure(builder.Configuration);

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    { ValidateIssuer=true, ValidIssuer=jwt.Issuer, ValidateAudience=true, ValidAudience=jwt.Audience, ValidateIssuerSigningKey=true, IssuerSigningKey=key, ValidateLifetime=true, ClockSkew=TimeSpan.FromSeconds(30) };
});
builder.Services.AddAuthorization();
builder.Services.AddCors(options => options.AddPolicy("WebApps", policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
await DatabaseInitializer.InitializeAsync(app.Services);
app.UseMiddleware<ExceptionMiddleware>();
app.UseStaticFiles();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseCors("WebApps"); app.UseAuthentication(); app.UseMiddleware<OutletActivationMiddleware>(); app.UseAuthorization(); app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status="ok", service="HealthApp.Api", framework=".NET 10", database="SQL Server / EF Core 10.0.12", time=DateTime.UtcNow }));
app.Run();
