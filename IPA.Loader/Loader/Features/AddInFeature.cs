namespace IPA.Loader.Features
{
    internal class AddInFeature : Feature
    {
        private PluginLoader.PluginMetadata selfMeta;

        public override bool Initialize(PluginLoader.PluginMetadata meta, string[] parameters)
        {
            selfMeta = meta;

            RequireLoaded(meta);

            return true;
        }

        public override bool BeforeLoad(PluginLoader.PluginMetadata plugin)
        {
            return plugin != selfMeta;
        }

        public override string InvalidMessage
        {
            get => "Plugin is an add-in for some other mod, therefore should not be loaded.";
            protected set { }
        }
    }
}
