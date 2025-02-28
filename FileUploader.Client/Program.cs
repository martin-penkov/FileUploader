using FileUploader.Client.Services.AlertService;
using FileUploader.Client.Services.FileUploadService;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace FileUploader.Client
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");

            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
            builder.Services.AddSingleton<IAlertService, AlertService>();
            builder.Services.AddScoped<IFileUploadService, FileUploadService>();

            await builder.Build().RunAsync();
        }
    }
}
