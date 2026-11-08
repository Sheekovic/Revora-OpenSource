namespace Revora.Launcher.Core;

public static class SafePath
{
    public static bool IsNormalizedRelative(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 240 || Path.IsPathRooted(value) || value.Contains('\\'))
        {
            return false;
        }

        string[] segments = value.Split('/');
        return segments.All(IsSafeWindowsSegment);
    }

    public static string ResolveContained(string root, string relative)
    {
        if (!IsNormalizedRelative(relative))
        {
            throw new InvalidDataException("The manifest contains an unsafe destination path.");
        }

        string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string candidate = Path.GetFullPath(Path.Combine(fullRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!candidate.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The manifest destination escapes the client directory.");
        }

        return candidate;
    }

    public static void RejectExistingReparsePoints(string root, string candidate)
    {
        string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        string fullCandidate = Path.GetFullPath(candidate);
        if (!fullCandidate.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The destination escapes the client directory.");
        }

        for (string? current = fullCandidate; current is not null && current.Length >= fullRoot.Length; current = Path.GetDirectoryName(current))
        {
            if ((File.Exists(current) || Directory.Exists(current)) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("Links are not permitted in a verified client path.");
            }

            if (string.Equals(current, fullRoot, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
        }
    }

    private static bool IsSafeWindowsSegment(string segment)
    {
        if (segment.Length is 0 or > 120 || segment is "." or ".." || segment.EndsWith(' ') || segment.EndsWith('.') ||
            segment.Any(character => character < ' ' || character is '<' or '>' or ':' or '"' or '|' or '?' or '*'))
        {
            return false;
        }

        string deviceName = segment.Split('.', 2)[0];
        return deviceName.ToUpperInvariant() is not ("CON" or "PRN" or "AUX" or "NUL" or
            "COM1" or "COM2" or "COM3" or "COM4" or "COM5" or "COM6" or "COM7" or "COM8" or "COM9" or
            "LPT1" or "LPT2" or "LPT3" or "LPT4" or "LPT5" or "LPT6" or "LPT7" or "LPT8" or "LPT9");
    }
}
