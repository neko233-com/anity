using System.Text;
using UnityEditor.ShaderGraph.Model;

namespace UnityEditor.ShaderGraph.Generation;

internal static class ShaderKeywordPragmaGenerator
{
    private static readonly (int Bit, string Suffix)[] StageSuffixes =
    {
        (1, "_vertex"),
        (2, "_fragment"),
        (4, "_geometry"),
        (8, "_hull"),
        (16, "_domain"),
        (32, "_raytracing")
    };

    internal static string Generate(ShaderGraphBlackboard blackboard)
    {
        if (blackboard is null) throw new ArgumentNullException(nameof(blackboard));
        var output = new StringBuilder();
        foreach (ShaderGraphKeyword keyword in blackboard.Keywords)
        {
            if (keyword.Definition == 2) continue;
            string declaration = keyword.Definition == 0 ? "shader_feature" : "multi_compile";
            if (keyword.Scope == 0) declaration += "_local";
            string variants = keyword.KeywordType == 0
                ? "_ " + keyword.ReferenceName
                : string.Join(" ", keyword.Entries.Select(entry =>
                    string.IsNullOrEmpty(entry.ReferenceName)
                        ? "_"
                        : keyword.ReferenceName + "_" + entry.ReferenceName));

            if (keyword.Stages == 0 || keyword.Stages == 63)
            {
                output.Append("#pragma ").Append(declaration).Append(' ').Append(variants).Append('\n');
                continue;
            }
            foreach ((int bit, string suffix) in StageSuffixes)
            {
                if ((keyword.Stages & bit) != 0)
                    output.Append("#pragma ").Append(declaration).Append(suffix).Append(' ').Append(variants).Append('\n');
            }
        }
        return output.ToString();
    }
}
