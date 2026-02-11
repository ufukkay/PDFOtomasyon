using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PDFAutomation;

IHost host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options =>
    {
        options.ServiceName = "PDFOtomasyonServisi";
    })
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();
