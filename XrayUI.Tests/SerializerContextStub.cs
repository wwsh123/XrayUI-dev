using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using XrayUI.Models;

namespace XrayUI.Services
{
    // Test-side stand-in for the app's source-generated AppJsonSerializerContext.
    internal class AppJsonSerializerContext
    {
        public static JsonSerializerOptions WriteReadable { get; } = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };

        public static AppJsonSerializerContext Default { get; } = new();

        public JsonTypeInfo<PresetSettings> PresetSettings =>
            (JsonTypeInfo<PresetSettings>)WriteReadable.GetTypeInfo(typeof(PresetSettings));

        public JsonTypeInfo<AppSettings> AppSettings =>
            (JsonTypeInfo<AppSettings>)WriteReadable.GetTypeInfo(typeof(AppSettings));

        public JsonTypeInfo<List<string>> ListString =>
            (JsonTypeInfo<List<string>>)WriteReadable.GetTypeInfo(typeof(List<string>));
    }
}
