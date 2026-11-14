using IPA.Json.Converters;
using IPA.Loader;
using System.Text.Json.Serialization;

namespace IPA.Json.SerializerContexts
{
    [JsonSerializable(typeof(PluginManifest))]
    [JsonSourceGenerationOptions(Converters =
    [
        typeof(SemverRangeConverter),
        typeof(SemverVersionConverter),
        typeof(AlmostVersionConverter),
        typeof(FeaturesFieldConverter)
    ], PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, RespectNullableAnnotations = true)]
    internal sealed partial class PluginManifestSerializerContext : JsonSerializerContext;
}
