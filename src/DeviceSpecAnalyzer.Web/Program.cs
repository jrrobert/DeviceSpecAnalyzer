using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.UI.Services;
using DeviceSpecAnalyzer.Core.Interfaces;
using DeviceSpecAnalyzer.Data;
using DeviceSpecAnalyzer.Data.Repositories;
using DeviceSpecAnalyzer.Processing.Services;
using DeviceSpecAnalyzer.Processing.Parsers;
using DeviceSpecAnalyzer.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
    throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddDefaultIdentity<IdentityUser>(options => 
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Register application services
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<IPdfTextExtractor, PdfTextExtractor>();
builder.Services.AddScoped<ITfIdfVectorizer, TfIdfVectorizer>();
builder.Services.AddScoped<ISimilarityCalculator, SimilarityCalculator>();
builder.Services.AddScoped<IDocumentProcessor, DocumentProcessor>();

// Register protocol parsers
builder.Services.AddScoped<IProtocolParser, Poct1AParser>();
builder.Services.AddScoped<IProtocolParser, AstmParser>();
builder.Services.AddScoped<IProtocolParser, Hl7Parser>();

// Register repository watcher as hosted service
builder.Services.AddHostedService<RepositoryWatcherHostedService>();
builder.Services.Configure<RepositoryWatcherOptions>(
    builder.Configuration.GetSection("RepositoryWatcher"));

// Add logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});

// Add HTTP client for file uploads
builder.Services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
}

app.Run();