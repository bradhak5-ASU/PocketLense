using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ExcelDataReader;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace PocketLense.Infrastructure.Services;

public record StatementData(string Text, string Source);

public static class StatementFileReader
{
    private static readonly Regex PdfRow = new(
        @"^(?<date>\d{1,2}[/-]\d{1,2}[/-]\d{2,4}|\d{4}-\d{1,2}-\d{1,2})\s+(?<description>.+?)\s+(?<amount>\(?[-+]?\$?[\d,]+\.\d{2}\)?)$",
        RegexOptions.Compiled);

    public static StatementData Read(Stream stream, string fileName)
    {
        string extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".csv" => new(ReadText(stream), "Csv"),
            ".xlsx" or ".xls" => new(ReadExcel(stream), "Excel"),
            ".pdf" => new(ReadPdf(stream), "Pdf"),
            _ => throw new AppException(400, "Choose a CSV, Excel, or PDF statement.")
        };
    }

    private static string ReadText(Stream stream)
    {
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string ReadExcel(Stream stream)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        try
        {
            using var reader = ExcelReaderFactory.CreateReader(stream);
            var rows = new List<string[]>();
            do
            {
                while (reader.Read())
                {
                    if (reader.FieldCount > 100 || rows.Count >= 50000)
                        throw new AppException(400, "The Excel statement is too large to import safely.");
                    var row = new string[reader.FieldCount];
                    for (int column = 0; column < reader.FieldCount; column++)
                    {
                        object? value = reader.GetValue(column);
                        row[column] = value is DateTime date
                            ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                            : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
                    }
                    if (row.Any(cell => !string.IsNullOrWhiteSpace(cell)))
                        rows.Add(row);
                }
            } while (rows.Count == 0 && reader.NextResult());

            if (rows.Count < 2)
                throw new AppException(400, "The first Excel sheet needs a header row and at least one transaction.");
            return ToCsv(rows);
        }
        catch (AppException)
        {
            throw;
        }
        catch
        {
            throw new AppException(400, "This Excel file could not be read. Try exporting it again.");
        }
    }

    private static string ReadPdf(Stream stream)
    {
        try
        {
            using var document = PdfDocument.Open(stream);
            if (document.NumberOfPages > 100)
                throw new AppException(400, "The PDF statement is too long to import safely.");
            List<string[]> rows = [["Date", "Description", "Amount"]];
            foreach (var page in document.GetPages())
            {
                string text = ContentOrderTextExtractor.GetText(page);
                foreach (string line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var match = PdfRow.Match(line.Trim());
                    if (!match.Success)
                        continue;
                    if (!TryDate(match.Groups["date"].Value, out var date))
                        continue;
                    string amount = match.Groups["amount"].Value.Replace("$", "").Replace(",", "");
                    if (amount.StartsWith('(') && amount.EndsWith(')'))
                        amount = "-" + amount[1..^1];
                    rows.Add([date.ToString("yyyy-MM-dd"), match.Groups["description"].Value.Trim(), amount]);
                    if (rows.Count > 50000)
                        throw new AppException(400, "The PDF statement has too many transaction rows.");
                }
            }
            if (rows.Count == 1)
                throw new AppException(400, "No transaction rows were found. Scanned PDFs need OCR, which is not available yet.");
            return ToCsv(rows);
        }
        catch (AppException)
        {
            throw;
        }
        catch
        {
            throw new AppException(400, "This PDF could not be read. Password-protected and scanned PDFs are not supported yet.");
        }
    }

    private static bool TryDate(string value, out DateOnly date)
    {
        string[] formats = ["M/d/yyyy", "MM/dd/yyyy", "M/d/yy", "MM/dd/yy", "yyyy-M-d", "yyyy-MM-dd"];
        return DateOnly.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    private static string ToCsv(IEnumerable<string[]> rows)
    {
        return string.Join('\n', rows.Select(row => string.Join(',', row.Select(Escape))));
    }

    private static string Escape(string value)
    {
        return value.IndexOfAny([',', '"', '\n']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }
}
