using ExchangeLogMiddleware.Producer.Configuration;
using ExchangeLogMiddleware.Producer.Generators;
using ExchangeLogMiddleware.Producer.Services;
using ExchangeLogMiddleware.Shared.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<ProducerSettings>(
    builder.Configuration.GetSection(ProducerSettings.SectionName));

builder.Services.AddMessageBroker(builder.Configuration);
builder.Services.AddSingleton<LogDataGenerator>();
builder.Services.AddHostedService<LogGeneratorService>();

var host = builder.Build();
host.Run();
