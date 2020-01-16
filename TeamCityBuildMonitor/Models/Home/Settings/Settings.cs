using System.Collections.Generic;

namespace TeamCityBuildMonitor.Models.Home.Settings
{
    public class Settings
    {
        public Settings()
        {
            Pipelines = new List<Pipeline>();
        }

        public List<Pipeline> Pipelines { get; set; }
    }
}