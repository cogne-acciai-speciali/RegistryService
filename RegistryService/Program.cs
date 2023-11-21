using MesLibrary.HttpConnector.MesWebApi;
using MesLibrary.HttpConnector.SAP;
using MesLibrary.HttpConnector.SapWebApi;
using Microsoft.Extensions.Http;
using RegistryService;
using Serilog;
using Serilog.Events;

IHost host = Host.CreateDefaultBuilder(args)
        .UseSerilog((context, _, config) => config
        .MinimumLevel.Debug()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Error)
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.File("test")
        )

    .ConfigureLogging((ctx, builder) =>
    {
        builder.ClearProviders();
        builder.AddSerilog();
    })
    .ConfigureServices(services =>
    {
        //services.AddHostedService<Worker>();
        services.AddHostedService<RegistryServices>();
        //services.AddHostedService<WorkCenterService>();
        services.AddHttpClient<RegistryServiceConnector>(client =>
        {
            client.BaseAddress = new Uri("https://localhost:7127");
        });
        services.AddHttpClient<RegistryServiceSapConnector>(client =>
        {
            client.BaseAddress = new Uri("https://localhost:7150");
        });
        services.AddSingleton<RegistrySapConnector>();
    })
    .Build();

host.Run();
