using Xunit.v3;

namespace Servercyde.Monitoring.Tests.TestAttributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = true)]
public class CategoryTraitAttribute(string category, string explanation = "") : Attribute, ITraitAttribute
{
    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits() => [
        new("Category", category),
        new("Explanation", explanation)
    ];
}
