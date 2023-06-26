using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Syncfusion.Blazor;
using Cloud5mins.ShortenerTools.TinyBlazorAdmin;
using AzureStaticWebApps.Blazor.Authentication;
using Microsoft.Extensions.Configuration;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
var baseAddress = builder.HostEnvironment.BaseAddress;

string azFuncAccessKey = builder.Configuration["azFuncAccessKey"];
string azFuncAccessKey2= "yeah right";
var  azFuncAccessKey3 = builder.Configuration.GetValue<string>("azFuncAccessKey");
try
{
    azFuncAccessKey2 = builder.Configuration.GetSection("appsettings")["azFuncAccessKey2"];
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}

Console.WriteLine($"key (at init): {azFuncAccessKey}");
Console.WriteLine($"key2 (at init): {azFuncAccessKey2}");
Console.WriteLine($"key3 (at init): {azFuncAccessKey3}");

HttpClient httpClient = new HttpClient { BaseAddress = new Uri(baseAddress) };
httpClient.DefaultRequestHeaders.Add("x-functions-key", azFuncAccessKey);

builder.Services.AddScoped(sp => httpClient)
                                .AddStaticWebAppsAuthentication();

// builder.Services.AddMsalAuthentication(options =>
// {
//     builder.Configuration.Bind("AzureAd", options.ProviderOptions.Authentication);
// });

// regiser fusion blazor service
// Community Licence for your personal use ONLY. Thank you Syncfusion for this generous offer.
Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("NzYyMzI1QDMyMzAyZTMxMmUzMFY0cEZ3MVozdkwvekVhek8xTWdPMkg2NlhvdVFNR1lvZHdhQWJWUlNjZW89"); 
builder.Services.AddSyncfusionBlazor();

await builder.Build().RunAsync();


public class AppConfig{
    public string azFuncAccessKey2 { get; set; }
}