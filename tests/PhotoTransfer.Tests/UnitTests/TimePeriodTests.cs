using NUnit.Framework;
using PhotoTransfer.Models;

namespace PhotoTransfer.Tests.UnitTests;

[TestFixture]
[Category("Unit")]
public class TimePeriodTests
{
    [Test]
    public void Parse_ValidDateString_ShouldReturnTimePeriod()
    {
        // Act
        var result = TimePeriod.Parse("2023-06");

        // Assert
        Assert.That(result.Year, Is.EqualTo(2023));
        Assert.That(result.Month, Is.EqualTo(6));
    }

    [Test]
    public void Parse_WithLeadingDashes_ShouldParseCorrectly()
    {
        // Act
        var result = TimePeriod.Parse("--2023-06");

        // Assert
        Assert.That(result.Year, Is.EqualTo(2023));
        Assert.That(result.Month, Is.EqualTo(6));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Parse_InvalidDateString_ShouldThrowArgumentException(string dateString)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => TimePeriod.Parse(dateString));
    }

    [Test]
    public void Parse_NullDateString_ShouldThrowArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => TimePeriod.Parse(null!));
    }

    [TestCase("2023")]
    [TestCase("2023-6-15")]
    [TestCase("23-06")]
    [TestCase("invalid")]
    public void Parse_InvalidFormat_ShouldThrowFormatException(string dateString)
    {
        // Act & Assert
        Assert.Throws<FormatException>(() => TimePeriod.Parse(dateString));
    }

    [TestCase("1899-06")]
    [TestCase("2030-06")]
    public void Parse_InvalidYear_ShouldThrowFormatException(string dateString)
    {
        // Act & Assert
        Assert.Throws<FormatException>(() => TimePeriod.Parse(dateString));
    }

    [TestCase("2023-00")]
    [TestCase("2023-13")]
    public void Parse_InvalidMonth_ShouldThrowFormatException(string dateString)
    {
        // Act & Assert
        Assert.Throws<FormatException>(() => TimePeriod.Parse(dateString));
    }

    [Test]
    public void Contains_DateInPeriod_ShouldReturnTrue()
    {
        // Arrange
        var period = new TimePeriod(2023, 6);
        var date = new DateTime(2023, 6, 15);

        // Act
        var result = period.Contains(date);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void Contains_DateNotInPeriod_ShouldReturnFalse()
    {
        // Arrange
        var period = new TimePeriod(2023, 6);
        var date = new DateTime(2023, 7, 15);

        // Act
        var result = period.Contains(date);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void ToString_ShouldReturnFormattedString()
    {
        // Arrange
        var period = new TimePeriod(2023, 6);

        // Act
        var result = period.ToString();

        // Assert
        Assert.That(result, Is.EqualTo("2023-06"));
    }

    [Test]
    public void Equals_SamePeriods_ShouldReturnTrue()
    {
        // Arrange
        var period1 = new TimePeriod(2023, 6);
        var period2 = new TimePeriod(2023, 6);

        // Act & Assert
        Assert.That(period1.Equals(period2), Is.True);
        Assert.That(period1.GetHashCode(), Is.EqualTo(period2.GetHashCode()));
    }

    [Test]
    public void Equals_DifferentPeriods_ShouldReturnFalse()
    {
        // Arrange
        var period1 = new TimePeriod(2023, 6);
        var period2 = new TimePeriod(2023, 7);

        // Act & Assert
        Assert.That(period1.Equals(period2), Is.False);
    }
}