using System.Xml.Serialization;

namespace TeamCityBuildMonitor.Models.Home
{
    public class Pipeline
    {
        [XmlAttribute("name")] public string Name { get; set; }

        [XmlAttribute("builderid")] public string BuilderId { get; set; }

        [XmlAttribute("qaid")] public string QAId { get; set; }

        [XmlAttribute("stgid")] public string STGId { get; set; }

        [XmlAttribute("rcid")] public string RCId { get; set; }

        [XmlAttribute("liveid")] public string LIVEId { get; set; }

        [XmlAttribute("text")] public string Text { get; set; }

        public string CurrentStep { get; set; }
        public BuildStatus BuilderStatus { get; set; }
        public string Branch { get; set; }
        public string Progress { get; set; }
        public string UpdatedBy { get; set; }
        public string LastRunText { get; set; }
        public bool IsQueued { get; set; }
        public string StatusDescription { get; set; }
        public string BrokenByName { get; set; }
        public string BrokenByPicData { get; set; }
        public string BrokenBySpeech { get; set; }
        public bool ShowBroken { get; set; }
        public BuildStatus QAStatus { get; set; }
        public BuildStatus STGStatus { get; set; }
        public BuildStatus RCStatus { get; set; }
        public BuildStatus LIVEStatus { get; set; }

        public string QACssClass { get; set; }
        public string STGCssClass { get; set; }
        public string RCCssClass { get; set; }
        public string LIVECssClass { get; set; }

        public string StatusText
        {
            get
            {
                switch (BuilderStatus)
                {
                    case BuildStatus.Success:
                        return "OK";

                    case BuildStatus.Failure:
                        return "FAIL";

                    case BuildStatus.Running:
                        return "RUNNING";

                    case BuildStatus.Error:
                        return "ERROR";

                    default:
                        return "UNKNOWN";
                }
            }
        }
    }
}