using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PocketLense.Infrastructure.Services;

namespace PocketLense.Api.Controllers;

[Route("api/imports"), RequestSizeLimit(6 * 1024 * 1024)]
public class ImportsController(CsvImportService imports) : UserController
{
    private static StatementData Read(IFormFile file)
    {
        if (file.Length == 0 || file.Length > 5 * 1024 * 1024)
            throw new AppException(400, "Upload a statement between 1 byte and 5 MB.");
        using var stream = file.OpenReadStream();
        return StatementFileReader.Read(stream, file.FileName);
    }

    [HttpPost("preview")]
    public IActionResult Preview(IFormFile file)
    {
        var statement = Read(file);
        return Ok(CsvImportService.Preview(statement.Text));
    }

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
        var statement = Read(file);
        var result = await imports.Commit(UserId, accountId, statement.Text, columns, statement.Source);
        return result.Errors.Count > 0 ? BadRequest(result) : Ok(result);
    }
}
