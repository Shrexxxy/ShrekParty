using AwesomeAssertions;
using ShrekParty.Api.Registration;

namespace ShrekParty.Tests.Registration;

public class PasswordMaskTests
{
    [Fact]
    public void Mask_six_digits() => PasswordMask.Mask("123456").Should().Be("1*34*6");

    [Fact]
    public void Mask_single_char() => PasswordMask.Mask("a").Should().Be("*");

    [Fact]
    public void Mask_hides_at_least_quarter()
    {
        for (var length = 1; length <= 30; length++)
        {
            var masked = PasswordMask.Mask(new string('x', length));

            masked.Length.Should().Be(length);
            masked.Count(c => c == '*').Should().BeGreaterThanOrEqualTo((int)Math.Ceiling(length * 0.25), $"длина {length}");
        }
    }
}
