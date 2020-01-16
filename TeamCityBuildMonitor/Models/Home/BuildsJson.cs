using System;
using System.Collections.Generic;

namespace TeamCityBuildMonitor.Models.Home
{
    public class BuildsJson
    {
        public BuildsJson()
        {
            UpdatedText = $"Updated {DateTime.UtcNow:ddd, h:mm:ss tt}";
            Builds = new List<BuildJson>();
        }

        public string UpdatedText { get; set; }
        public List<BuildJson> Builds { get; set; }
    }
}