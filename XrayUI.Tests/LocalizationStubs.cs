namespace XrayUI.Helpers
{
    // Test-side stand-ins for the WinAppSDK ResourceLoader-backed localization
    // facade (Helpers/L.cs, Helpers/Loc.cs). Only the members that linked
    // production sources actually touch are stubbed; add members here when a
    // newly linked file references more of L.*.
    public static class L
    {
        public static string ServerDetail_Timeout => "Timeout";
        public static string Subscription_NeverUpdated => "Never updated";
        public static string Subscription_JustNow => "Just now";
        public static string Chain_NeedServerList => "Chain needs server list";
        public static string Chain_EndpointMissing => "Chain endpoint missing";
        public static string Chain_NoNesting => "Chain nesting not supported";
        public static string ConfigProfile_DiscardMsg => "Discard changes?";
        public static string ConfigProfile_DiscardTitle => "Discard";
        public static string ConfigProfile_ErrEmpty => "Empty";
        public static string ConfigProfile_ErrInboundsMissing => "Inbounds missing";
        public static string ConfigProfile_ErrOutboundsNotAllowed => "Outbounds not allowed";
        public static string ConfigProfile_ErrRootMustBeObject => "Object required";
        public static string ConfigProfile_ErrTunInboundMissing => "TUN required";
        public static string ConfigProfile_ErrTunInboundNotAllowed => "TUN not allowed";
        public static string ConfigProfile_ExternalChangeWarning => "Unsaved edits";
        public static string ConfigProfile_LoadFailed => "Load failed";
        public static string ConfigProfile_PreviewNoServer => "No server";
        public static string ConfigProfile_PreviewStaleMsg => "Save first";
        public static string ConfigProfile_PreviewStaleTitle => "Unsaved";
        public static string ConfigProfile_PreviewTitle => "Preview";
        public static string ConfigProfile_ResetMsg => "Reset?";
        public static string ConfigProfile_ResetTitle => "Reset";
        public static string ConfigProfile_Saved => "Saved";
        public static string ConfigProfile_SavedInactive => "Saved inactive";
        public static string ConfigProfile_SaveFailedTitle => "Save failed";
        public static string ConfigProfile_SettingsUnwritable => "Unwritable";
        public static string ConfigProfile_WarnNoAutoSystemRouting => "No auto routes";
        public static string ConfigProfile_WarnNoSystemProxyInbound => "No system proxy";
    }

    public static class Loc
    {
        public static string GetString(string key) => key;
        public static string Format(string key, params object?[] args) =>
            $"{key}({string.Join(",", args)})";
    }
}

namespace Microsoft.UI.Xaml
{
    public enum Visibility
    {
        Visible = 0,
        Collapsed = 1
    }
}
