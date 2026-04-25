using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Data.Common;
using System.Reflection;
using System.Text;
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();
try
{
    var builder = WebApplication.CreateBuilder(args);
    var allowedOrigins = builder.Configuration.GetSection("AllowedCorsOrigins").Get<string[]>();
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowReactApp", policy =>
        {
            policy.WithOrigins(allowedOrigins) // Replace with your React app's URL
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // For cookies or authentication

        });
    });

    builder.Services.AddControllers().AddNewtonsoftJson(); // registers the controller

    builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

    builder.Services.AddHttpContextAccessor(); // Required for accessing HttpContext
    builder.Services.AddDistributedMemoryCache(); // Required for session
    builder.Services.AddMemoryCache();

    builder.Services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromMinutes(30); // Set session timeout
        options.Cookie.HttpOnly = true; // Make session cookie HTTP only
        options.Cookie.IsEssential = true; // Make the session cookie essential
    });

    builder.Services.AddEndpointsApiExplorer();
    //builder.Services.AddSwaggerGen();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "Your API", Version = "v1" });

 //       c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
 //       {
 //           Name = "Authorization",
 //           Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
 //           Scheme = "Bearer",
 //           BearerFormat = "JWT",
 //           In = Microsoft.OpenApi.Models.ParameterLocation.Header,
 //           Description = "Enter 'Bearer' followed by space and your JWT token.\n\nExample: Bearer eyJhbGciOiJIUzI1NiIs..."
 //       });

 //       c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
 //{
 //{
 //new Microsoft.OpenApi.Models.OpenApiSecurityScheme
 //{
 //Reference = new Microsoft.OpenApi.Models.OpenApiReference
 //{
 //Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
 //Id = "Bearer"
 //}
 //},
 //Array.Empty<string>()
 //}
 //});
    });
    //builder.Services.AddSingleton<IActivityLogger>(provider => new FileActivityLogger(Path.Combine(AppContext.BaseDirectory, "Logs")));
    //builder.Services.AddScoped<UsageLogDB>();

    var jwtSettings = builder.Configuration.GetSection("Jwt");
    string authority = jwtSettings["Authority"];
    string audience = jwtSettings["Audience"];
    builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = authority;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
     Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };
    });

    var connectionString = builder.Configuration.GetConnectionString("AGMDB");
    //DbConnection.SetConnectionString(connectionString);
    builder.Host.UseSerilog();

    var app = builder.Build();
    app.UseCors("AllowReactApp");
    app.Use(async (context, next) =>
    {
        if (context.Request.Method == HttpMethods.Options)
        {
            context.Response.Headers.Add("Access-Control-Allow-Origin", allowedOrigins);
            context.Response.Headers.Add("Access-Control-Allow-Methods", "POST, GET, OPTIONS");
            context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");
            context.Response.Headers.Add("Access-Control-Allow-Credentials", "true");
            context.Response.StatusCode = 204; // No Content
            return;
        }
        await next();
    });

    // Following is for VAPT check
    app.Use(async (context, next) =>
    {
        context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
        context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
        context.Response.Headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
        context.Response.Headers.TryAdd("Permissions-Policy", "geolocation=(), camera=(), microphone=()");
        context.Response.Headers.TryAdd(
        "Content-Security-Policy",
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline';");
        await next();
    });

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseRouting();
    app.UseSession();
    app.UseAuthorization();
    app.UseHttpsRedirection();
    app.MapControllers();
    //app.UseMiddleware<ActivityLoggingMiddleware>();//added Middleware use Activity Logger
    //app.UseMiddleware<UsageLogMiddleware>(); //added Middleware use Usage Log
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application startup failed");
}