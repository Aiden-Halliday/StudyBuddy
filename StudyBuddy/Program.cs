using StudyBuddy.Services;
using StudyBuddy.Data;
using StudyBuddy.Models;
using Amazon.S3;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services
    .AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.Zero);

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Events.OnValidatePrincipal =
        AdminIdentitySetup.ValidatePrincipalAsync;
});

builder.Services.AddScoped<AdminUserService>();

// Upload configuration is resolved when storage is actually used.
builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var r2 = sp.GetRequiredService<IConfiguration>()
        .GetSection("CloudflareR2");

    return new AmazonS3Client(
        r2["AccessKeyId"]
            ?? throw new InvalidOperationException("R2 access key is missing."),
        r2["SecretAccessKey"]
            ?? throw new InvalidOperationException("R2 secret key is missing."),
        new AmazonS3Config
        {
            ServiceURL = r2["ServiceUrl"]
                ?? throw new InvalidOperationException("R2 endpoint is missing."),
            ForcePathStyle = true
        });
});

builder.Services.AddScoped<CloudflareR2Service>(sp =>
    new CloudflareR2Service(
        sp.GetRequiredService<IAmazonS3>(),
        sp.GetRequiredService<IConfiguration>()));

builder.Services.AddScoped<CompatibilityService>();

var app = builder.Build();

// Integration tests initialize their isolated database separately.
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();

    await AdminIdentitySetup.InitializeAsync(
        scope.ServiceProvider,
        app.Configuration,
        app.Environment);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// Allows WebApplicationFactory<Program> in integration tests.
public partial class Program { }