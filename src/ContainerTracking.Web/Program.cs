using Blazored.LocalStorage;
using Blazored.Modal;
using Blazored.Toast;
using ContainerTracking.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBase = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5001";

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBase) });

builder.Services.AddBlazoredLocalStorage();
builder.Services.AddBlazoredModal();
builder.Services.AddBlazoredToast();

builder.Services.AddScoped<ApiClient>(sp =>
    new ApiClient(sp.GetRequiredService<HttpClient>(),
        sp.GetRequiredService<ILocalStorageService>()));

builder.Services.AddScoped<TrackingHubService>(sp =>
    new TrackingHubService(apiBase, sp.GetRequiredService<ILocalStorageService>()));

await builder.Build().RunAsync();
