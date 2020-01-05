namespace IPA.Loader.Features
{
    internal class NoUpdateFeature : Feature
    {
        public override bool Initialize(PluginMetadata meta, string[] parameters)
        {
            return meta.Id != null;
        }

        public override string InvalidMessage { get; protected set; } = "No ID specified; cannot update anyway";
    }
}
