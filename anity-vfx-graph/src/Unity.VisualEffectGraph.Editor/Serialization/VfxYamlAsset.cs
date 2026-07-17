using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;

namespace UnityEditor.VFX.Serialization;

/// <summary>
/// Losslessly indexes the Unity YAML object stream used by Visual Effect Graph 14.x assets.
/// Full typed block/context deserialization is layered on this object registry.
/// </summary>
internal sealed class VfxYamlAsset
{
    private const long VisualEffectResourceClassId = 2058629511;
    private static readonly Regex DocumentHeader = new(
        @"^--- !u!(?<classId>-?[0-9]+) &(?<fileId>-?[0-9]+)[ \t]*\r?$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);
    private static readonly Regex RootType = new(
        @"^(?<type>[A-Za-z_][A-Za-z0-9_.]*):[ \t]*\r?$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);
    private static readonly Regex InlineReference = new(
        @"\{(?<body>[^{}\r\n]*)\}",
        RegexOptions.CultureInvariant);
    private static readonly Regex FileId = new(
        @"(?:^|,)\s*fileID\s*:\s*(?<fileId>-?[0-9]+)(?:\s*,|\s*$)",
        RegexOptions.CultureInvariant);

    private readonly ReadOnlyCollection<VfxYamlDocument> _documents;
    private readonly ReadOnlyDictionary<long, VfxYamlDocument> _documentsByFileId;

    private VfxYamlAsset(
        string sourceText,
        string preamble,
        List<VfxYamlDocument> documents,
        Dictionary<long, VfxYamlDocument> documentsByFileId)
    {
        SourceText = sourceText;
        Preamble = preamble;
        _documents = documents.AsReadOnly();
        _documentsByFileId = new ReadOnlyDictionary<long, VfxYamlDocument>(documentsByFileId);
        Resource = documents.FirstOrDefault(document =>
                       document.ClassId == VisualEffectResourceClassId &&
                       string.Equals(document.RootType, "VisualEffectResource", StringComparison.Ordinal))
                   ?? throw new InvalidDataException("VFX asset does not contain a VisualEffectResource document.");
    }

    internal string SourceText { get; }

    internal string Preamble { get; }

    internal IReadOnlyList<VfxYamlDocument> Documents => _documents;

    internal IReadOnlyDictionary<long, VfxYamlDocument> DocumentsByFileId => _documentsByFileId;

    internal VfxYamlDocument Resource { get; }

    internal static VfxYamlAsset Parse(string sourceText)
    {
        if (sourceText is null) throw new ArgumentNullException(nameof(sourceText));
        MatchCollection headers = DocumentHeader.Matches(sourceText);
        if (headers.Count == 0)
            throw new InvalidDataException("VFX asset does not contain any Unity YAML document headers.");

        var documents = new List<VfxYamlDocument>(headers.Count);
        var documentsByFileId = new Dictionary<long, VfxYamlDocument>();
        for (int index = 0; index < headers.Count; index++)
        {
            Match header = headers[index];
            int documentStart = header.Index;
            int documentEnd = index + 1 < headers.Count ? headers[index + 1].Index : sourceText.Length;
            string rawText = sourceText.Substring(documentStart, documentEnd - documentStart);
            long classId = ParseInt64(header.Groups["classId"].Value, "class id", documentStart);
            long fileId = ParseInt64(header.Groups["fileId"].Value, "file id", documentStart);
            string rootType = ReadRootType(rawText, header.Length);
            long[] localReferences = ReadLocalReferences(rawText);
            var document = new VfxYamlDocument(classId, fileId, rootType, rawText, localReferences);
            if (!documentsByFileId.TryAdd(fileId, document))
                throw new InvalidDataException($"Duplicate Unity YAML fileID '{fileId}'.");
            documents.Add(document);
        }

        string preamble = sourceText.Substring(0, headers[0].Index);
        return new VfxYamlAsset(sourceText, preamble, documents, documentsByFileId);
    }

    internal bool TryResolve(long fileId, out VfxYamlDocument? document)
        => _documentsByFileId.TryGetValue(fileId, out document);

    internal IReadOnlyList<long> GetUnresolvedLocalFileIds()
    {
        var references = new HashSet<long>();
        foreach (VfxYamlDocument document in _documents)
        {
            foreach (long fileId in document.LocalFileIds)
                if (fileId != 0) references.Add(fileId);
        }
        references.ExceptWith(_documentsByFileId.Keys);
        return references.OrderBy(value => value).ToArray();
    }

    private static long ParseInt64(string value, string description, int offset)
    {
        if (!long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long result))
            throw new InvalidDataException($"Invalid Unity YAML {description} at character {offset}.");
        return result;
    }

    private static string ReadRootType(string rawText, int headerLength)
    {
        int bodyStart = headerLength;
        while (bodyStart < rawText.Length && (rawText[bodyStart] == '\r' || rawText[bodyStart] == '\n')) bodyStart++;
        Match match = RootType.Match(rawText, bodyStart);
        if (!match.Success || match.Index != bodyStart)
            throw new InvalidDataException("Unity YAML document requires a root serialized type.");
        return match.Groups["type"].Value;
    }

    private static long[] ReadLocalReferences(string rawText)
    {
        var references = new HashSet<long>();
        foreach (Match inline in InlineReference.Matches(rawText))
        {
            string body = inline.Groups["body"].Value;
            if (body.IndexOf("guid:", StringComparison.Ordinal) >= 0) continue;
            Match fileId = FileId.Match(body);
            if (!fileId.Success) continue;
            references.Add(ParseInt64(fileId.Groups["fileId"].Value, "reference file id", inline.Index));
        }
        return references.OrderBy(value => value).ToArray();
    }
}

internal sealed class VfxYamlDocument
{
    internal VfxYamlDocument(
        long classId,
        long fileId,
        string rootType,
        string rawText,
        long[] localFileIds)
    {
        ClassId = classId;
        FileId = fileId;
        RootType = rootType;
        RawText = rawText;
        LocalFileIds = localFileIds;
    }

    internal long ClassId { get; }

    internal long FileId { get; }

    internal string RootType { get; }

    internal string RawText { get; }

    internal IReadOnlyList<long> LocalFileIds { get; }
}
