using API.DTOs;
using Infrastructure.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace API.Controllers;

public class ConfigController(IOptions<StripeSettings> stripe) : BaseApiController
{
    // Only the publishable key leaves the server; the secret key never does.
    [HttpGet]
    public ActionResult<ConfigDto> GetConfig()
    {
        return new ConfigDto { PublishableKey = stripe.Value.PublishableKey, Currency = stripe.Value.Currency };
    }
}
