using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Revora.Auth.SimpleIdentity;

namespace Revora.Admin;

public sealed class AdminSecurityStore(string dataRoot, IDataProtectionProvider protection)
{
	private readonly string path = Path.Combine(dataRoot, "administrator.json");

	public bool Exists => File.Exists(path);

	public AdminCredential Load()
	{
		return JsonSerializer.Deserialize<AdminCredential>(File.ReadAllText(path)) ?? throw new InvalidDataException("Administrator credential is invalid.");
	}

	public void Bootstrap(string username, string password, byte[] secret, RevoraPasswordHasher hasher)
	{
		if (Exists)
		{
			throw new InvalidOperationException("Administrator bootstrap has already completed.");
		}
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		string protectedTotpSecret = protection.CreateProtector("Revora.Admin.Totp.v1").Protect(Convert.ToBase64String(secret));
		AdminCredential value = new AdminCredential(username, hasher.Hash(password), protectedTotpSecret, Guid.NewGuid());
		using FileStream utf8Json = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
		JsonSerializer.Serialize(utf8Json, value);
	}

	public bool VerifyTotp(AdminCredential credential, string code)
	{
		byte[] array;
		try
		{
			array = Convert.FromBase64String(protection.CreateProtector("Revora.Admin.Totp.v1").Unprotect(credential.ProtectedTotpSecret));
		}
		catch (CryptographicException)
		{
			return false;
		}
		try
		{
			return VerifyCode(array, code, DateTimeOffset.UtcNow);
		}
		finally
		{
			CryptographicOperations.ZeroMemory(array);
		}
	}

	public static bool VerifyCode(byte[] secret, string code, DateTimeOffset now)
	{
		if (code.Length != 6 || !int.TryParse(code, out var result))
		{
			return false;
		}
		long num = now.ToUnixTimeSeconds() / 30;
		Span<byte> span = stackalloc byte[8];
		for (long num2 = -1L; num2 <= 1; num2++)
		{
			BinaryPrimitives.WriteInt64BigEndian(span, num + num2);
			byte[] array = HMACSHA1.HashData(secret, span);
			int num3 = array[^1] & 0xF;
			int num4 = (((array[num3] & 0x7F) << 24) | (array[num3 + 1] << 16) | (array[num3 + 2] << 8) | array[num3 + 3]) % 1000000;
			if (num4 == result)
			{
				return true;
			}
		}
		return false;
	}
}
