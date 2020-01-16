using System.Collections.Generic;

namespace TeamCityBuildMonitor.Models.Home
{
    public class BuildMonitorViewModel
    {
        public BuildMonitorViewModel()
        {
            Pipelines = new List<Pipeline>();
        }

        public List<Pipeline> Pipelines { get; set; }
    }
}