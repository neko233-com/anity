using System.Reflection;
using Anity.UnityApiParity;
using Xunit;

namespace Anity.UnityApiParity.Tests
{

public sealed class ApiParityTests
{
    private readonly ApiSurfaceReader _reader = new();
    private const string FixtureNamespace = "Anity.UnityApiParity.Tests.Fixtures";

    [Fact]
    public void Reader_IncludesPublicAndProtectedContractMembers()
    {
        ApiType type = ReadFixture<Fixtures.PublicFixture<Fixtures.ReferenceFixture>>();
        Assert.Contains(type.Members, member => member.Identity == "field|ProtectedValue");
        Assert.DoesNotContain(type.Members, member => member.Identity == "field|PrivateValue");
    }

    [Fact]
    public void Reader_ExcludesInternalTypes()
    {
        ApiSurface surface = _reader.ReadAssembly(typeof(ApiParityTests).Assembly, new[] { FixtureNamespace });
        Assert.DoesNotContain(surface.Types.Keys, name => name.Contains("InternalFixture", StringComparison.Ordinal));
    }

    [Fact]
    public void Reader_RecordsTypeKindBaseInterfacesAndGenericConstraints()
    {
        ApiType type = ReadFixture<Fixtures.PublicFixture<Fixtures.ReferenceFixture>>();
        Assert.Equal("class", type.Kind);
        Assert.Contains("base=System.Object", type.Fingerprint);
        Assert.Contains("System.IDisposable", type.Fingerprint);
        Assert.Contains("ReferenceTypeConstraint", type.Fingerprint);
    }

    [Fact]
    public void Reader_RecordsEnumValuesExactly()
    {
        ApiType type = ReadFixture<Fixtures.FixtureFlags>();
        ApiMember value = Assert.Single(type.Members.Where(member => member.Identity == "field|Second"));
        Assert.Contains("value=4", value.Fingerprint);
        Assert.Contains("System.FlagsAttribute", type.Fingerprint);
    }

    [Fact]
    public void Reader_DistinguishesMethodOverloads()
    {
        ApiType type = ReadFixture<Fixtures.PublicFixture<Fixtures.ReferenceFixture>>();
        string[] overloads = type.Members
            .Where(member => member.Identity.StartsWith("method|Overload|", StringComparison.Ordinal))
            .Select(member => member.Identity)
            .ToArray();
        Assert.Equal(2, overloads.Length);
        Assert.Equal(2, overloads.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Reader_DistinguishesConversionOperatorsByTargetType()
    {
        ApiType type = ReadFixture<Fixtures.ConversionFixture>();
        string[] conversions = type.Members
            .Where(member => member.Identity.StartsWith("method|op_Implicit|", StringComparison.Ordinal))
            .Select(member => member.Identity)
            .ToArray();
        Assert.Equal(2, conversions.Length);
        Assert.Contains(conversions, identity => identity.EndsWith("System.Int32", StringComparison.Ordinal));
        Assert.Contains(conversions, identity => identity.EndsWith("System.String", StringComparison.Ordinal));
    }

    [Fact]
    public void Reader_RecordsParameterNamesDefaultsAndParams()
    {
        ApiType type = ReadFixture<Fixtures.PublicFixture<Fixtures.ReferenceFixture>>();
        ApiMember method = Assert.Single(type.Members.Where(member => member.Identity.StartsWith("method|Configure|", StringComparison.Ordinal)));
        Assert.Contains("count optional=3", method.Fingerprint);
        Assert.Contains("params System.String[] names", method.Fingerprint);
    }

    [Fact]
    public void Reader_RecordsRefOutAndInModifiers()
    {
        ApiType type = ReadFixture<Fixtures.PublicFixture<Fixtures.ReferenceFixture>>();
        ApiMember method = Assert.Single(type.Members.Where(member => member.Identity.StartsWith("method|Mutate|", StringComparison.Ordinal)));
        Assert.Contains("ref System.Int32", method.Fingerprint);
        Assert.Contains("out System.String", method.Fingerprint);
        Assert.Contains("in System.Double", method.Fingerprint);
    }

    [Fact]
    public void Reader_RecordsPropertyAccessorVisibilityAndIndexer()
    {
        ApiType type = ReadFixture<Fixtures.PublicFixture<Fixtures.ReferenceFixture>>();
        ApiMember value = Assert.Single(type.Members.Where(member => member.Identity == "property|Value|"));
        Assert.Contains("get=public", value.Fingerprint);
        Assert.Contains("set=none", value.Fingerprint);
        Assert.Contains(type.Members, member => member.Identity.Contains("property|Item|System.Int32", StringComparison.Ordinal));
    }

    [Fact]
    public void Reader_RecordsEventsAndAttributes()
    {
        ApiType type = ReadFixture<Fixtures.PublicFixture<Fixtures.ReferenceFixture>>();
        Assert.Contains("System.ObsoleteAttribute", type.Fingerprint);
        ApiMember changed = Assert.Single(type.Members.Where(member => member.Identity == "event|Changed"));
        Assert.Contains("System.EventHandler", changed.Fingerprint);
        Assert.Contains("add=public", changed.Fingerprint);
    }

    [Fact]
    public void Comparer_ReportsMissingType()
    {
        ApiParityReport report = Compare(
            Surface(Type("UnityEngine.Camera", members: new[] { Member("property|depth|", "float") })),
            Surface());
        AssertDifference(report, ApiDifferenceKind.MissingType, "UnityEngine.Camera");
        Assert.Equal(0, report.Summary.MatchedTypes);
        Assert.Equal(1, report.Summary.MembersInMissingTypes);
        Assert.Equal(1, report.Summary.MissingMembers);
        Assert.Equal(0, report.Summary.MemberCoveragePercent);
    }

    [Fact]
    public void Comparer_ReportsExtraTypeSeparately()
    {
        ApiParityReport report = Compare(Surface(), Surface(Type("UnityEngine.AnityExtension")));
        AssertDifference(report, ApiDifferenceKind.ExtraType, "UnityEngine.AnityExtension");
        Assert.Equal(100, report.Summary.TypeCoveragePercent);
    }

    [Fact]
    public void Comparer_ReportsTypeFingerprintMismatch()
    {
        ApiParityReport report = Compare(
            Surface(Type("UnityEngine.Camera", fingerprint: "class")),
            Surface(Type("UnityEngine.Camera", fingerprint: "struct")));
        AssertDifference(report, ApiDifferenceKind.TypeMismatch, "UnityEngine.Camera");
    }

    [Fact]
    public void Comparer_ReportsMissingAndExtraMembers()
    {
        ApiType expected = Type("UnityEngine.Camera", members: new[] { Member("property|depth|", "float") });
        ApiType actual = Type("UnityEngine.Camera", members: new[] { Member("property|enabled|", "bool") });
        ApiParityReport report = Compare(Surface(expected), Surface(actual));
        AssertDifference(report, ApiDifferenceKind.MissingMember, "UnityEngine.Camera", "property|depth|");
        AssertDifference(report, ApiDifferenceKind.ExtraMember, "UnityEngine.Camera", "property|enabled|");
    }

    [Fact]
    public void Comparer_ReportsMemberFingerprintMismatch()
    {
        ApiType expected = Type("UnityEngine.Camera", members: new[] { Member("property|depth|", "System.Single") });
        ApiType actual = Type("UnityEngine.Camera", members: new[] { Member("property|depth|", "System.Int32") });
        ApiParityReport report = Compare(Surface(expected), Surface(actual));
        AssertDifference(report, ApiDifferenceKind.MemberMismatch, "UnityEngine.Camera", "property|depth|");
    }

    [Fact]
    public void Comparer_ExactSurface_HasNoDifferencesAndFullCoverage()
    {
        ApiType type = Type("UnityEngine.Camera", members: new[] { Member("property|depth|", "System.Single") });
        ApiParityReport report = Compare(Surface(type), Surface(type));
        Assert.Empty(report.Differences);
        Assert.Equal(100, report.Summary.TypeCoveragePercent);
        Assert.Equal(100, report.Summary.MemberCoveragePercent);
    }

    [Fact]
    public void Comparer_PropagatesLoadIssuesIntoEvidence()
    {
        var issue = new ApiLoadIssue("UnityEngine.CoreModule", "dependency missing");
        ApiParityReport report = Compare(new ApiSurface(new Dictionary<string, ApiType>(), new[] { issue }), Surface());
        Assert.Same(issue, Assert.Single(report.LoadIssues));
    }

    private ApiType ReadFixture<T>()
    {
        string definitionName = typeof(T).IsGenericType
            ? typeof(T).GetGenericTypeDefinition().FullName!
            : typeof(T).FullName!;
        ApiSurface surface = _reader.ReadAssembly(typeof(T).Assembly, new[] { FixtureNamespace });
        return surface.Types[definitionName];
    }

    private static ApiMember Member(string identity, string fingerprint)
        => new(identity, identity.Split('|')[0], fingerprint, identity);

    private static ApiType Type(string name, string fingerprint = "class", IReadOnlyList<ApiMember>? members = null)
        => new(name, "Fixture", "class", fingerprint, members ?? Array.Empty<ApiMember>());

    private static ApiSurface Surface(params ApiType[] types)
        => new(types.ToDictionary(type => type.Name, StringComparer.Ordinal));

    private static ApiParityReport Compare(ApiSurface expected, ApiSurface actual)
        => new ApiSurfaceComparer().Compare(expected, actual, "Unity", "unity", "anity");

    private static void AssertDifference(ApiParityReport report, ApiDifferenceKind kind, string type, string? member = null)
        => Assert.Contains(report.Differences, difference => difference.Kind == kind && difference.Type == type && difference.Member == member);
}
}

namespace Anity.UnityApiParity.Tests.Fixtures
{
    [Flags]
    public enum FixtureFlags
    {
        None = 0,
        First = 1,
        Second = 4
    }

    public sealed class ReferenceFixture { }

    public readonly struct ConversionFixture
    {
        public static implicit operator int(ConversionFixture value) => 0;
        public static implicit operator string(ConversionFixture value) => string.Empty;
    }

    [Obsolete("fixture")]
    public abstract class PublicFixture<T> : IDisposable where T : class, new()
    {
        private int PrivateValue;
        protected int ProtectedValue;
        public const int Constant = 7;
        public T? Value { get; private set; }
        public string this[int index] => index.ToString();
        public event EventHandler? Changed;

        public void Overload(int value) => PrivateValue = value;
        public void Overload(string value) => Value = new T();
        public void Configure(int count = 3, params string[] names) => ProtectedValue = count + names.Length;
        public void Mutate(ref int value, out string text, in double factor)
        {
            value += (int)factor;
            text = value.ToString();
        }

        protected virtual void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
        public void Dispose() { }
    }

    internal sealed class InternalFixture { }
}
