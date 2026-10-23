using Portfolio.Api.Configuration;
using Portfolio.Core.Accounts;
using Portfolio.Core.Management;
using Portfolio.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Portfolio.Api.Filters;
using Portfolio.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AntiforgeryProblemFilter());
    options.Filters.Add(new BusinessExceptionFilter());
});
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<PortfolioDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("PortfolioDatabase")));

SecurityConfiguration.AddSecurityServices(builder.Services, builder.Environment);
builder.Services.AddScoped<DatabaseInitializer>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IPortfolioService, PortfolioService>();
builder.Services.AddScoped<IHistoryReport, HistoryReport>();

var app = builder.Build();

if (args.Contains("--initialize-database") || args.Contains("--create-admin"))
{
    using var scope = app.Services.CreateScope();
    DatabaseInitializer initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.Initialize();

    if (args.Contains("--create-admin"))
    {
        string email = builder.Configuration["PortfolioBootstrap:Email"]
            ?? throw new InvalidOperationException("Set PortfolioBootstrap__Email before creating an administrator.");
        string password = builder.Configuration["PortfolioBootstrap:Password"]
            ?? throw new InvalidOperationException("Set PortfolioBootstrap__Password before creating an administrator.");
        await initializer.CreateAdmin(email, password);
    }

    return;
}

app.UseExceptionHandler();
app.UseStatusCodePages();
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.UseRequestInterceptor("""
            (request) => {
                const tokenCookie = document.cookie.split('; ').find(value => value.startsWith('XSRF-TOKEN='));
                if (tokenCookie && !['GET', 'HEAD', 'OPTIONS'].includes(request.method.toUpperCase())) {
                    request.headers['X-XSRF-TOKEN'] = decodeURIComponent(tokenCookie.substring('XSRF-TOKEN='.length));
                }
                return request;
            }
            """);
    });
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program
{
}

