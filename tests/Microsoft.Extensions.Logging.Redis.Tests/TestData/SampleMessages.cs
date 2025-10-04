namespace Microsoft.Extensions.Logging.Redis.Tests.TestData;

public static class SampleMessages
{
    public const string Simple = "Hello World";
    public const string WithParameters = "User {userId} logged in";
    public static string Long => new('a', 10_240);
    public static string VeryLong => new('b', 1_048_576);
    public const string Unicode = "Hello 世界 🌍 مرحبا Привет";
    public const string SpecialCharacters = "Line1\nLine2\tTabbed \"Quoted\"";
    public const string Empty = "";
    public const string Whitespace = "   ";
}
