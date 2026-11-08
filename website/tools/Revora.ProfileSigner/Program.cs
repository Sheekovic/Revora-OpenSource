using System.Security.Cryptography;
using System.Text.Json;
using Revora.Launcher.Core;

if (args.Length != 4 || args[0] != "sign")
{
    Console.Error.WriteLine("Usage: Revora.ProfileSigner sign <payload.json> <private-key.pem> <output.json>");
    return 2;
}
byte[] source = await File.ReadAllBytesAsync(args[1]);
ClientProfile profile = JsonSerializer.Deserialize<ClientProfile>(source) ?? throw new InvalidDataException("Profile payload is empty.");
byte[] payload = ProfileCodec.SerializeCanonical(profile);
string keyId = profile.Channel == "testing" ? LauncherPolicy.TestingSigningKeyId : LauncherPolicy.ProductionSigningKeyId;
using ECDsa signer = ECDsa.Create();
signer.ImportFromPem(await File.ReadAllTextAsync(args[2]));
byte[] signature = signer.SignData(payload, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
byte[] envelope = JsonSerializer.SerializeToUtf8Bytes(new SignedEnvelope(keyId, "ECDSA-P256-SHA256", Convert.ToBase64String(payload), Convert.ToBase64String(signature)));
await File.WriteAllBytesAsync(args[3], envelope);
return 0;
