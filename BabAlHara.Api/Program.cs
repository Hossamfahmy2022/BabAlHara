using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BabAlHara.Api.Data;
using BabAlHara.Api.Hubs;
using BabAlHara.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var rawConnectionString = builder.Configuration.GetConnectionString("Default") ?? "Data Source=babalhara.db";
var isSqlServer = rawConnectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase)
    || rawConnectionString.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase)
    || rawConnectionString.Contains("User Id=", StringComparison.OrdinalIgnoreCase);

if (isSqlServer)
{
    builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(rawConnectionString));
}
else
{
    var sqliteBuilder = new SqliteConnectionStringBuilder(rawConnectionString);
    if (!string.IsNullOrWhiteSpace(sqliteBuilder.DataSource)
        && sqliteBuilder.DataSource != ":memory:"
        && !Path.IsPathRooted(sqliteBuilder.DataSource))
    {
        sqliteBuilder.DataSource = Path.Combine(builder.Environment.ContentRootPath, sqliteBuilder.DataSource);
    }
    var dbDir = Path.GetDirectoryName(sqliteBuilder.DataSource);
    if (!string.IsNullOrWhiteSpace(dbDir))
    {
        Directory.CreateDirectory(dbDir);
    }
    var connectionString = sqliteBuilder.ToString();
    builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
}

builder.Services.AddSingleton<CairoClockService>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<IOrderNotifier, SignalROrderNotifier>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<DashboardService>();

builder.Services.AddProblemDetails();

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services
    .AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddOpenApi();

var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
    ?? ["http://localhost:4200", "http://localhost:4300"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendCors", policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "BabAlHara.Api";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "BabAlHara.Admin";
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is required.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/orders"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (DomainValidationException ex)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        var problem = new ValidationProblemDetails(ex.Errors)
        {
            Title = "بيانات غير صالحة",
            Detail = ex.Message,
            Status = StatusCodes.Status400BadRequest,
            Instance = context.Request.Path
        };
        await context.Response.WriteAsJsonAsync(problem);
    }
    catch (DomainConflictException ex)
    {
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        var problem = new ProblemDetails
        {
            Title = "تعذر تنفيذ العملية",
            Detail = ex.Message,
            Status = StatusCodes.Status409Conflict,
            Instance = context.Request.Path
        };
        await context.Response.WriteAsJsonAsync(problem);
    }
    catch (KeyNotFoundException ex)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        var problem = new ProblemDetails
        {
            Title = "غير موجود",
            Detail = ex.Message,
            Status = StatusCodes.Status404NotFound,
            Instance = context.Request.Path
        };
        await context.Response.WriteAsJsonAsync(problem);
    }
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbSeeder.SeedAsync(db, app.Configuration, app.Environment.ContentRootPath);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "BabAlHara API v1");
    });
}

app.UseStaticFiles();
app.UseCors("FrontendCors");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<OrdersHub>("/hubs/orders");

app.Run();
