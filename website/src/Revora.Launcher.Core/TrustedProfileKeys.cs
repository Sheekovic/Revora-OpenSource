namespace Revora.Launcher.Core;

public static class TrustedProfileKeys
{
    public static ReadOnlyMemory<byte> Testing => Convert.FromBase64String("MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEUKl8GIjNNOcJR2Jz5B6551j90Tb2nDFeZOiTZsu6Lbm+sWWe1s4dyc7K5UYrx8dIjDa4/8DPjR9idDhK/iTEsw==");
    public static ReadOnlyMemory<byte> Production => Convert.FromBase64String("MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEw/fOtfDJOMFY1KtrgcydEMjD0OPo8pe4JZYlkvc5UgjpVKqSr6/9NAyg/lWPLxuBJQSZRwt2ySfiU75/07a8Dg==");
}
