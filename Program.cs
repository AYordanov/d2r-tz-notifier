using TerrorZoneNotifier;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration(cfg => cfg.AddEnvironmentVariables())
    .ConfigureServices(services =>
    {
        // Typed HTTP client: D2EmuClient gets an HttpClient injected.
        services.AddHttpClient<D2EmuClient>(c =>
        {
            c.Timeout = TimeSpan.FromSeconds(30);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("TerrorZoneNotifier/1.0");
        });

        services.AddSingleton<EmailSender>();
    })
    .Build();

host.Run();
