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
    .AddEntityFrameworkStores<ApplicationDbContext>();


var r2 = builder.Configuration.GetSection("CloudflareR2");

var serviceUrl = r2["ServiceUrl"]
    ?? throw new InvalidOperationException("R2 endpoint is missing.");

var accessKey = r2["AccessKeyId"]
    ?? throw new InvalidOperationException("R2 access key is missing.");

var secretKey = r2["SecretAccessKey"]
    ?? throw new InvalidOperationException("R2 secret key is missing.");

builder.Services.AddSingleton<IAmazonS3>(_ =>
    new AmazonS3Client(
        accessKey,
        secretKey,
        new AmazonS3Config
        {
            ServiceURL = serviceUrl,
            ForcePathStyle = true
        }));

builder.Services.AddScoped<CloudflareR2Service>(sp =>
    new CloudflareR2Service(
        sp.GetRequiredService<IAmazonS3>(),
        sp.GetRequiredService<IConfiguration>()));

builder.Services.AddScoped<CompatibilityService>();

var app = builder.Build();


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