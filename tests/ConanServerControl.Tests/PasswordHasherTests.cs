using ConanServerControl.Core.Security;

namespace ConanServerControl.Tests;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_and_verify_round_trip()
    {
        var hash = PasswordHasher.Hash("correct horse battery staple");
        Assert.True(PasswordHasher.LooksLikeHash(hash));
        Assert.True(PasswordHasher.Verify("correct horse battery staple", hash));
        Assert.False(PasswordHasher.Verify("wrong password", hash));
        Assert.False(PasswordHasher.Verify("correct horse battery staple", "not-a-hash"));
        Assert.DoesNotContain("correct horse", hash, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_low_iteration_counts()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PasswordHasher.Hash("secret", iterations: 100));
    }
}
