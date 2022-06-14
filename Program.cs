using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using NLog;
using NLog.Web;
using decovar.dev.Code;
using decovar.dev.Middleware;

//var logger = NLog.Web.NLogBuilder.ConfigureNLog("nlog.config").GetCurrentClassLogger();
var logger = NLog.LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
logger.Info("Starting the application...");

var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.Configure<ConfigurationAD>
    (
        c =>
        {
            c.Port = configuration.GetSection("AD:port").Get<int>();

            c.Zone = configuration.GetSection("AD:zone").Value;
            c.Domain = configuration.GetSection("AD:domain").Value;
            c.Subdomain = configuration.GetSection("AD:subdomain").Value;

            c.Username = configuration.GetSection("AD:username").Value;
            c.Password = configuration.GetSection("AD:password").Value;

            // connection string with port doesn't work on GNU/Linux and Mac OS
            //c.LDAPserver = $"{c.Subdomain}.{c.Domain}.{c.Zone}:{c.Port}";
            c.LDAPserver = $"{c.Subdomain}.{c.Domain}.{c.Zone}";
            // that depends on how it is in your LDAP server
            //c.LDAPQueryBase = $"DC={c.Subdomain},DC={c.Domain},DC={c.Zone}";
            c.LDAPQueryBase = $"DC={c.Domain},DC={c.Zone}";

            c.Crew = new StringBuilder()
                .Append($"CN={configuration.GetSection("AD:crew").Value},")
                // check which CN (Users or Groups) your LDAP server has the groups in
                .Append($"CN=Users,{c.LDAPQueryBase}")
                .ToString();
            c.Managers = new StringBuilder()
                .Append($"CN={configuration.GetSection("AD:managers").Value},")
                // check which CN (Users or Groups) your LDAP server has the groups in
                .Append($"CN=Users,{c.LDAPQueryBase}")
                .ToString();
        }
    );

    builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
    builder.Services.AddScoped<ISignInManager, SignInManager>();

    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(
            options =>
            {
                options.Cookie.HttpOnly = true;
                // lowering this value improves security (for example, in case
                // of employees leaving the company), but increases the annoyance
                // for users, as they'll need to sign-in more often
                options.ExpireTimeSpan = TimeSpan.FromDays(11);
                // for the same reasons you migth want setting this one to false
                options.SlidingExpiration = true;

                options.LoginPath = "/account/login";
                options.AccessDeniedPath = "/account/access-denied";
            }
        );

    builder.Services.AddAuthorization(options =>
    {
        options.DefaultPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
    });

    builder.Services.AddControllersWithViews(
        options =>
        {
            options.Filters.Add(new AuthorizeFilter());
            options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
        }
    ).AddRazorRuntimeCompilation();

    // NLog
    builder.Logging.ClearProviders();
    builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
    //builder.Logging.AddConsole();
    builder.Host.UseNLog();

    var app = builder.Build();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/error");
        // to log uncaught exceptions
        app.UseCustomExceptionsHandler();

        // not when we are behind NGINX
        //app.UseHsts();
        //app.UseHttpsRedirection();
    }

    app.UseStatusCodePagesWithReExecute("/error/{0}");

    app.UseStaticFiles();

    app.UseRouting();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}"
    );

    // for openning browser via launch.json, as the Microsoft's logs are disabled
    logger.Debug("Now listening on: http://localhost:5000");

    app.Run();
}
catch (Exception ex)
{
    logger.Error(ex, "Stopped the program because of exception");
    throw;
}
finally
{
    // ensure to flush and stop internal timers/threads before application-exit
    // to avoid segmentation fault on Linux
    NLog.LogManager.Shutdown();
}

public class ConfigurationAD
{
    public int Port { get; set; } = 389; // for encrypted connection use 636
    public string Zone { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Subdomain { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public string LDAPserver { get; set; } = string.Empty;
    public string LDAPQueryBase { get; set; } = string.Empty;

    public string Crew { get; set; } = string.Empty;
    public string Managers { get; set; } = string.Empty;
}
