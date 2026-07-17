using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace Anity.UnityApiParity;

public sealed record ApiMember(
    string Identity,
    string Kind,
    string Fingerprint,
    string Display);

public sealed record ApiType(
    string Name,
    string Assembly,
    string Kind,
    string Fingerprint,
    IReadOnlyList<ApiMember> Members);

public sealed record ApiLoadIssue(string Assembly, string Message);

public sealed class ApiSurface
{
    public IReadOnlyDictionary<string, ApiType> Types { get; }
    public IReadOnlyList<ApiLoadIssue> LoadIssues { get; }

    public ApiSurface(
        IReadOnlyDictionary<string, ApiType> types,
        IReadOnlyList<ApiLoadIssue>? loadIssues = null)
    {
        Types = types ?? throw new ArgumentNullException(nameof(types));
        LoadIssues = loadIssues ?? Array.Empty<ApiLoadIssue>();
    }

    public int MemberCount => Types.Values.Sum(type => type.Members.Count);
}

[JsonConverter(typeof(JsonStringEnumConverter<ApiDifferenceKind>))]
public enum ApiDifferenceKind
{
    MissingType,
    ExtraType,
    TypeMismatch,
    MissingMember,
    ExtraMember,
    MemberMismatch
}

public sealed record ApiDifference(
    ApiDifferenceKind Kind,
    string Type,
    string? Member,
    string Expected,
    string Actual)
{
    public string ExpectedAssembly { get; init; } = string.Empty;
    public string ActualAssembly { get; init; } = string.Empty;
    public string StableId => $"{Kind}|{Type}|{Member ?? string.Empty}";
    public string EvidenceId => $"{StableId}|{Hash(Expected)}|{Hash(Actual)}";

    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).Substring(0, 16);
}

public sealed class ApiParitySummary
{
    public int UnityTypes { get; init; }
    public int AnityTypes { get; init; }
    public int MatchedTypes { get; init; }
    public int ExactTypes { get; init; }
    public int UnityMembers { get; init; }
    public int AnityMembers { get; init; }
    public int MatchedMembers { get; init; }
    public int ExactMembers { get; init; }
    public int MissingTypes { get; init; }
    public int ExtraTypes { get; init; }
    public int TypeMismatches { get; init; }
    public int MissingMembers { get; init; }
    public int MissingMembersOnPresentTypes { get; init; }
    public int MembersInMissingTypes { get; init; }
    public int ExtraMembers { get; init; }
    public int MemberMismatches { get; init; }
    public double TypeCoveragePercent { get; init; }
    public double TypeExactPercent { get; init; }
    public double MemberCoveragePercent { get; init; }
    public double MemberExactPercent { get; init; }
}

public sealed class ApiParityReport
{
    public string UnityLabel { get; init; } = string.Empty;
    public string UnitySource { get; init; } = string.Empty;
    public string AnitySource { get; init; } = string.Empty;
    public DateTime GeneratedUtc { get; init; }
    public ApiParitySummary Summary { get; init; } = new();
    public IReadOnlyList<ApiLoadIssue> LoadIssues { get; init; } = Array.Empty<ApiLoadIssue>();
    public IReadOnlyList<ApiDifference> Differences { get; init; } = Array.Empty<ApiDifference>();
}

public sealed class ApiParityBaseline
{
    public int SchemaVersion { get; init; } = 1;
    public string UnityLabel { get; init; } = string.Empty;
    public DateTime GeneratedUtc { get; init; }
    public ApiParitySummary Summary { get; init; } = new();
    public IReadOnlyList<string> DifferenceEvidenceIds { get; init; } = Array.Empty<string>();
}
