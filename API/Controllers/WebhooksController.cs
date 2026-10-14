using System.Threading.Channels;
using Infrastructure.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stripe;

namespace API.Controllers;

/// Two endpoints with separate signing secrets: platform events (all payments, since destination charges live on the platform)
/// and Connect events (connected-account events such as account.updated).
public class WebhooksController(Channel<Event> queue, IOptions<StripeSettings> stripe, ILogger<WebhooksController> logger)
    : BaseApiController
{
    [HttpPost("platform")]
    public Task<IActionResult> PlatformWebhook() => Receive(stripe.Value.PlatformWebhookSecret);

    [HttpPost("connect")]
    public Task<IActionResult> ConnectWebhook() => Receive(stripe.Value.ConnectWebhookSecret);

    // Verify the signature against the untouched raw body, enqueue, acknowledge immediately.
    private async Task<IActionResult> Receive(string secret)
    {
        var json = await new StreamReader(Request.Body).ReadToEndAsync();
        if (Request.Headers["Stripe-Signature"] is not [{ Length: > 0 } signature]) return BadRequest();

        try
        {
            var stripeEvent = EventUtility.ConstructEvent(json, signature, secret, throwOnApiVersionMismatch: false);
            await queue.Writer.WriteAsync(stripeEvent);
            return Ok();
        }
        catch (Exception ex) when (ex is StripeException or Newtonsoft.Json.JsonException)
        {
            logger.LogWarning("Webhook signature verification failed: {Message}", ex.Message);
            return BadRequest();
        }
    }
}
