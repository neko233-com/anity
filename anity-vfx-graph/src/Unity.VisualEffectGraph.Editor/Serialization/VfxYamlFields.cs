using System.Globalization;
using System.Text.RegularExpressions;

namespace UnityEditor.VFX.Serialization;

internal static class VfxYamlFields
{
    private static readonly Regex ScriptReference = new(
        @"^  m_Script:\s*\{[^}\r\n]*\bguid:\s*(?<guid>[0-9A-Fa-f-]+)[^}\r\n]*\}\s*\r?$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);
    private static readonly Regex TopLevelField = new(
        @"^  (?<name>[A-Za-z_][A-Za-z0-9_]*):(?<inline>[^\r\n]*)\r?$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);
    private static readonly Regex ListReference = new(
        @"^  -\s*\{\s*fileID:\s*(?<fileId>-?[0-9]+)(?:\s*,[^}]*)?\}\s*\r?$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);
    private static readonly Regex FlowSlot = new(
        @"^  -\s*link:(?<empty>\s*\[\])?\s*\r?$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);
    private static readonly Regex FlowLink = new(
        @"^    -\s*context:\s*\{\s*fileID:\s*(?<context>-?[0-9]+)(?:\s*,[^}]*)?\}\s*\r?\n" +
        @"      slotIndex:\s*(?<slotIndex>-?[0-9]+)\s*\r?$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);

    internal static string ReadRequiredScriptGuid(string rawText)
    {
        Match match = ScriptReference.Match(rawText);
        if (!match.Success)
            throw new InvalidDataException("VFX MonoBehaviour is missing its m_Script GUID reference.");
        return match.Groups["guid"].Value;
    }

    internal static string? ReadString(string rawText, string fieldName)
    {
        FieldSlice? field = FindTopLevelField(rawText, fieldName);
        return field?.Inline.TrimStart();
    }

    internal static long ReadReference(string rawText, string fieldName, long defaultValue = 0)
    {
        FieldSlice? field = FindTopLevelField(rawText, fieldName);
        if (field is null) return defaultValue;
        Match match = Regex.Match(
            field.Value.Inline,
            @"^\s*\{\s*fileID:\s*(?<fileId>-?[0-9]+)(?:\s*,[^}]*)?\}\s*$",
            RegexOptions.CultureInvariant);
        if (!match.Success)
            throw new InvalidDataException("VFX field '" + fieldName + "' must be an inline fileID reference.");
        return ParseInt64(match.Groups["fileId"].Value, fieldName);
    }

    internal static long ReadNestedReference(
        string rawText,
        string parentFieldName,
        string nestedFieldName,
        long defaultValue = 0)
    {
        FieldSlice? parent = FindTopLevelField(rawText, parentFieldName);
        if (parent is null) return defaultValue;
        Match match = Regex.Match(
            parent.Value.Block,
            @"^    " + Regex.Escape(nestedFieldName) +
            @":\s*\{\s*fileID:\s*(?<fileId>-?[0-9]+)(?:\s*,[^}]*)?\}\s*\r?$",
            RegexOptions.Multiline | RegexOptions.CultureInvariant);
        if (!match.Success) return defaultValue;
        return ParseInt64(match.Groups["fileId"].Value, nestedFieldName);
    }

    internal static int? ReadInt32(string rawText, string fieldName)
    {
        FieldSlice? field = FindTopLevelField(rawText, fieldName);
        if (field is null) return null;
        string value = field.Value.Inline.Trim();
        if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int result))
            throw new InvalidDataException("VFX field '" + fieldName + "' must be a 32-bit integer.");
        return result;
    }

    internal static string? ReadDescendantScalar(
        string rawText,
        string parentFieldName,
        string descendantFieldName)
    {
        FieldSlice? parent = FindTopLevelField(rawText, parentFieldName);
        if (parent is null) return null;
        MatchCollection matches = Regex.Matches(
            parent.Value.Block,
            @"^ {4,}" + Regex.Escape(descendantFieldName) + @":(?<value>[^\r\n]*)\r?$",
            RegexOptions.Multiline | RegexOptions.CultureInvariant);
        if (matches.Count == 0) return null;
        if (matches.Count != 1)
            throw new InvalidDataException("VFX field '" + parentFieldName + "' contains duplicate '" + descendantFieldName + "' values.");
        return matches[0].Groups["value"].Value.TrimStart();
    }

    internal static string? ReadDescendantFoldedScalar(
        string rawText,
        string parentFieldName,
        string descendantFieldName)
    {
        FieldSlice? parent = FindTopLevelField(rawText, parentFieldName);
        if (parent is null) return null;
        Match match = Regex.Match(
            parent.Value.Block,
            @"^ {4,}" + Regex.Escape(descendantFieldName) +
            @":(?<first>[^\r\n]*)(?<continuations>(?:\r?\n {6,}[^\r\n]*)*)",
            RegexOptions.Multiline | RegexOptions.CultureInvariant);
        if (!match.Success) return null;
        string[] parts = (match.Groups["first"].Value + match.Groups["continuations"].Value)
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim())
            .Where(part => part.Length != 0)
            .ToArray();
        return string.Join(" ", parts);
    }

    internal static IReadOnlyList<long> ReadReferenceList(string rawText, string fieldName)
    {
        FieldSlice? field = FindTopLevelField(rawText, fieldName);
        if (field is null) return Array.Empty<long>();
        if (string.Equals(field.Value.Inline.Trim(), "[]", StringComparison.Ordinal)) return Array.Empty<long>();
        if (field.Value.Inline.Trim().Length != 0)
            throw new InvalidDataException("VFX field '" + fieldName + "' must be a block fileID list or [].");

        var references = new List<long>();
        foreach (Match match in ListReference.Matches(field.Value.Block))
            references.Add(ParseInt64(match.Groups["fileId"].Value, fieldName));
        int serializedEntryCount = Regex.Matches(
            field.Value.Block,
            @"^  -",
            RegexOptions.Multiline | RegexOptions.CultureInvariant).Count;
        if (serializedEntryCount != references.Count)
            throw new InvalidDataException("VFX field '" + fieldName + "' contains a malformed fileID list entry.");
        return references.AsReadOnly();
    }

    internal static IReadOnlyList<IReadOnlyList<VfxSerializedFlowLink>> ReadFlowSlots(
        string rawText,
        string fieldName)
    {
        FieldSlice? field = FindTopLevelField(rawText, fieldName);
        if (field is null) return Array.Empty<IReadOnlyList<VfxSerializedFlowLink>>();
        if (string.Equals(field.Value.Inline.Trim(), "[]", StringComparison.Ordinal))
            return Array.Empty<IReadOnlyList<VfxSerializedFlowLink>>();
        if (field.Value.Inline.Trim().Length != 0)
            throw new InvalidDataException("VFX flow field '" + fieldName + "' must be a block list or [].");

        MatchCollection slots = FlowSlot.Matches(field.Value.Block);
        if (slots.Count == 0 && field.Value.Block.Trim().Length != 0)
            throw new InvalidDataException("VFX flow field '" + fieldName + "' contains no valid flow slots.");
        var result = new List<IReadOnlyList<VfxSerializedFlowLink>>(slots.Count);
        for (int index = 0; index < slots.Count; index++)
        {
            int start = slots[index].Index + slots[index].Length;
            int end = index + 1 < slots.Count ? slots[index + 1].Index : field.Value.Block.Length;
            string slotBody = field.Value.Block.Substring(start, end - start);
            var links = new List<VfxSerializedFlowLink>();
            foreach (Match link in FlowLink.Matches(slotBody))
            {
                links.Add(new VfxSerializedFlowLink(
                    ParseInt64(link.Groups["context"].Value, fieldName + ".context"),
                    ParseInt32(link.Groups["slotIndex"].Value, fieldName + ".slotIndex")));
            }
            int serializedLinkCount = Regex.Matches(
                slotBody,
                @"^    -\s*context:",
                RegexOptions.Multiline | RegexOptions.CultureInvariant).Count;
            if (serializedLinkCount != links.Count ||
                (slots[index].Groups["empty"].Success && serializedLinkCount != 0) ||
                (!slots[index].Groups["empty"].Success && serializedLinkCount == 0))
                throw new InvalidDataException("VFX flow field '" + fieldName + "' contains a malformed link entry.");
            result.Add(links.AsReadOnly());
        }
        return result.AsReadOnly();
    }

    private static FieldSlice? FindTopLevelField(string rawText, string fieldName)
    {
        MatchCollection fields = TopLevelField.Matches(rawText);
        for (int index = 0; index < fields.Count; index++)
        {
            Match field = fields[index];
            if (!string.Equals(field.Groups["name"].Value, fieldName, StringComparison.Ordinal)) continue;
            int blockStart = field.Index + field.Length;
            while (blockStart < rawText.Length && (rawText[blockStart] == '\r' || rawText[blockStart] == '\n'))
                blockStart++;
            int blockEnd = index + 1 < fields.Count ? fields[index + 1].Index : rawText.Length;
            return new FieldSlice(
                field.Groups["inline"].Value,
                rawText.Substring(blockStart, blockEnd - blockStart));
        }
        return null;
    }

    private static long ParseInt64(string value, string fieldName)
    {
        if (!long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long result))
            throw new InvalidDataException("VFX field '" + fieldName + "' contains an invalid 64-bit integer.");
        return result;
    }

    private static int ParseInt32(string value, string fieldName)
    {
        if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int result))
            throw new InvalidDataException("VFX field '" + fieldName + "' contains an invalid 32-bit integer.");
        return result;
    }

    private readonly struct FieldSlice
    {
        internal FieldSlice(string inline, string block)
        {
            Inline = inline;
            Block = block;
        }

        internal string Inline { get; }

        internal string Block { get; }
    }
}

internal readonly struct VfxSerializedFlowLink
{
    internal VfxSerializedFlowLink(long contextFileId, int slotIndex)
    {
        ContextFileId = contextFileId;
        SlotIndex = slotIndex;
    }

    internal long ContextFileId { get; }

    internal int SlotIndex { get; }
}
