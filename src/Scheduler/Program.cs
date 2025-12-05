// Copyright (c) 2025 Duplicati Inc.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
// of the Software, and to permit persons to whom the Software is furnished to do
// so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
using ConsoleCommon;
using MassTransit;
using MassTransit.SqlTransport.PostgreSql;
using Quartz;
using Scheduler.MassTransit;
using Scheduler.Messages;
using Scheduler.Quartz;
using Scheduler.Schedules;

var builder = WebApplication.CreateSlimBuilder(args);
builder.ConfigureForDevelopment();

var serilogConfig = builder.Configuration.GetSection("Serilog").Get<SerilogConfig>();
builder.AddCommonLogging(serilogConfig, null);

var securityconfig = builder.Configuration.GetSection("Security").Get<SimpleSecurityOptions>();
builder.AddSimpleSecurityFilter(securityconfig, msg => Serilog.Log.Warning(msg));

var databaseConnectionString = builder.Configuration.GetValue<string>("Database:ConnectionString");
var adminDatabaseConnectionString = builder.Configuration.GetValue<string>("Database:AdminConnectionString");
var messagingConnectionString = builder.Configuration.GetValue<string>("Messaging:ConnectionString");
var adminMessagingConnectionString = builder.Configuration.GetValue<string>("Messaging:AdminConnectionString");
var redirectUrl = builder.Configuration.GetValue<string>("Environment:RedirectUrl");
var emitApiKey = builder.Configuration.GetValue<string>("Environment:EmitApiKey");

// For dev setups, we assume the connection string has admin rights if no admin connection string is explicitly set
if (builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(adminDatabaseConnectionString))
    adminDatabaseConnectionString = databaseConnectionString;
if (builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(adminMessagingConnectionString))
    adminMessagingConnectionString = messagingConnectionString;

if (!string.IsNullOrWhiteSpace(adminDatabaseConnectionString))
    QuartzSchemaInitializer.CreateSchemaIfMissing(adminDatabaseConnectionString);

if (!string.IsNullOrWhiteSpace(adminMessagingConnectionString))
    await MessagingSchemaInitializer.RunPostgresMigrationsAsync(adminMessagingConnectionString, CancellationToken.None);

// Support database-less development environment in development mode
if (!builder.Environment.IsDevelopment())
{
    if (string.IsNullOrWhiteSpace(databaseConnectionString))
        throw new InvalidOperationException("Database connection string is not configured.");
    if (string.IsNullOrWhiteSpace(messagingConnectionString))
        throw new InvalidOperationException("Messaging connection string is not configured.");
}

if (string.Equals(args.FirstOrDefault(), "initonly", StringComparison.OrdinalIgnoreCase))
{
    Serilog.Log.Information("Initialized database schema and exiting as requested");
    Console.WriteLine("Initialized database schema and exiting as requested");
    return;
}

builder.Services.AddHttpContextAccessor();

builder.Services.AddQuartz(configure =>
{
    if (string.IsNullOrWhiteSpace(databaseConnectionString))
        configure.UseInMemoryStore();
    else
        configure.UsePersistentStore(persistentStoreOptions =>
        {
            persistentStoreOptions.UseProperties = true;
            persistentStoreOptions.UsePostgres(providerOptions => { providerOptions.ConnectionString = databaseConnectionString; });
            persistentStoreOptions.UseSystemTextJsonSerializer();
        });
});

builder.Services.AddMassTransit(x =>
{
    x.AddPublishMessageScheduler();
    x.AddQuartzConsumers();

    if (string.IsNullOrWhiteSpace(messagingConnectionString))
        x.UsingInMemory((context, configurator) =>
        {
            configurator.UsePublishMessageScheduler();
            configurator.ConfigureEndpoints(context);
        });
    else
        x.UsingPostgres((context, configurator) =>
        {
            configurator.Host(new PostgresSqlHostSettings(messagingConnectionString));
            configurator.UsePublishMessageScheduler();
            configurator.ConfigureEndpoints(context);
        });
});

var app = builder.Build();

app.UseCommonLogging();
app.UseSimpleSecurityFilter(securityconfig);

app.MapGet("/health", () => "OK");
app.MapGet("/", ctx =>
{
    if (string.IsNullOrWhiteSpace(redirectUrl))
        ctx.Response.StatusCode = 404;
    else
        ctx.Response.Redirect(redirectUrl);
    return Task.CompletedTask;
});

// Support manual message emission for testing purposes
if (!string.IsNullOrWhiteSpace(emitApiKey))
{
    app.MapGet("/emit", ctx =>
    {
        var apiKey = ctx.Request.Query["apiKey"].ToString();
        if (apiKey != emitApiKey)
        {
            ctx.Response.StatusCode = 403;
            return Task.CompletedTask;
        }

        var type = ctx.Request.Query["type"].ToString().ToLowerInvariant().Trim();
        if (string.Equals(type, "tenminutes", StringComparison.OrdinalIgnoreCase))
            ctx.RequestServices.GetRequiredService<IBus>().Publish(new EveryTenMinutesMessage(), ctx => { ctx.TimeToLive = TimeSpan.FromMinutes(5); });
        else if (string.Equals(type, "hourly", StringComparison.OrdinalIgnoreCase))
            ctx.RequestServices.GetRequiredService<IBus>().Publish(new HourlyMessage(), ctx => { ctx.TimeToLive = TimeSpan.FromMinutes(30); });
        else if (string.Equals(type, "daily", StringComparison.OrdinalIgnoreCase))
            ctx.RequestServices.GetRequiredService<IBus>().Publish(new DailyMessage(), ctx => { ctx.TimeToLive = TimeSpan.FromMinutes(60); });
        else
        {
            ctx.Response.StatusCode = 400;
            return Task.CompletedTask;
        }

        ctx.Response.StatusCode = 200;
        return Task.CompletedTask;
    });
}

await using var scope = app.Services.CreateAsyncScope();
var scheduler = scope.ServiceProvider.GetRequiredService<IRecurringMessageScheduler>();
await Task.WhenAll(
    scheduler.ScheduleRecurringPublish(
        new EveryTenMinutesSchedule(),
        new EveryTenMinutesMessage(),
        Pipe.Execute<SendContext<EveryTenMinutesMessage>>(ctx => { ctx.TimeToLive = TimeSpan.FromMinutes(5); })),
    scheduler.ScheduleRecurringPublish(
        new HourlySchedule(),
        new HourlyMessage(),
        Pipe.Execute<SendContext<HourlyMessage>>(ctx => { ctx.TimeToLive = TimeSpan.FromMinutes(30); })),
    scheduler.ScheduleRecurringPublish(
        new DailySchedule(),
        new DailyMessage(),
        Pipe.Execute<SendContext<DailyMessage>>(ctx => { ctx.TimeToLive = TimeSpan.FromMinutes(60); }))
);

app.Run();