using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimescaleProject.Models;
using System.Globalization;

namespace TimescaleProject.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DataController : ControllerBase
{
    private readonly AppDbContext _context;
    public DataController(AppDbContext context) => _context = context;

    [HttpPost("upload")]
    public async Task<IActionResult> UploadCsv(IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest("Файл не выбран.");

        var lines = new List<string>();
        using (var reader = new StreamReader(file.OpenReadStream()))
        {
            while (!reader.EndOfStream) lines.Add(await reader.ReadLineAsync() ?? "");
        }


        var dataLines = lines.Skip(1).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        if (dataLines.Count < 1 || dataLines.Count > 10000)
            return BadRequest("Количество строк должно быть от 1 до 10 000.");

        var dataPoints = new List<DataValue>();
        try
        {
            foreach (var line in dataLines)
            {
                var parts = line.Split(';');
                if (parts.Length != 3) return BadRequest("Неверный формат CSV.");

                var date = DateTime.Parse(parts[0], null, DateTimeStyles.RoundtripKind);
                var execTime = double.Parse(parts[1], CultureInfo.InvariantCulture);
                var value = double.Parse(parts[2], CultureInfo.InvariantCulture);


                if (date > DateTime.UtcNow || date < new DateTime(2000, 1, 1)) return BadRequest("Неверная дата.");
                if (execTime < 0 || value < 0) return BadRequest("Значения не могут быть меньше 0.");

                dataPoints.Add(new DataValue { Date = date, ExecutionTime = execTime, Value = value, FileName = file.FileName });
            }
        }
        catch { return BadRequest("Ошибка преобразования типов данных."); }


        var sorted = dataPoints.OrderBy(p => p.Date).ToList();
        var values = dataPoints.Select(p => p.Value).OrderBy(v => v).ToList();

        var result = new TimescaleProject.Models.FileResult
        {
            FileName = file.FileName,
            FirstOperationDate = sorted.First().Date,
            TimeDelta = (sorted.Last().Date - sorted.First().Date).TotalSeconds,
            AverageExecutionTime = dataPoints.Average(p => p.ExecutionTime),
            AverageValue = dataPoints.Average(p => p.Value),
            MaxValue = values.Last(),
            MinValue = values.First(),
            MedianValue = values.Count % 2 == 0 ? (values[values.Count / 2 - 1] + values[values.Count / 2]) / 2 : values[values.Count / 2]
        };

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var old = await _context.Results.FirstOrDefaultAsync(r => r.FileName == file.FileName);
            if (old != null)
            {
                _context.Values.RemoveRange(_context.Values.Where(v => v.FileName == file.FileName));
                _context.Results.Remove(old);
            }
            _context.Results.Add(result);
            _context.Values.AddRange(dataPoints);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok("Файл успешно обработан.");
        }
        catch { return StatusCode(500, "Ошибка БД."); }
    }


    [HttpGet("results")]
    public async Task<IActionResult> GetResults(
        [FromQuery] string? fileName,
        [FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo,
        [FromQuery] double? avgValMin, [FromQuery] double? avgValMax)
    {
        var query = _context.Results.AsQueryable();

        if (!string.IsNullOrEmpty(fileName))
            query = query.Where(r => r.FileName.Contains(fileName));

        if (dateFrom.HasValue) query = query.Where(r => r.FirstOperationDate >= dateFrom);
        if (dateTo.HasValue) query = query.Where(r => r.FirstOperationDate <= dateTo);

        if (avgValMin.HasValue) query = query.Where(r => r.AverageValue >= avgValMin);
        if (avgValMax.HasValue) query = query.Where(r => r.AverageValue <= avgValMax);

        return Ok(await query.ToListAsync());
    }


    [HttpGet("last-values/{fileName}")]
    public async Task<IActionResult> GetLastValues(string fileName)
    {
        var values = await _context.Values
            .Where(v => v.FileName == fileName)
            .OrderByDescending(v => v.Date)
            .Take(10)
            .ToListAsync();

        return Ok(values);
    }

}
