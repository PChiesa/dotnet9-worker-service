using FluentAssertions;
using WorkerService.Domain.ValueObjects;
using Xunit;

namespace WorkerService.UnitTests.Domain;

public class PriceTests
{
    [Fact]
    public void Price_Creation_With_Valid_Amount_Should_Set_Properties()
    {
        // Arrange
        var amount = 25.99m;
        var currency = "USD";

        // Act
        var price = new Price(amount, currency);

        // Assert
        price.Amount.Should().Be(amount);
        price.Currency.Should().Be(currency);
    }

    [Fact]
    public void Price_Creation_With_Default_Currency_Should_Use_USD()
    {
        // Arrange
        var amount = 25.99m;

        // Act
        var price = new Price(amount);

        // Assert
        price.Amount.Should().Be(amount);
        price.Currency.Should().Be("USD");
    }

    [Fact]
    public void Price_Creation_With_Negative_Amount_Should_Throw_Exception()
    {
        // Arrange
        var negativeAmount = -10.50m;

        // Act & Assert
        var action = () => new Price(negativeAmount);
        action.Should().Throw<ArgumentException>()
            .WithMessage("Price cannot be negative*")
            .WithParameterName("amount");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Price_Creation_With_Empty_Currency_Should_Throw_Exception(string invalidCurrency)
    {
        // Arrange
        var amount = 25.99m;

        // Act & Assert
        var action = () => new Price(amount, invalidCurrency);
        action.Should().Throw<ArgumentException>()
            .WithMessage("Currency cannot be empty*")
            .WithParameterName("currency");
    }

    [Fact]
    public void Price_Creation_Should_Round_Amount_To_Two_Decimal_Places()
    {
        // Arrange
        var amount = 25.999m; // Three decimal places

        // Act
        var price = new Price(amount);

        // Assert
        price.Amount.Should().Be(26.00m); // Rounded up
    }

    [Theory]
    [InlineData(25.994, 25.99)]
    [InlineData(25.995, 26.00)]
    [InlineData(25.996, 26.00)]
    [InlineData(25.001, 25.00)]
    [InlineData(25.000, 25.00)]
    public void Price_Creation_Should_Round_Correctly(decimal input, decimal expected)
    {
        // Act
        var price = new Price(input);

        // Assert
        price.Amount.Should().Be(expected);
    }

    [Fact]
    public void Price_Creation_Should_Convert_Currency_To_Uppercase()
    {
        // Arrange
        var amount = 25.99m;
        var currency = "eur";

        // Act
        var price = new Price(amount, currency);

        // Assert
        price.Currency.Should().Be("EUR");
    }

    [Theory]
    [InlineData("usd", "USD")]
    [InlineData("eur", "EUR")]
    [InlineData("gbp", "GBP")]
    [InlineData("Cad", "CAD")]
    [InlineData("JPY", "JPY")]
    public void Price_Creation_Should_Normalize_Currency_Case(string input, string expected)
    {
        // Arrange
        var amount = 100.00m;

        // Act
        var price = new Price(amount, input);

        // Assert
        price.Currency.Should().Be(expected);
    }

    [Fact]
    public void Price_Equality_With_Same_Values_Should_Return_True()
    {
        // Arrange
        var amount = 25.99m;
        var currency = "USD";
        var price1 = new Price(amount, currency);
        var price2 = new Price(amount, currency);

        // Act & Assert
        price1.Equals(price2).Should().BeTrue();
        price1.GetHashCode().Should().Be(price2.GetHashCode());
    }

    [Fact]
    public void Price_Equality_With_Different_Amount_Should_Return_False()
    {
        // Arrange
        var price1 = new Price(25.99m, "USD");
        var price2 = new Price(30.99m, "USD");

        // Act & Assert
        price1.Equals(price2).Should().BeFalse();
        price1.GetHashCode().Should().NotBe(price2.GetHashCode());
    }

    [Fact]
    public void Price_Equality_With_Different_Currency_Should_Return_False()
    {
        // Arrange
        var price1 = new Price(25.99m, "USD");
        var price2 = new Price(25.99m, "EUR");

        // Act & Assert
        price1.Equals(price2).Should().BeFalse();
        price1.GetHashCode().Should().NotBe(price2.GetHashCode());
    }

    [Fact]
    public void Price_Equals_With_Null_Should_Return_False()
    {
        // Arrange
        var price = new Price(25.99m);

        // Act & Assert
        price.Equals(null).Should().BeFalse();
        price.Equals((object?)null).Should().BeFalse();
    }

    [Fact]
    public void Price_Equals_With_Different_Type_Should_Return_False()
    {
        // Arrange
        var price = new Price(25.99m);
        var stringValue = "25.99 USD";

        // Act & Assert
        price.Equals(stringValue).Should().BeFalse();
    }

    [Fact]
    public void Price_ToString_Should_Return_Formatted_String()
    {
        // Arrange
        var price = new Price(25.99m, "USD");

        // Act
        var result = price.ToString();

        // Assert
        result.Should().Be("25.99 USD");
    }

    [Theory]
    [InlineData(0.00, "USD", "0.00 USD")]
    [InlineData(1.00, "EUR", "1.00 EUR")]
    [InlineData(999.99, "GBP", "999.99 GBP")]
    [InlineData(1000.00, "JPY", "1000.00 JPY")]
    [InlineData(0.01, "CAD", "0.01 CAD")]
    public void Price_ToString_Should_Format_Various_Values_Correctly(decimal amount, string currency, string expected)
    {
        // Arrange
        var price = new Price(amount, currency);

        // Act
        var result = price.ToString();

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Price_GetHashCode_Should_Be_Consistent()
    {
        // Arrange
        var price = new Price(25.99m, "USD");

        // Act
        var hashCode1 = price.GetHashCode();
        var hashCode2 = price.GetHashCode();

        // Assert
        hashCode1.Should().Be(hashCode2);
    }

    [Fact]
    public void Price_Equality_Operator_With_Same_Reference_Should_Return_True()
    {
        // Arrange
        var price = new Price(25.99m, "USD");

        // Act & Assert
        price.Equals(price).Should().BeTrue();
    }

    [Fact]
    public void Price_With_Zero_Amount_Should_Be_Valid()
    {
        // Act
        var price = new Price(0.00m);

        // Assert
        price.Amount.Should().Be(0.00m);
        price.Currency.Should().Be("USD");
    }

    [Fact]
    public void Price_With_Large_Amount_Should_Be_Valid()
    {
        // Arrange
        var largeAmount = 999999.99m;

        // Act
        var price = new Price(largeAmount);

        // Assert
        price.Amount.Should().Be(largeAmount);
    }

    [Fact]
    public void Price_With_Small_Decimal_Should_Round_Correctly()
    {
        // Arrange
        var smallAmount = 0.001m;

        // Act
        var price = new Price(smallAmount);

        // Assert
        price.Amount.Should().Be(0.00m);
    }

    [Fact]
    public void Price_Equality_Should_Handle_Currency_Case_Sensitivity()
    {
        // Arrange
        var price1 = new Price(25.99m, "usd");
        var price2 = new Price(25.99m, "USD");

        // Act & Assert
        price1.Equals(price2).Should().BeTrue();
        price1.Currency.Should().Be("USD");
        price2.Currency.Should().Be("USD");
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("EUR")]
    [InlineData("GBP")]
    [InlineData("JPY")]
    [InlineData("CAD")]
    [InlineData("AUD")]
    [InlineData("CHF")]
    public void Price_Creation_With_Various_Currencies_Should_Succeed(string currency)
    {
        // Arrange
        var amount = 100.00m;

        // Act
        var price = new Price(amount, currency);

        // Assert
        price.Amount.Should().Be(amount);
        price.Currency.Should().Be(currency.ToUpperInvariant());
    }

    [Fact]
    public void Price_With_Exactly_Two_Decimal_Places_Should_Not_Change()
    {
        // Arrange
        var amount = 25.99m;

        // Act
        var price = new Price(amount);

        // Assert
        price.Amount.Should().Be(25.99m);
    }

    [Fact]
    public void Price_With_One_Decimal_Place_Should_Have_Two()
    {
        // Arrange
        var amount = 25.9m;

        // Act
        var price = new Price(amount);

        // Assert
        price.Amount.Should().Be(25.90m);
    }

    [Fact]
    public void Price_With_No_Decimal_Places_Should_Have_Two()
    {
        // Arrange
        var amount = 25m;

        // Act
        var price = new Price(amount);

        // Assert
        price.Amount.Should().Be(25.00m);
    }
}