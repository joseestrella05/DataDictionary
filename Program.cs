using System.IO;
using DataDictionaryWeb.Components;
using DataDictionaryWeb.Services;

var builder = WebApplication.CreateBuilder(args);

// Servicios Razor / Blazor Server
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddServerSideBlazor().AddHubOptions(options =>
{
    options.MaximumReceiveMessageSize = 10 * 1024 * 1024; // 10MB para subida de logotipos e intercambio interactivo
});

// Inyección de Dependencias
builder.Services.AddScoped<DatabaseDiscoveryService>();
builder.Services.AddScoped<MetadataExtractionService>();
builder.Services.AddScoped<WordDictionaryService>();
builder.Services.AddScoped<IntelligentMetadataService>();
builder.Services.AddScoped<SchemaDriftService>();
builder.Services.AddSingleton<DocumentDownloadManager>();

var app = builder.Build();

// Configuración del pipeline HTTP
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();

// Endpoint de descarga de alta velocidad y bajo consumo de memoria para documentos masivos
app.MapGet("/api/download/{token}", (string token, DocumentDownloadManager downloadManager) =>
{
    if (downloadManager.TryGetFile(token, out var filePath, out var fileName) && filePath != null)
    {
        var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.DeleteOnClose);
        return Results.File(fileStream, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
    }
    return Results.NotFound("El archivo solicitado ha expirado o ya fue descargado.");
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
