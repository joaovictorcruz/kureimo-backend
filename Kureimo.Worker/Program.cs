using Kureimo.Application.Metrics;
using Kureimo.Infra;
using Kureimo.Worker;
using Kureimo.Worker.Jobs;
using OpenTelemetry.Metrics;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructureForWorker(builder.Configuration);

builder.Services.AddHttpClient("KureimoApi", (sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    client.BaseAddress = new Uri(config["InternalApi:BaseUrl"]
        ?? throw new InvalidOperationException("InternalApi:BaseUrl não configurada."));
    client.DefaultRequestHeaders.Add(
        "X-Internal-Api-Key",
        config["InternalApi:ApiKey"]);
});

if (builder.Environment.IsProduction())
{
    try
    {
        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics
                    .AddRuntimeInstrumentation()
                    .AddMeter(KureimoMetrics.MeterName)
                    .AddPrometheusHttpListener(options =>
                    {
                        options.UriPrefixes = new[] { "http://+:9464/" };
                    });
            });
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[Observability] Falha ao configurar métricas, seguindo sem elas: {ex.Message}");
    }
}

builder.Services.AddHostedService<AutoOpenSetsJob>();

var host = builder.Build();
host.Run();