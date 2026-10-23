using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Portfolio.Core.Authentication;
using Portfolio.Infrastructure.Identity;
using Portfolio.Infrastructure.Data;

namespace Portfolio.Api.Configuration
{
    public static class SecurityConfiguration
    {
        public static void AddSecurityServices(IServiceCollection services, IWebHostEnvironment environment)
        {
            var dataProtection = services.AddDataProtection()
                .SetApplicationName("FinancialPortfolioProject2")
                .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(environment.ContentRootPath, "App_Data", "Keys")));

            if (OperatingSystem.IsWindows())
            {
                dataProtection.ProtectKeysWithDpapi();
            }

            services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            }).AddEntityFrameworkStores<PortfolioDbContext>().AddDefaultTokenProviders();

            services.Configure<SecurityStampValidatorOptions>(options =>
                options.ValidationInterval = TimeSpan.Zero);

            services.ConfigureApplicationCookie(options =>
            {
                options.Cookie.Name = environment.IsDevelopment() ? "Portfolio.Session" : "__Host-Portfolio.Session";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
                options.SlidingExpiration = false;
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
                options.Events.OnValidatePrincipal = async context =>
                {
                    await SecurityStampValidator.ValidatePrincipalAsync(context);

                    if (context.Principal != null)
                    {
                        UserManager<ApplicationUser> userManager = context.HttpContext.RequestServices
                            .GetRequiredService<UserManager<ApplicationUser>>();
                        ApplicationUser? user = await userManager.GetUserAsync(context.Principal);

                        if (user == null || !user.IsActive)
                        {
                            context.RejectPrincipal();
                            await context.HttpContext.RequestServices.GetRequiredService<SignInManager<ApplicationUser>>()
                                .SignOutAsync();
                        }
                    }
                };
            });

            services.AddAntiforgery(options =>
            {
                options.HeaderName = "X-XSRF-TOKEN";
                options.Cookie.Name = environment.IsDevelopment() ? "Portfolio.Antiforgery" : "__Host-Portfolio.Antiforgery";
                options.Cookie.SecurePolicy = environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
            });
            services.AddAuthorization();
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddPolicy("authentication", context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions()
                        {
                            PermitLimit = 20,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0
                        }));
            });
            services.AddScoped<IAuthenticationService, AuthenticationService>();

        }
    }
}
