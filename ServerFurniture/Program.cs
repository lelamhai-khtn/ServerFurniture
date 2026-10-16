using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using ServerFurniture.Data;
using ServerFurniture.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Configure Entity Framework / SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure JWT authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT key not configured");
var keyBytes = Encoding.UTF8.GetBytes(jwtKey);
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(keyBytes)
    };
});

builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddHttpClient();

// Configure CORS for Google Sign-In
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "My API");
    });
}

app.UseHttpsRedirection();

// Enable serving static files from wwwroot
app.UseStaticFiles();
app.UseDefaultFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Apply any pending EF Core migrations at startup (convenient for development).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
        // Ensure RefreshTokens table exists; create it if missing as a safety net.
        var ensureRefreshTokensSql = @"
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[RefreshTokens]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[RefreshTokens](
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Token] NVARCHAR(450) NOT NULL,
        [ExpiresAt] DATETIME2 NOT NULL,
        [Revoked] BIT NOT NULL,
        [UserId] INT NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT (GETUTCDATE()),
        [ReplacedByToken] NVARCHAR(MAX) NULL
    );
END
";

        try
        {
            db.Database.ExecuteSqlRaw(ensureRefreshTokensSql);
        }
        catch
        {
            // swallow: if this fails, migrations should provide schema and exception will surface normally
        }
}

app.Run();
