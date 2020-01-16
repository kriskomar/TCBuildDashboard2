using System.Collections.Generic;
using TeamCityBuildMonitor.Models.Home;

namespace TeamCityBuildMonitor.Models.Api
{
    public class ApiBuilds
    {
        public ApiBuilds()
        {
            Pipelines = new List<Pipeline>();
        }
        public List<Pipeline> Pipelines { get; set; }
        public string UpdatedText { get; set; }
    }
}
