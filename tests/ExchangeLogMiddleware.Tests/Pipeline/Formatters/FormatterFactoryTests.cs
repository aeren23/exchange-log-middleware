namespace ExchangeLogMiddleware.Tests.Pipeline.Formatters;

using ExchangeLogMiddleware.Middleware.Configuration;
using ExchangeLogMiddleware.Middleware.Pipeline.Formatters;
using ExchangeLogMiddleware.Shared.Enums;
using ExchangeLogMiddleware.Shared.Interfaces;
using Microsoft.Extensions.Options;

public sealed class FormatterFactoryTests
{
    private readonly IEnumerable<IFormatterStrategy> _allStrategies;

    public FormatterFactoryTests()
    {
        _allStrategies =
        [
            new JsonFormatterStrategy(),
            new CsvFormatterStrategy(),
            new MarkdownFormatterStrategy(),
            new HtmlFormatterStrategy()
        ];
    }

    [Fact]
    public void GetStrategies_ReturnsOnlyStrategiesForGivenRole()
    {
        // Arrange
        var settings = new RouterSettings
        {
            RoleFormatters = new Dictionary<TargetRole, List<string>>
            {
                { TargetRole.Developer, ["Json"] }
            }
        };
        var options = Options.Create(settings);
        var factory = new FormatterFactory(_allStrategies, options);

        // Act
        var strategies = factory.GetStrategies(TargetRole.Developer).ToList();

        // Assert
        Assert.Single(strategies);
        Assert.IsType<JsonFormatterStrategy>(strategies[0]);
    }

    [Fact]
    public void GetStrategies_FanOut_ReturnsMultipleStrategiesIfConfigured()
    {
        // Arrange
        var settings = new RouterSettings
        {
            RoleFormatters = new Dictionary<TargetRole, List<string>>
            {
                { TargetRole.SysAdmin, ["Markdown", "Html"] }
            }
        };
        var options = Options.Create(settings);
        var factory = new FormatterFactory(_allStrategies, options);

        // Act
        var strategies = factory.GetStrategies(TargetRole.SysAdmin).ToList();

        // Assert
        Assert.Equal(2, strategies.Count);
        Assert.Contains(strategies, s => s is MarkdownFormatterStrategy);
        Assert.Contains(strategies, s => s is HtmlFormatterStrategy);
    }

    [Fact]
    public void GetStrategies_NoConfigFound_ReturnsAllStrategiesForThatRole()
    {
        // Arrange - Boş konfigürasyon (Fallback testi)
        var settings = new RouterSettings(); 
        var options = Options.Create(settings);
        var factory = new FormatterFactory(_allStrategies, options);

        // Act
        var strategies = factory.GetStrategies(TargetRole.SysAdmin).ToList();

        // Assert - Hem Markdown hem de Html TargetRole.SysAdmin olarak işaretli
        Assert.Equal(2, strategies.Count);
    }

    [Fact]
    public void GetStrategies_IgnoresCase_InConfig()
    {
        // Arrange
        var settings = new RouterSettings
        {
            RoleFormatters = new Dictionary<TargetRole, List<string>>
            {
                { TargetRole.Developer, ["jSoN"] } // Case-insensitive
            }
        };
        var options = Options.Create(settings);
        var factory = new FormatterFactory(_allStrategies, options);

        // Act
        var strategies = factory.GetStrategies(TargetRole.Developer).ToList();

        // Assert
        Assert.Single(strategies);
        Assert.IsType<JsonFormatterStrategy>(strategies[0]);
    }
}
