namespace Anity.UnityApiParity;

public sealed class ApiSurfaceComparer
{
    public ApiParityReport Compare(
        ApiSurface unity,
        ApiSurface anity,
        string unityLabel,
        string unitySource,
        string anitySource)
    {
        if (unity == null) throw new ArgumentNullException(nameof(unity));
        if (anity == null) throw new ArgumentNullException(nameof(anity));

        var differences = new List<ApiDifference>();
        int matchedTypes = 0;
        int matchedMembers = 0;
        int membersInMissingTypes = 0;

        foreach ((string typeName, ApiType expectedType) in unity.Types.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!anity.Types.TryGetValue(typeName, out ApiType? actualType))
            {
                differences.Add(new ApiDifference(ApiDifferenceKind.MissingType, typeName, null, expectedType.Fingerprint, "missing")
                {
                    ExpectedAssembly = expectedType.Assembly
                });
                membersInMissingTypes += expectedType.Members.Count;
                continue;
            }

            matchedTypes++;
            if (!string.Equals(expectedType.Fingerprint, actualType.Fingerprint, StringComparison.Ordinal))
            {
                differences.Add(new ApiDifference(ApiDifferenceKind.TypeMismatch, typeName, null, expectedType.Fingerprint, actualType.Fingerprint)
                {
                    ExpectedAssembly = expectedType.Assembly,
                    ActualAssembly = actualType.Assembly
                });
            }

            var actualMembers = actualType.Members.ToDictionary(member => member.Identity, StringComparer.Ordinal);
            foreach (ApiMember expectedMember in expectedType.Members)
            {
                if (!actualMembers.TryGetValue(expectedMember.Identity, out ApiMember? actualMember))
                {
                    differences.Add(new ApiDifference(ApiDifferenceKind.MissingMember, typeName, expectedMember.Identity, expectedMember.Fingerprint, "missing")
                    {
                        ExpectedAssembly = expectedType.Assembly,
                        ActualAssembly = actualType.Assembly
                    });
                    continue;
                }

                matchedMembers++;
                if (!string.Equals(expectedMember.Fingerprint, actualMember.Fingerprint, StringComparison.Ordinal))
                {
                    differences.Add(new ApiDifference(ApiDifferenceKind.MemberMismatch, typeName, expectedMember.Identity, expectedMember.Fingerprint, actualMember.Fingerprint)
                    {
                        ExpectedAssembly = expectedType.Assembly,
                        ActualAssembly = actualType.Assembly
                    });
                }
            }

            var expectedMemberIds = expectedType.Members.Select(member => member.Identity).ToHashSet(StringComparer.Ordinal);
            foreach (ApiMember actualMember in actualType.Members.Where(member => !expectedMemberIds.Contains(member.Identity)))
            {
                differences.Add(new ApiDifference(ApiDifferenceKind.ExtraMember, typeName, actualMember.Identity, "missing", actualMember.Fingerprint)
                {
                    ExpectedAssembly = expectedType.Assembly,
                    ActualAssembly = actualType.Assembly
                });
            }
        }

        foreach ((string typeName, ApiType actualType) in anity.Types
                     .Where(pair => !unity.Types.ContainsKey(pair.Key))
                     .OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            differences.Add(new ApiDifference(ApiDifferenceKind.ExtraType, typeName, null, "missing", actualType.Fingerprint)
            {
                ActualAssembly = actualType.Assembly
            });
        }

        differences = differences
            .OrderBy(difference => difference.Kind)
            .ThenBy(difference => difference.Type, StringComparer.Ordinal)
            .ThenBy(difference => difference.Member, StringComparer.Ordinal)
            .ToList();

        int missingTypes = Count(ApiDifferenceKind.MissingType);
        int missingMembersOnPresentTypes = Count(ApiDifferenceKind.MissingMember);
        int missingMembers = missingMembersOnPresentTypes + membersInMissingTypes;
        int typeMismatches = Count(ApiDifferenceKind.TypeMismatch);
        int memberMismatches = Count(ApiDifferenceKind.MemberMismatch);
        return new ApiParityReport
        {
            UnityLabel = unityLabel,
            UnitySource = unitySource,
            AnitySource = anitySource,
            GeneratedUtc = DateTime.UtcNow,
            LoadIssues = unity.LoadIssues.Concat(anity.LoadIssues).ToArray(),
            Differences = differences,
            Summary = new ApiParitySummary
            {
                UnityTypes = unity.Types.Count,
                AnityTypes = anity.Types.Count,
                MatchedTypes = matchedTypes,
                ExactTypes = matchedTypes - typeMismatches,
                UnityMembers = unity.MemberCount,
                AnityMembers = anity.MemberCount,
                MatchedMembers = matchedMembers,
                ExactMembers = matchedMembers - memberMismatches,
                MissingTypes = missingTypes,
                ExtraTypes = Count(ApiDifferenceKind.ExtraType),
                TypeMismatches = typeMismatches,
                MissingMembers = missingMembers,
                MissingMembersOnPresentTypes = missingMembersOnPresentTypes,
                MembersInMissingTypes = membersInMissingTypes,
                ExtraMembers = Count(ApiDifferenceKind.ExtraMember),
                MemberMismatches = memberMismatches,
                TypeCoveragePercent = Percent(matchedTypes, unity.Types.Count),
                TypeExactPercent = Percent(matchedTypes - typeMismatches, unity.Types.Count),
                MemberCoveragePercent = Percent(matchedMembers, unity.MemberCount),
                MemberExactPercent = Percent(matchedMembers - memberMismatches, unity.MemberCount)
            }
        };

        int Count(ApiDifferenceKind kind) => differences.Count(difference => difference.Kind == kind);
    }

    private static double Percent(int numerator, int denominator)
        => denominator == 0 ? 100 : Math.Round(100.0 * Math.Max(0, numerator) / denominator, 3);
}
