namespace Servercyde.Monitoring.Tests.TestAttributes;

public class IntegrationTestAttribute(string explanation = "")
    : CategoryTraitAttribute("IntegrationTest", explanation)
{
}

