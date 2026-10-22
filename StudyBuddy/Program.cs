using StudyBuddy.Services;
using Amazon.S3;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();


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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
