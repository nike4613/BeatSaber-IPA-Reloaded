using IPA.Loader.Features;
using System.Text.Json.Serialization;

namespace IPA.Json.SerializerContexts
{
    [JsonSerializable(typeof(DefineFeature.DataModel), TypeInfoPropertyName = "DefineFeatureDataModel")]
    [JsonSerializable(typeof(ConfigProviderFeature.DataModel), TypeInfoPropertyName = "ConfigProviderFeatureDataModel")]
    [JsonSourceGenerationOptions(RespectNullableAnnotations = true)]
    internal sealed partial class FeatureDataModelSerializerContext : JsonSerializerContext;
}
