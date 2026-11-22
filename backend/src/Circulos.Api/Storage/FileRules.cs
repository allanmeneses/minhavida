namespace Circulos.Api.Storage;

public static class FileRules
{
    public const long MaxIncomingBytes = 80L * 1024 * 1024;

    private static readonly Dictionary<string, string[]> DocumentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = ["application/pdf"],
        [".txt"] = ["text/plain"],
        [".md"] = ["text/markdown", "text/plain"],
        [".csv"] = ["text/csv", "text/plain", "application/vnd.ms-excel"],
        [".jpg"] = ["image/jpeg"],
        [".jpeg"] = ["image/jpeg"],
        [".png"] = ["image/png"],
        [".webp"] = ["image/webp"],
        [".gif"] = ["image/gif"],
        [".doc"] = ["application/msword"],
        [".docx"] = ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"],
        [".xls"] = ["application/vnd.ms-excel"],
        [".xlsx"] = ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"],
        [".zip"] = ["application/zip", "application/x-zip-compressed"],
    };

    private static readonly Dictionary<string, string[]> PhotoTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = ["image/jpeg"],
        [".jpeg"] = ["image/jpeg"],
        [".png"] = ["image/png"],
        [".webp"] = ["image/webp"],
    };

    public static string? ValidateDocument(string fileName, string? contentType, long size)
    {
        if (size <= 0)
        {
            return "O arquivo esta vazio.";
        }

        if (size > MaxIncomingBytes)
        {
            return "O arquivo e grande demais para enviar de uma vez.";
        }

        return Validate(fileName, contentType, DocumentTypes, "Este tipo de arquivo nao e aceito nos documentos pessoais.");
    }

    public static string? ValidatePhoto(string fileName, string? contentType, long size)
    {
        if (size <= 0)
        {
            return "A foto esta vazia.";
        }

        if (size > MaxIncomingBytes)
        {
            return "A foto e grande demais para enviar de uma vez.";
        }

        return Validate(fileName, contentType, PhotoTypes, "A foto precisa ser JPG, PNG ou WebP.");
    }

    public static string CanonicalContentType(string fileName, string? contentType)
    {
        var ext = Path.GetExtension(fileName);
        if (DocumentTypes.TryGetValue(ext, out var types) || PhotoTypes.TryGetValue(ext, out types))
        {
            if (!string.IsNullOrWhiteSpace(contentType)
                && types.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            {
                return contentType;
            }

            return types[0];
        }

        return string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;
    }

    public static string SafeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return "arquivo";
        }

        name = name.Replace("\"", string.Empty).Replace("\n", string.Empty).Replace("\r", string.Empty);
        return name.Length > 255 ? name[..255] : name;
    }

    private static string? Validate(
        string fileName,
        string? contentType,
        Dictionary<string, string[]> allowed,
        string invalidTypeMessage)
    {
        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(ext) || !allowed.TryGetValue(ext, out var types))
        {
            return invalidTypeMessage;
        }

        if (string.IsNullOrWhiteSpace(contentType)
            || contentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return types.Contains(contentType, StringComparer.OrdinalIgnoreCase) ? null : invalidTypeMessage;
    }
}
