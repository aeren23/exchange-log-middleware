namespace ExchangeLogMiddleware.Producer.Configuration;

/// <summary>
/// seed-data.json içerisindeki yapılandırılmış sahte verileri (mock data) temsil eder.
/// </summary>
public sealed class SeedDataOptions
{
    public const string SectionName = "SeedData";

    public string[] DatabaseMessages { get; set; } = [];
    public string[] AuthMessages { get; set; } = [];
    public string[] SystemMessages { get; set; } = [];
    public string[] TcknNumbers { get; set; } = [];
    public string[] CreditCardNumbers { get; set; } = [];
    public string[] EmailAddresses { get; set; } = [];
    public string[] PhoneNumbers { get; set; } = [];
}
