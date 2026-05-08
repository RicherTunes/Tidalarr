using System;

namespace Lidarr.Plugin.Common.TestKit.Compliance;

// <summary>
// Local declaration of the wave-11 parity opt-in attribute. The canonical type ships in
// the Lidarr.Plugin.Common.TestKit assembly, which is a test-only dependency the plugin
// production project does not (and should not) reference. The parity check matches by
// AttributeType.FullName (string), so a same-fully-qualified-name marker compiled into
// the plugin assembly is sufficient.
// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
internal sealed class ParityAllowedTokenStoreAttribute : Attribute
{
    public ParityAllowedTokenStoreAttribute(string rationale)
    {
        Rationale = rationale ?? throw new ArgumentNullException(nameof(rationale));
    }

    public string Rationale { get; }
}
