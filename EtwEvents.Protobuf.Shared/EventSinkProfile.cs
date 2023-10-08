using System.Text.RegularExpressions;

namespace KdSoft.EtwLogging
{
    partial class EventSinkProfile
    {
        partial void OnConstruction() {
            this.SinkType = "NullSink";
            this.Name = "Null";
            this.Version = "1.0";
        }

        [GeneratedRegex("\\s(?=([^\"]* \"[^\"]*\")*[^\"]*$)", RegexOptions.Multiline | RegexOptions.Compiled)]
        private static partial Regex StripWhiteSpace();

        /// <summary>
        /// Matches two EventSinkProfiles based on options, credentials and context.
        /// </summary>
        public static bool Matches(EventSinkProfile xProfile, EventSinkProfile yProfile) {
            if (xProfile.PersistentChannel != yProfile.PersistentChannel)
                return false;
            // we try to compare JSON based settings by removing redundant whitespace
            var xOptions = StripWhiteSpace().Replace(xProfile.Options, "");
            var yOptions = StripWhiteSpace().Replace(yProfile.Options, "");
            if (xOptions != yOptions)
                return false;
            var xCredentials = StripWhiteSpace().Replace(xProfile.Credentials, "");
            var yCredentials = StripWhiteSpace().Replace(yProfile.Credentials, "");
            return xCredentials == yCredentials;
        }
    }
}
