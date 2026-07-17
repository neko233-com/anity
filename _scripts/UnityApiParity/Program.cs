using System.Text.Json;
using System.Text.Json.Serialization;
using Anity.UnityApiParity;

return await ProgramEntry.RunAsync(args);

public static class ProgramEntry
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter<ApiDifferenceKind>() }
    };

    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            Options options = Options.Parse(args);
            if (options.Help)
            {
                Console.WriteLine(Options.HelpText);
                return 0;
            }

            string[] unityAssemblies = Directory.EnumerateFiles(options.UnityDirectory, "*.dll", SearchOption.TopDirectoryOnly)
                .Where(path =>
                {
                    string name = Path.GetFileName(path);
                    return name.StartsWith("UnityEngine", StringComparison.Ordinal)
                           || name.StartsWith("UnityEditor", StringComparison.Ordinal);
                })
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            if (unityAssemblies.Length == 0)
                throw new InvalidOperationException("No UnityEngine/UnityEditor assemblies were found in " + options.UnityDirectory);

            var reader = new ApiSurfaceReader();
            Console.WriteLine($"Reading {unityAssemblies.Length} official Unity assemblies...");
            ApiSurface unity = reader.ReadAssemblyFiles(unityAssemblies, options.NamespacePrefixes);
            Console.WriteLine("Reading Anity assembly...");
            ApiSurface anity = reader.ReadAssemblyFiles(new[] { options.AnityAssembly }, options.NamespacePrefixes);

            foreach (string typeName in options.InspectTypes)
            {
                PrintType("Unity", unity, typeName);
                PrintType("Anity", anity, typeName);
            }

            ApiParityReport report = new ApiSurfaceComparer().Compare(
                unity,
                anity,
                options.UnityLabel,
                Path.GetFullPath(options.UnityDirectory),
                Path.GetFullPath(options.AnityAssembly));

            PrintSummary(report, options.MaxDetails);
            bool hasRegression = false;
            if (!string.IsNullOrWhiteSpace(options.BaselinePath))
            {
                ApiParityBaseline baseline = JsonSerializer.Deserialize<ApiParityBaseline>(
                    await File.ReadAllTextAsync(options.BaselinePath), JsonOptions)
                    ?? throw new InvalidDataException("Parity baseline is empty.");
                if (baseline.SchemaVersion != 1)
                    throw new InvalidDataException("Unsupported parity baseline schema: " + baseline.SchemaVersion);
                if (!string.Equals(baseline.UnityLabel, report.UnityLabel, StringComparison.Ordinal))
                    throw new InvalidDataException($"Parity baseline targets '{baseline.UnityLabel}', current audit targets '{report.UnityLabel}'. Regenerate intentionally for a Unity version change.");

                var previous = baseline.DifferenceEvidenceIds.ToHashSet(StringComparer.Ordinal);
                var current = report.Differences.Select(difference => difference.EvidenceId).ToHashSet(StringComparer.Ordinal);
                string[] regressions = current.Except(previous).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                string[] improvements = previous.Except(current).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                hasRegression = regressions.Length > 0;
                Console.WriteLine($"Baseline: regressions={regressions.Length}, removed-or-changed={improvements.Length}");
                foreach (string regression in regressions.Take(options.MaxDetails)) Console.WriteLine("  REGRESSION " + regression);
            }

            if (!string.IsNullOrWhiteSpace(options.WriteBaselinePath))
            {
                var baseline = new ApiParityBaseline
                {
                    UnityLabel = report.UnityLabel,
                    GeneratedUtc = DateTime.UtcNow,
                    Summary = report.Summary,
                    DifferenceEvidenceIds = report.Differences.Select(difference => difference.EvidenceId).OrderBy(value => value, StringComparer.Ordinal).ToArray()
                };
                string path = Path.GetFullPath(options.WriteBaselinePath);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(path, JsonSerializer.Serialize(baseline, JsonOptions));
                Console.WriteLine("Baseline written: " + path);
            }

            if (!string.IsNullOrWhiteSpace(options.JsonOutput))
            {
                string output = Path.GetFullPath(options.JsonOutput);
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                await File.WriteAllTextAsync(output, JsonSerializer.Serialize(report, JsonOptions));
                Console.WriteLine("JSON report: " + output);
            }

            bool hasContractGap = report.Differences.Any(difference => difference.Kind is
                ApiDifferenceKind.MissingType or ApiDifferenceKind.TypeMismatch
                or ApiDifferenceKind.MissingMember or ApiDifferenceKind.MemberMismatch);
            if (options.FailOnRegression && (hasRegression || report.LoadIssues.Count > 0)) return 1;
            return options.FailOnDifference && (hasContractGap || report.LoadIssues.Count > 0) ? 1 : 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("unity-api-parity: " + exception.Message);
            return 2;
        }
    }

    private static void PrintSummary(ApiParityReport report, int maxDetails)
    {
        ApiParitySummary summary = report.Summary;
        Console.WriteLine($"Unity API parity: {report.UnityLabel}");
        Console.WriteLine($"Types: present={summary.MatchedTypes}/{summary.UnityTypes} ({summary.TypeCoveragePercent:0.###}%); exact={summary.ExactTypes} ({summary.TypeExactPercent:0.###}%); missing={summary.MissingTypes}, mismatch={summary.TypeMismatches}, extra={summary.ExtraTypes}");
        Console.WriteLine($"Members: present={summary.MatchedMembers}/{summary.UnityMembers} ({summary.MemberCoveragePercent:0.###}%); exact={summary.ExactMembers} ({summary.MemberExactPercent:0.###}%); missing={summary.MissingMembers} (missing types={summary.MembersInMissingTypes}, present types={summary.MissingMembersOnPresentTypes}), mismatch={summary.MemberMismatches}, extra={summary.ExtraMembers}");
        Console.WriteLine($"Load issues: {report.LoadIssues.Count}");
        foreach (ApiLoadIssue issue in report.LoadIssues.Take(Math.Min(maxDetails, 10)))
            Console.WriteLine($"  LOAD {issue.Assembly}: {issue.Message}");
        foreach (ApiDifference difference in report.Differences.Take(maxDetails))
            Console.WriteLine($"  {difference.Kind}: {difference.Type}{(difference.Member == null ? "" : " :: " + difference.Member)}");
        if (report.Differences.Count > maxDetails)
            Console.WriteLine($"  ... {report.Differences.Count - maxDetails} more differences in JSON report");
    }

    private static void PrintType(string label, ApiSurface surface, string typeName)
    {
        Console.WriteLine($"INSPECT {label} {typeName}");
        if (!surface.Types.TryGetValue(typeName, out ApiType? type))
        {
            Console.WriteLine("  missing");
            return;
        }
        Console.WriteLine($"  assembly={type.Assembly}");
        Console.WriteLine($"  {type.Fingerprint}");
        foreach (ApiMember member in type.Members)
        {
            Console.WriteLine($"  {member.Identity}");
            Console.WriteLine($"    {member.Fingerprint}");
        }
    }

    private sealed class Options
    {
        public string UnityDirectory { get; private set; } = string.Empty;
        public string AnityAssembly { get; private set; } = string.Empty;
        public string UnityLabel { get; private set; } = "Unity 2022.3";
        public string? JsonOutput { get; private set; }
        public string? BaselinePath { get; private set; }
        public string? WriteBaselinePath { get; private set; }
        public bool FailOnDifference { get; private set; }
        public bool FailOnRegression { get; private set; }
        public bool Help { get; private set; }
        public int MaxDetails { get; private set; } = 30;
        public List<string> NamespacePrefixes { get; } = new();
        public List<string> InspectTypes { get; } = new();

        public static Options Parse(string[] args)
        {
            var options = new Options();
            for (int index = 0; index < args.Length; index++)
            {
                string argument = args[index];
                string Next()
                {
                    if (++index >= args.Length) throw new ArgumentException("Missing value for " + argument);
                    return args[index];
                }

                switch (argument)
                {
                    case "--unity-dir": options.UnityDirectory = Next(); break;
                    case "--anity": options.AnityAssembly = Next(); break;
                    case "--unity-label": options.UnityLabel = Next(); break;
                    case "--json": options.JsonOutput = Next(); break;
                    case "--baseline": options.BaselinePath = Next(); break;
                    case "--write-baseline": options.WriteBaselinePath = Next(); break;
                    case "--namespace": options.NamespacePrefixes.Add(Next()); break;
                    case "--inspect-type": options.InspectTypes.Add(Next()); break;
                    case "--max-details":
                        if (!int.TryParse(Next(), out int count) || count < 0 || count > 1000)
                            throw new ArgumentOutOfRangeException(nameof(args), "--max-details must be between 0 and 1000.");
                        options.MaxDetails = count;
                        break;
                    case "--fail-on-difference": options.FailOnDifference = true; break;
                    case "--fail-on-regression": options.FailOnRegression = true; break;
                    case "--help":
                    case "-h": options.Help = true; break;
                    default: throw new ArgumentException("Unknown argument: " + argument);
                }
            }

            if (options.Help) return options;
            if (string.IsNullOrWhiteSpace(options.UnityDirectory)) throw new ArgumentException("--unity-dir is required.");
            if (string.IsNullOrWhiteSpace(options.AnityAssembly)) throw new ArgumentException("--anity is required.");
            options.UnityDirectory = Path.GetFullPath(options.UnityDirectory);
            options.AnityAssembly = Path.GetFullPath(options.AnityAssembly);
            if (!Directory.Exists(options.UnityDirectory)) throw new DirectoryNotFoundException(options.UnityDirectory);
            if (!File.Exists(options.AnityAssembly)) throw new FileNotFoundException("Anity assembly not found.", options.AnityAssembly);
            if (options.FailOnRegression && string.IsNullOrWhiteSpace(options.BaselinePath))
                throw new ArgumentException("--fail-on-regression requires --baseline.");
            if (!string.IsNullOrWhiteSpace(options.BaselinePath))
            {
                options.BaselinePath = Path.GetFullPath(options.BaselinePath);
                if (!File.Exists(options.BaselinePath)) throw new FileNotFoundException("Parity baseline not found.", options.BaselinePath);
            }
            if (!string.IsNullOrWhiteSpace(options.WriteBaselinePath))
                options.WriteBaselinePath = Path.GetFullPath(options.WriteBaselinePath);
            if (options.NamespacePrefixes.Count == 0)
            {
                options.NamespacePrefixes.Add("UnityEngine");
                options.NamespacePrefixes.Add("UnityEditor");
            }
            return options;
        }

        public const string HelpText = """
Anity Unity 2022 API parity auditor

Required:
  --unity-dir <dir>       Directory containing official UnityEngine*.dll/UnityEditor*.dll
  --anity <dll>           Built Anity.Core.dll

Options:
  --unity-label <text>    Version label written to the report
  --namespace <prefix>    Namespace prefix; repeatable (default UnityEngine + UnityEditor)
  --inspect-type <name>   Print exact official/Anity type and member fingerprints
  --json <path>           Write complete machine-readable evidence
  --baseline <path>       Compare against a reviewed evidence baseline
  --write-baseline <path> Write a compact hash baseline for future regression gates
  --max-details <n>       Console difference limit (default 30)
  --fail-on-difference    Exit 1 for missing/mismatched official API or load issues
  --fail-on-regression    Exit 1 for new/changed differences relative to --baseline
""";
    }
}
