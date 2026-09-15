using Microsoft.AspNetCore.Mvc;
using PocketLense.Infrastructure.Services;

namespace PocketLense.Api.Controllers;

[Route("api/dashboard")]
public class DashboardController(FinanceService finance) : UserController
{
    [HttpGet]
    public async Task<IActionResult> Get(string? month) => Ok(await finance.Dashboard(UserId, Month(month)));
}

[Route("api/features")]
public class FeaturesController(IConfiguration configuration) : UserController
{
    [HttpGet]
    public IActionResult Get() => Ok(new { aiAssistant = AssistantController.AssistantEnabled(configuration), emailSync = false });
}
