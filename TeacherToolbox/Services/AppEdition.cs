namespace TeacherToolbox.Services
{
    public sealed class AppEdition : IAppEdition
    {
        private AppEdition(AppFlavor flavor)
        {
            Flavor = flavor;
        }

        public AppFlavor Flavor { get; }

        public bool CloudTelemetryEnabled => Flavor == AppFlavor.Outwood;

        public bool RegisterSyncEnabled => false;

        public bool OutwoodOnlyFeaturesEnabled => Flavor == AppFlavor.Outwood;

        public static AppEdition Current { get; } = new AppEdition(GetBuildFlavor());

        private static AppFlavor GetBuildFlavor()
        {
#if OUTWOOD_BUILD
            return AppFlavor.Outwood;
#else
            return AppFlavor.Global;
#endif
        }
    }
}
