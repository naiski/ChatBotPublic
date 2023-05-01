namespace ServicesTests;

public class TextGenerationServiceUnitTests
{
    [Theory]
    [InlineData("This is a reply.")]
    [InlineData("This is a reply.\n(User1): This is another reply.")]
    [InlineData("This is a reply.\n(User1): This is another reply.\n(User2): This is yet another reply.")]
    public static void TestSanitizeReply(string reply)
    {
        reply = TextGenerationService.SanitizeReply(reply);
        reply.Should().Be("This is a reply.");
    }
}