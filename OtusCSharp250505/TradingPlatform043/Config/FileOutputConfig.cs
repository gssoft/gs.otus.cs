namespace TradingPlatform.Config
{
    public class FileOutputConfig
    {
        public string PortsFileName { get; set; } = "platform_urls.txt";
        public string StatsFileName { get; set; } = "platform_stats.txt";
        public bool EnableFileOutput { get; set; } = true;
        public string OutputDirectory { get; set; } = "Logs";
    }

    public class DisplayConfig
    {
        public int PauseSeconds { get; set; } = 5;
        public bool ShowApiEndpoints { get; set; } = true;
        public bool ShowAllPages { get; set; } = true;
    }

    public class ApplicationInfoConfig
    {
        public string Name { get; set; } = "TradingPlatform";
        public string Version { get; set; } = "1.0.0";
    }
}
