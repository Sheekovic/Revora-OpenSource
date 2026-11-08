namespace Revora.Auth.SimpleIdentity;
public sealed class RevoraPasswordHasher
{
    private readonly Argon2id.PasswordHasher.Argon2idPasswordHasher hasher = new();
    public string Hash(string password) => hasher.HashPassword(password);
    public bool Verify(string password, string encodedHash) => hasher.VerifyPassword(password, encodedHash);
}
