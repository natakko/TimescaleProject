using System.ComponentModel.DataAnnotations;

namespace TimescaleProject.Models;

public class FileResult
{
    [Key]
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public double TimeDelta { get; set; }
    public DateTime FirstOperationDate { get; set; }
    public double AverageExecutionTime { get; set; }
    public double AverageValue { get; set; }
    public double MedianValue { get; set; }
    public double MaxValue { get; set; }
    public double MinValue { get; set; }
}

public class DataValue
{
    [Key]
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public double ExecutionTime { get; set; }
    public double Value { get; set; }
}
