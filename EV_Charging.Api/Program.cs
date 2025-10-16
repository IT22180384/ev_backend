/*
 * Program.cs
 * Main entry point for the EV Charging API application.
 * Configures services, middleware, and application pipeline.
 */
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using EV_Charging.Api.Services;
using EV_Charging.Api.Data;
using EV_Charging.Api.Utils;
using DotNetEnv;
using System.Text;
using MongoDB.Driver;

// Load .env file - THIS MUST BE AT THE VERY TOP
DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowClients", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("CORS:AllowedOrigins")
            .Get<string[]>() ?? new[] { "http://localhost:3000" };
        
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "EV Charging API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

// JWT Configuration from .env
var jwtSecretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") 
                   ?? throw new InvalidOperationException("JWT_SECRET_KEY not found in environment variables");
var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") 
                ?? "EVCharging-API";
var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") 
                  ?? "EVCharging-Client";
var jwtExpireHours = int.Parse(Environment.GetEnvironmentVariable("JWT_EXPIRE_HOURS") 
                  ?? "24");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
            ClockSkew = TimeSpan.Zero
        };
        
        // Optional: Handle authentication failures
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"Authentication failed: {context.Exception.Message}");
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Console.WriteLine("Token validated successfully");
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// Register MongoDB context
builder.Services.AddSingleton<MongoDbContext>();

// Register IMongoDatabase from MongoDbContext
builder.Services.AddSingleton<IMongoDatabase>(serviceProvider =>
{
    var context = serviceProvider.GetRequiredService<MongoDbContext>();
    return context.GetDatabase();
});

// Register custom services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IEVOwnerService, EVOwnerService>();
builder.Services.AddScoped<IStationService, StationService>();
builder.Services.AddScoped<IOperatorService, OperatorService>();
builder.Services.AddScoped<IReservationService, ReservationService>();
builder.Services.AddScoped<IQrCodeGenerator, QrCodeGenerator>();
builder.Services.AddScoped<IJwtService, JwtService>();

// Add logging
builder.Services.AddLogging(loggingBuilder => {
    loggingBuilder.AddConsole();
    loggingBuilder.AddDebug();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowClients");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/", () => "Welcome to EV Charging Backend API");

// Test endpoint to verify environment variables
app.MapGet("/env-test", () => 
{
    var envVars = new
    {
        JwtSecretExists = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("JWT_SECRET_KEY")),
        JwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER"),
        JwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE"),
        MongoDbConnection = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING"))
    };
    return Results.Ok(envVars);
});

app.Run();