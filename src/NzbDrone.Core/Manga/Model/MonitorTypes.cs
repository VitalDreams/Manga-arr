using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Manga
{
    public class MonitorTypes
    {
        public const string All = "all";
        public const string Future = "future";
        public const string Missing = "missing";
        public const string Existing = "existing";
        public const string Latest = "latest";
        public const string None = "none";
    }

    public class NewItemMonitorTypes
    {
        public const string All = "all";
        public const string None = "none";
        public const string Specific = "specific";
    }

    public class AddMangaSeriesOptions
    {
        public bool SearchForMissingVolumes { get; set; }
        public bool Monitor { get; set; } = true;
    }

    public class AddVolumeOptions
    {
        public bool SearchForVolume { get; set; }
    }

    public class MonitoringOptions
    {
        public string Monitor { get; set; } = MonitorTypes.All;
        public int[] VolumesToMonitor { get; set; }
    }
}
