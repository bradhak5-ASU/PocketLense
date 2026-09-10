using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PocketLense.Infrastructure.Services;

namespace PocketLense.Api.Controllers;

[Route("api/imports"), RequestSizeLimit(6 * 1024 * 1024)]
public class ImportsController(CsvImportService imports) : UserController
{
    private static async Task<string> Read(IFormFile file)
    {
        if (file.Length == 0 || file.Length > 5 * 1024 * 1024)
            throw new AppException(400, "Upload a CSV file between 1 byte and 5 MB.");
        using var reader = new StreamReader(file.OpenReadStream());
        return await reader.ReadToEndAsync();
    }

    [HttpPost("preview")]
    public async Task<IActionResult> Preview(IFormFile file) => Ok(CsvImportService.Preview(await Read(file)));

    [HttpPost("commit")]
    public async Task<IActionResult> Commit(IFormFile file, [FromForm] Guid accountId, [FromForm] string mapping)
    {
        CsvMapping? columns;
        try
        {
            columns = JsonSerializer.Deserialize<CsvMapping>(mapping, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (JsonException) { return Problem(statusCode: 400, title: "The column mapping is invalid."); }
        if (columns == null)
            return Problem(statusCode: 400, title: "Choose a column mapping.");
        var result = await imports.Commit(UserId, accountId, await Read(file), columns);
        return result.Errors.Count > 0 ? BadRequest(result) : Ok(result);
    }
}
