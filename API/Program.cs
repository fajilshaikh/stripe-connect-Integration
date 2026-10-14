using System.Text.Json.Serialization;
using System.Threading.Channels;
using API.Middleware;
using Azure.Identity;
using Core.Interfaces;
using Infrastructure.Data;
using Infrastructure.Services;
using Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stripe;

var builder = WebApplication.CreateBuilder(args);

// Production: secrets live only in Azure Key Vault (names like "Stripe--SecretKey"), read via managed identity.
// Local: `dotnet user-secrets set "Stripe:SecretKey" sk_test_...`
if (builder.Configuration["KeyVault:Uri"] is { Length: > 0 } vault)
    builder.Configuration.AddAzureKeyVault(new Uri(vault), new DefaultAzureCredential());

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection("Stripe"));
builder.Services.AddSingleton<IStripeClient>(sp => sp.GetRequiredService<IOptions<StripeSettings>>().Value.SecretKey is { Length: > 0 } key
    ? new StripeClient(key)
    : throw new InvalidOperationException("Stripe:SecretKey is not configured (user-secrets locally, Key Vault in Azure)."));

builder.Services.AddDbContext<PaymentsContext>(opt =>
{
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});
builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();
builder.Services.AddScoped<IBookingRepository, BookingRepository>();
builder.Services.AddScoped<IStripeEventStore, StripeEventStore>();

builder.Services.AddSingleton<IPaymentFlowLog, PaymentFlowLog>();
builder.Services.AddScoped<ISupplierOnboardingService, SupplierOnboardingService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();

// Webhook queue: controllers enqueue verified events, the background processor handles them.
builder.Services.AddSingleton(Channel.CreateUnbounded<Event>());
builder.Services.AddHostedService<StripeEventProcessor>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseMiddleware<ExceptionMiddleware>();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    // Creates the database on first run and applies any pending migrations.
    await scope.ServiceProvider.GetRequiredService<PaymentsContext>().Database.MigrateAsync();

    // Refresh supplier status from Stripe and pick up accounts created outside the app (e.g. in the Dashboard).
    try
    {
        await scope.ServiceProvider.GetRequiredService<ISupplierOnboardingService>().ImportFromStripeAsync(CancellationToken.None);
    }
    catch (StripeException e)
    {
        app.Logger.LogWarning("Could not import suppliers from Stripe: {Message}", e.Message);
    }
}

app.Run();
