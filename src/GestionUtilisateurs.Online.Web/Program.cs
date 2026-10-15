using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using GestionUtilisateurs.Online.Web.Components.Online;
using GestionUtilisateurs.Online.Web.Services;
using GestionUtilisateurs.Shared.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<Root>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Configuration Supabase (adresse du projet + clé publique « anon », sans secret).
var supabase = new SupabaseOptions();
builder.Configuration.GetSection("Supabase").Bind(supabase);
builder.Services.AddSingleton(supabase);

// Données partagées entre les membres via Supabase (cache local + envoi différé).
builder.Services.AddScoped<OnlineApi>();
builder.Services.AddScoped<OnlineStorageProvider>();
builder.Services.AddScoped<IStorageProvider>(sp => sp.GetRequiredService<OnlineStorageProvider>());
builder.Services.AddScoped(sp => new DataStore(sp.GetRequiredService<IStorageProvider>()) { OnlineMode = true });
builder.Services.AddScoped<AppState>();
builder.Services.AddScoped<OnlineSession>();
builder.Services.AddScoped<CapacityService>();
builder.Services.AddScoped<AccountService>();

// Services navigateur (mêmes API que les services natifs Android).
builder.Services.AddScoped<JavascriptBridge>();
builder.Services.AddScoped<WebFiles>();
builder.Services.AddScoped<PhotoService>();
builder.Services.AddScoped<NativeFileSaver>();
builder.Services.AddScoped<SpreadsheetPicker>();
builder.Services.AddScoped<DocumentService>();
builder.Services.AddScoped<AutoExportService>();

await builder.Build().RunAsync();
