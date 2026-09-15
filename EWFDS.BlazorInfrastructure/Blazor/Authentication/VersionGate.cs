using System;
using System.Collections.Generic;
using System.Linq;

namespace EWFDS.BlazorInfrastructure.Blazor.Authentication
{

    /// <summary>
    /// Server-side app-version policy. Compares the client-supplied X-Access-Version
    /// numerically (not as text) to decide whether an instance may run.
    /// </summary>
    public static class VersionGate
    {
        public enum Decision { Allow, Warn, Block }

        public sealed record VersionPolicy(
            Version MinSupported,          // hard floor: below this = blocked
            Version? WarnBelow = null,     // optional soft-deprecation threshold
            IReadOnlyCollection<Version>? Blocked = null); // specific bad builds

        public sealed record VersionResult(Decision Decision, string Message);

        public static VersionResult Evaluate(string? rawClientVersion, VersionPolicy policy)
        {
            // 1. Parse safely. A missing/garbage version is treated as blocked,
            //    so a tampered or ancient client can't slip through as "unknown".
            if (!TryParseVersion(rawClientVersion, out var client))
            {
                return new VersionResult(Decision.Block,
                    "This application version could not be determined. Please install the latest version.");
            }

            // 2. Explicit blocklist (e.g. a known-compromised build above the floor).
            if (policy.Blocked is not null && policy.Blocked.Any(b => VersionsEqual(b, client)))
            {
                return new VersionResult(Decision.Block,
                    $"This version ({client}) has been withdrawn. Please install the latest version.");
            }

            // 3. Hard minimum. Numeric compare via System.Version.
            if (client < policy.MinSupported)
            {
                return new VersionResult(Decision.Block,
                    $"This version ({client}) is no longer supported. " +
                    $"Please install version {policy.MinSupported} or later.");
            }

            // 4. Optional soft-deprecation warning window.
            if (policy.WarnBelow is not null && client < policy.WarnBelow)
            {
                return new VersionResult(Decision.Warn,
                    $"This version ({client}) is deprecated and will stop working soon. " +
                    "Please update at your earliest convenience.");
            }

            return new VersionResult(Decision.Allow, string.Empty);
        }

        private static bool TryParseVersion(string? raw, out Version version)
        {
            version = new Version(0, 0);
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            // Strip any SemVer pre-release/build suffix (e.g. "1.10.0-beta+ci") so
            // System.Version can parse the numeric core. Drop this if you never tag.
            var core = raw.Trim();
            var cut = core.IndexOfAny(new[] { '-', '+' });
            if (cut >= 0)
                core = core[..cut];

            return Version.TryParse(core, out version!);
        }

        // Compare only the components you care about (major.minor.build.revision).
        // System.Version treats unset trailing components as -1, so "1.2" != "1.2.0".
        // Normalising avoids surprises when the client omits trailing zeros.
        private static bool VersionsEqual(Version a, Version b) =>
            Normalize(a) == Normalize(b);

        private static Version Normalize(Version v) =>
            new(v.Major, v.Minor, Math.Max(0, v.Build), Math.Max(0, v.Revision));
    }
}
