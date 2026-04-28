using Kureimo.Infra;
using Kureimo.Worker;
using Kureimo.Worker.Jobs;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpClient("KureimoApi", (sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    client.BaseAddress = new Uri(config["InternalApi:BaseUrl"]
        ?? throw new InvalidOperationException("InternalApi:BaseUrl não configurada."));
    client.DefaultRequestHeaders.Add(
        "X-Internal-Api-Key",
        config["InternalApi:ApiKey"]);
});

builder.Services.AddHostedService<AutoOpenSetsJob>();

var host = builder.Build();
host.Run();