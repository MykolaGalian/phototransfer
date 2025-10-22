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

    /// <summary>
    /// Compares this period with another. Returns -1 if this is earlier, 0 if equal, 1 if later.
    /// </summary>
    public int CompareTo(TimePeriod other)
    {
        if (Year != other.Year)
            return Year.CompareTo(other.Year);
        return Month.CompareTo(other.Month);
    }

    /// <summary>
    /// Checks if this period is within the specified range (inclusive)
    /// </summary>
    public bool IsInRange(TimePeriod start, TimePeriod end)
    {
        return CompareTo(start) >= 0 && CompareTo(end) <= 0;
    }

    /// <summary>
    /// Generates all periods between start and end (inclusive)
    /// </summary>
    public static List<TimePeriod> GetRange(TimePeriod start, TimePeriod end)
    {
        var periods = new List<TimePeriod>();
        var current = new TimePeriod(start.Year, start.Month);

        while (current.CompareTo(end) <= 0)
        {
            periods.Add(new TimePeriod(current.Year, current.Month));

            // Move to next month
            if (current.Month == 12)
            {
                current = new TimePeriod(current.Year + 1, 1);
            }
            else
            {
                current = new TimePeriod(current.Year, current.Month + 1);
            }
        }

        return periods;
    }

    /// <summary>
    /// Parses a period range string like "2020-01..2022-12" or "2020-01:2022-12"
    /// Returns null if not a range format
    /// </summary>
    public static (TimePeriod start, TimePeriod end)? ParseRange(string rangeString)
    {
        if (string.IsNullOrWhiteSpace(rangeString))
            return null;

        // Try different range separators
        string[] separators = { "..", ":", " to ", "-to-" };

        foreach (var separator in separators)
        {
            if (rangeString.Contains(separator))
            {
                var parts = rangeString.Split(separator, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    try
                    {
                        var start = Parse(parts[0].Trim());
                        var end = Parse(parts[1].Trim());

                        if (start.CompareTo(end) > 0)
                        {
                            throw new FormatException("Start period must be before or equal to end period");
                        }

                        return (start, end);
                    }
                    catch
                    {
                        // Continue trying other separators
                    }
                }
            }
        }

        return null;
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