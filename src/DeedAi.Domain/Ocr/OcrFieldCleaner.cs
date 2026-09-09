namespace DeedAi.Domain.Ocr;

public static class OcrFieldCleaner
{
    public static string? Clean(
        string? value,
        IEnumerable<string> trimTokens,
        IEnumerable<string> discardWords)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trim = trimTokens
            .Where(token => !string.IsNullOrEmpty(token))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var discard = new HashSet<string>(
            discardWords.Where(word => !string.IsNullOrWhiteSpace(word)),
            StringComparer.OrdinalIgnoreCase);

        var text = value.Trim();
        var changed = true;
        while (changed && text.Length > 0)
        {
            changed = false;
            foreach (var token in trim)
            {
                if (text.StartsWith(token, StringComparison.Ordinal))
                {
                    text = text[token.Length..].TrimStart();
                    changed = true;
                }

                if (text.Length > 0 && text.EndsWith(token, StringComparison.Ordinal))
                {
                    text = text[..^token.Length].TrimEnd();
                    changed = true;
                }
            }

            var next = text.Trim();
            if (next != text)
            {
                text = next;
                changed = true;
            }
        }

        if (text.Length == 0 || discard.Contains(text))
        {
            return null;
        }

        var kept = text
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => !discard.Contains(part))
            .ToArray();
        return kept.Length == 0 ? null : string.Join(' ', kept);
    }
}
