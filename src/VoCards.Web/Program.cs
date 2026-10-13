using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using VoCards.Web;
using VoCards.Web.Services;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// The app is entirely client-side: one library, one store, all singletons.
builder.Services.AddScoped<JsBridge>();
builder.Services.AddScoped<LibraryStore>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<KeyboardService>();
builder.Services.AddScoped<AppState>();

await builder.Build().RunAsync();
