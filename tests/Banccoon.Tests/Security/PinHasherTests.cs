using Banccoon.Core.Security;
using Xunit;

namespace Banccoon.Tests.Security;

public sealed class PinHasherTests
{
    [Fact]
    public void Verify_WithCorrectPin_ReturnsTrue()
    {
        var salt = PinHasher.GenerateSalt();
        var hash = PinHasher.Hash("1234", salt);

        Assert.True(PinHasher.Verify("1234", salt, hash));
    }

    [Fact]
    public void Verify_WithWrongPin_ReturnsFalse()
    {
        var salt = PinHasher.GenerateSalt();
        var hash = PinHasher.Hash("1234", salt);

        Assert.False(PinHasher.Verify("4321", salt, hash));
    }

    [Fact]
    public void GenerateSalt_ProducesDifferentSaltsEachTime()
    {
        var first = PinHasher.GenerateSalt();
        var second = PinHasher.GenerateSalt();

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Hash_SamePinWithDifferentSalts_ProducesDifferentHashes()
    {
        var firstSalt = PinHasher.GenerateSalt();
        var secondSalt = PinHasher.GenerateSalt();

        var firstHash = PinHasher.Hash("1234", firstSalt);
        var secondHash = PinHasher.Hash("1234", secondSalt);

        Assert.NotEqual(firstHash, secondHash);
    }
}
