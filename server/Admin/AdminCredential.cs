using System;

namespace Revora.Admin;

public sealed record AdminCredential(string Username, string PasswordHash, string ProtectedTotpSecret, Guid SecurityStamp);
