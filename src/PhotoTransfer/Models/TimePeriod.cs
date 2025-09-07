namespace PhotoTransfer.Models;

public class TimePeriod
{
    public int Year { get; set; }
    public int Month { get; set; }

    public TimePeriod(int year, int month)
    {
        Year = year;
        Month = month;
    }

    public static TimePeriod Parse(string dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString))
        {
            throw new ArgumentException("Date string cannot be null or empty", nameof(dateString));
        }

        // Remove leading dashes if present (e.g., "--2012-01" -> "2012-01")
        var cleanDate = dateString.TrimStart('-');
        
        var parts = cleanDate.Split('-');
        if (parts.Length != 2)
        {
            throw new FormatException($"Invalid date format: {dateString}. Expected format: YYYY-MM");
        }

        if (!int.TryParse(parts[0], out var year) || year < 1900 || year > DateTime.Now.Year + 1)
        {
            throw new FormatException($"Invalid year: {parts[0]}");
        }

        if (!int.TryParse(parts[1], out var month) || month < 1 || month > 12)
        {
            throw new FormatException($"Invalid month: {parts[1]}");
        }

        return new TimePeriod(year, month);
    }

    public bool Contains(DateTime date)
    {
        return date.Year == Year && date.Month == Month;
    }

    public override string ToString()
    {
        return $"{Year:D4}-{Month:D2}";
    }

    public override bool Equals(object? obj)
    {
        return obj is TimePeriod other && Year == other.Year && Month == other.Month;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Year, Month);
    }
}