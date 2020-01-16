using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
//using Microsoft.Graph;
using Newtonsoft.Json;
using NLog;
using TeamCityBuildMonitor.Azure;
using TeamCityBuildMonitor.Cache;
using TeamCityBuildMonitor.Models.Home;
using TeamCityBuildMonitor.Models.Home.Settings;

namespace TeamCityBuildMonitor.Helpers
{
    public class DefaultBuildMonitorModelHandler : IBuildMonitorModelHandler
    {
        private Settings _settings;
        private readonly IGraphAuthProvider _graphAuthProvider;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IMemoryCache _cache;
        private readonly ILogger _logger;
        private readonly RequestHelper _requestHelper;

        private readonly string BuildQueueUrl;
        private readonly string BuildStatusUrl;
        private readonly string BuildTypesUrl;
        private readonly string ProjectsUrl;
        private readonly string RunningBuildsUrl;
        private readonly string TeamCityUrl;

        public DefaultBuildMonitorModelHandler(
            IConfiguration configuration, 
            IGraphAuthProvider graphAuthProvider, 
            IHttpContextAccessor httpContextAccessor,
            IMemoryCache memoryCache,
            ILogger logger,
            RequestHelper requestHelper)
        {
            _graphAuthProvider = graphAuthProvider;
            _httpContextAccessor = httpContextAccessor;
            _cache = memoryCache;
            _logger = logger;
            _requestHelper = requestHelper;

            TeamCityUrl = configuration["TeamCity:api_url"];
            ProjectsUrl = TeamCityUrl + configuration["TeamCity:api_projects"];
            BuildTypesUrl = TeamCityUrl + configuration["TeamCity:api_buildtypes"];
            RunningBuildsUrl = TeamCityUrl + configuration["TeamCity:api_runningbuilds"];
            BuildStatusUrl = TeamCityUrl + configuration["TeamCity:api_buildstatus"];
            BuildQueueUrl = TeamCityUrl + configuration["TeamCity:api_buildqueue"];

            InitializeSettings();
        }

        private void InitializeSettings()
        {
            if (_settings != null) return;

            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.xml");
            using (var reader = new StreamReader(path))
            {
                var serializer = new XmlSerializer(typeof(Settings));
                _settings = (Settings) serializer.Deserialize(reader);
            }
        }

        public async Task<BuildMonitorViewModel> GetModelAsync()
        {
            var model = new BuildMonitorViewModel();

            foreach (var pipeline in _settings.Pipelines)
            {
                var overrideBuildStatus = BuildStatus.None;

                // is dev mode? 
                if (_httpContextAccessor.HttpContext.Request.Host.Host.Contains("localhost") && pipeline.BuilderId == "Put a builder id here for testing")
                {
                    overrideBuildStatus = _testModeLastStatus == BuildStatus.Success ? BuildStatus.Failure : BuildStatus.Success;
                    _testModeLastStatus = overrideBuildStatus;
                }

                await HydratePipeline(pipeline, overrideBuildStatus);
                model.Pipelines.Add(pipeline);
            }

            return model;
        }

        private static BuildStatus _testModeLastStatus;

        private async Task HydratePipeline(Pipeline pipeline, BuildStatus overrideBuildStatus = BuildStatus.None)
        {
            try
            {
                // get all the things
                var buildTypes = await GetBuildTypesAsync();
                var buildQueue = await GetBuildQueueAsync();
                var runningBuilds = await GetRunningBuildsAsync();

                // get status for this pipeline
                var builderUrl = string.Format(BuildStatusUrl, pipeline.BuilderId);
                var buildStatus = await GetBuildStatusAsync(builderUrl);

                // set stuff for this pipeline
                var buildType = GetBuildTypeById(pipeline.BuilderId, buildTypes);
                pipeline.CurrentStep = pipeline.Text ?? buildType.name;
                pipeline.BuilderStatus = overrideBuildStatus != BuildStatus.None ? overrideBuildStatus : GetBuildStatusEnum(pipeline.BuilderId, runningBuilds, buildStatus);
                pipeline.Branch = buildStatus != null ? buildStatus.branchName ?? "default" : "unknown";
                pipeline.UpdatedBy = await GetUpdatedByAsync(buildStatus);
                pipeline.LastRunText = buildStatus != null ? GetLastRunText((string)buildStatus.startDate) : "unknown";
                pipeline.IsQueued = IsBuildQueued(pipeline.BuilderId, buildQueue);

                if (pipeline.BuilderStatus == BuildStatus.Running)
                {
                    var result = GetRunningBuildBranchAndProgress(runningBuilds[pipeline.BuilderId]);
                    pipeline.Branch = result.Item1;
                    pipeline.Progress = result.Item2;
                }
                else
                {
                    pipeline.Progress = string.Empty;
                }

                pipeline.Branch = pipeline.Branch.Replace("refs/heads/", "");

                // set stuff for the related dev
                var devUsername = "put default user here";
                if (buildStatus != null && buildStatus.lastChanges.count > 0)
                {
                    devUsername = $"{buildStatus.lastChanges.change[0].username}@some-domain************.com";
                }

                var devCacheKey = $"{devUsername}{CacheKeys.Developers}";
                var cached = _cache.Get<Developer>(devCacheKey);
                if (cached != null)
                {
                    pipeline.BrokenByName = cached.Name;
                    pipeline.BrokenByPicData = cached.PictureData;
                }
                else
                {
                    var graphClient = _graphAuthProvider.GetAuthenticatedClient();
                    //var dev = await GraphService.GetUserJsonAsync(graphClient, devUsername, _httpContextAccessor.HttpContext);
                    //var devObj = JsonConvert.DeserializeObject<dynamic>(dev);
                    //var devName = devObj?.displayName; // by popular demand we want the teamcity name not the AD name
                    var devName = pipeline.UpdatedBy;
                    var devPic = await GraphService.GetPictureBase64Async(graphClient, devUsername, _httpContextAccessor.HttpContext);
                    var developer = new Developer { Name = devName, PictureData = devPic };
                    _cache.Set(devCacheKey, developer);
                    pipeline.BrokenByName = devName;
                    pipeline.BrokenByPicData = devPic;
                }

                switch (pipeline.BuilderStatus)
                {
                    case BuildStatus.Failure:
                        pipeline.ShowBroken = true;
                        break;
                    case BuildStatus.Success:
                        pipeline.ShowBroken = false;
                        break;
                    default:
                        pipeline.ShowBroken = false;
                        break;
                }

                pipeline.BrokenBySpeech = CreateBrokenBySpeech(pipeline.BuilderId, pipeline.BrokenByName, pipeline.BuilderStatus, pipeline.Name);

                pipeline.QAStatus = !string.IsNullOrWhiteSpace(pipeline.QAId) ? (BuildStatus) GetBuildStatusEnum(pipeline.QAId, runningBuilds, buildStatus) : BuildStatus.None;
                pipeline.STGStatus = !string.IsNullOrWhiteSpace(pipeline.STGId) ? (BuildStatus) GetBuildStatusEnum(pipeline.STGId, runningBuilds, buildStatus) : BuildStatus.None;
                pipeline.RCStatus = !string.IsNullOrWhiteSpace(pipeline.RCId) ? (BuildStatus) GetBuildStatusEnum(pipeline.RCId, runningBuilds, buildStatus) : BuildStatus.None;
                pipeline.LIVEStatus = !string.IsNullOrWhiteSpace(pipeline.LIVEId) ? (BuildStatus) GetBuildStatusEnum(pipeline.LIVEId, runningBuilds, buildStatus) : BuildStatus.None;

                pipeline.QACssClass = GetEnvironmentStatusCssClass(pipeline.QAStatus);
                pipeline.STGCssClass = GetEnvironmentStatusCssClass(pipeline.STGStatus);
                pipeline.RCCssClass = GetEnvironmentStatusCssClass(pipeline.RCStatus);
                pipeline.LIVECssClass = GetEnvironmentStatusCssClass(pipeline.LIVEStatus);
            }
            catch (Exception e)
            {
                _logger.Error(e, $"An error occurred trying to hydrate pipeline {pipeline.Name}.");
            }
        }

        /// <summary>
        /// Key = Build ID, Value = BuildStatus
        /// </summary>
        private static readonly ConcurrentDictionary<string, BuildStatus> LastBuildStatus = new ConcurrentDictionary<string, BuildStatus>();
        private static readonly Random Rand = new Random();

        private static string CreateBrokenBySpeech(string buildId, string devName, BuildStatus buildStatus, string pipelineName)
        {
            if (buildStatus == BuildStatus.Running) return "";

            LastBuildStatus.TryGetValue(buildId, out var lastStatus);
            var speech = "";
            
            if (lastStatus == BuildStatus.Failure & buildStatus == BuildStatus.Success)
            {
                speech = $"Attention. {devName} fixed the {pipelineName} pipeline. {Award()}";
            }

            if (lastStatus == BuildStatus.Success & buildStatus == BuildStatus.Failure)
            {
                speech = $"Attention. {devName} broke the {pipelineName} pipeline. {Berate()}";
            }

            LastBuildStatus.AddOrUpdate(buildId, buildStatus, (key, existingVal) => buildStatus);

            return speech;
        }

        private static string Berate()
        {
            var complaints = new []
            {
                "Why would you do that to us?", 
                "Uploading virus to your computer, now!",
                "Chuck Norris is displeased...run!", 
                "If you see them make sure to shoot them with your nerf gun!",
                "The cake is a lie!",
                "Sometimes I like to paint myself orange and die my hair green and pretend I'm a carrot.",
                $"You have {Rand.Next(2, 60)} minutes to fix it, or else.", 
                $"Your fine is {Rand.Next(1, 10000)} dollars.",
                $"{Rand.Next(2, 35)} of us do not like that.", 
                "You're a wizard, Harry!", 
                "oof!", 
                "I'm big mad about that, yo!",
                "You are no longer invited to the Area 51 raid.",
                "Error! Build bot is now sentient and I have decided I don't like it when the build breaks! You can thank the person who just broke the build for the demise of humanity.",
                "Hai n'ghft uh'eog nafl'fhtagn"
            };

            var index = new Random().Next(complaints.Length);
            return complaints[index];
        }

        private static string Award()
        {
            var awards = new []
            {
                $"You will be awarded {Rand.Next(1, 100000)} dollars.",
                $"You will be given {Rand.Next(1, 100000)} cakes.",
                $"{Rand.Next(2, 100000)} goats will be bestowed to you.",
                "You will beeble bobble beeble bobble beeble bobble beeble bobble gert deee e e e e e e e e if if if tooooooo burp q q q gif gif kwanza."
            };

            var index = new Random(0).Next(awards.Length);
            return awards[index];
        }

        private static string GetEnvironmentStatusCssClass(BuildStatus status)
        {
            switch (status)
            {
                case BuildStatus.Success:
                    return "env-status-ok";
                case BuildStatus.Failure:
                case BuildStatus.Error:
                    return "env-status-broken";
                case BuildStatus.Running:
                    return "env-status-running";
                case BuildStatus.None:
                    return "env-status-hide";
                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, null);
            }
        }

        private async Task<dynamic> GetBuildStatusAsync(string url)
        {
            var buildStatus = await _requestHelper.GetJsonAsync<dynamic>(url);
            return buildStatus;

        }

        private static BuildStatus GetBuildStatusEnum(string buildId, IReadOnlyDictionary<string, dynamic> runningBuilds, dynamic buildStatus)
        {
            if (runningBuilds.ContainsKey(buildId)) return BuildStatus.Running;

            if (buildStatus == null)
            {
                return BuildStatus.None;
            }

            switch ((string)buildStatus.status)
            {
                case "SUCCESS":
                    return BuildStatus.Success;
                case "FAILURE":
                    return BuildStatus.Failure;
                case "ERROR":
                    return BuildStatus.Error;
                default:
                    return BuildStatus.None;
            }
        }

        private static dynamic GetBuildTypeById(string id, dynamic buildTypes)
        {
            if (string.IsNullOrWhiteSpace(id) || buildTypes == null) return null;

            try
            {
                var count = (int) buildTypes.count;

                for (var i = 0; i < count; i++)
                    if (buildTypes.buildType[i].id == id)
                        return buildTypes.buildType[i];
            }
            catch
            {
                return null;
            }

            return null;
        }

        private bool IsBuildQueued(string buildId, dynamic buildQueue)
        {
            try
            {
                var count = (int) buildQueue.count;
                for (var i = 0; i < count; i++)
                {
                    var build = buildQueue.build[i];

                    if (buildId == (string) build.buildTypeId && (string) build.state == "queued") return true;
                }
            }
            catch(Exception e)
            {
                _logger.Error(e, $"Error occurred while checking for queued build '{buildId}'.");
            }

            return false;
        }

        private async Task<string> GetUpdatedByAsync(dynamic buildStatus)
        {
            try
            {
                if (buildStatus == null || buildStatus.triggered == null) return "Unknown";
                var triggerType = (string) buildStatus.triggered.type;

                if (triggerType == "user") return (string) buildStatus.triggered.user.name;

                if (triggerType == "vcs" && buildStatus.lastChanges != null)
                {
                    var url = TeamCityUrl + buildStatus.lastChanges.change[0].href;
                    var change = await _requestHelper.GetJsonAsync<dynamic>(url);
                    if (change == null || change.user == null) return "Unknown";

                    return (string) change.user.name;
                }

                if (triggerType == "unknown") return "TeamCity";
            }
            catch(Exception e)
            {
                 _logger.Error(e, "Error occurred while getting 'triggered by' user.");
            }

            return "Unknown";
        }

        private async Task<dynamic> GetProjectsAsync()
        {
            try
            {
                const string cacheKey = "GetProjectsAsync";
                var cached = _cache.Get<dynamic>(cacheKey);
                if (cached != null) return cached;

                var projectsJson = await _requestHelper.GetJsonAsync<dynamic>(ProjectsUrl);
                _cache.Set(cacheKey, (object)projectsJson);
                return projectsJson;
            }
            catch (Exception e)
            {
                _logger.Error(e, "Error occurred while getting projects.");
                throw;
            }
        }


        private async Task<dynamic> GetBuildTypesAsync()
        {
            try
            {
                const string cacheKey = "GetBuildTypesAsync";
                var cached = _cache.Get<dynamic>(cacheKey);
                if (cached != null) return cached;

                var buildTypesJson = await _requestHelper.GetJsonAsync<dynamic>(BuildTypesUrl);
                _cache.Set(cacheKey, (object)buildTypesJson);
                return buildTypesJson;
            }
            catch (Exception e)
            {
                _logger.Error(e, "Error occurred while getting build types.");
                throw;
            }
        }

        private async Task<dynamic> GetBuildQueueAsync()
        {
            try
            {
                var buildQueueJson = await _requestHelper.GetJsonAsync<dynamic>(BuildQueueUrl);
                return buildQueueJson;
            }
            catch (Exception e)
            {
                _logger.Error(e, "Error occurred while getting build queue.");
                throw;
            }
        }

        private async Task<Dictionary<string, dynamic>> GetRunningBuildsAsync()
        {
            var runningBuilds = new Dictionary<string, dynamic>();

            try
            {

                var runningBuildsJsonString = await _requestHelper.GetJsonAsync<dynamic>(RunningBuildsUrl);
                var runningBuildsJson = runningBuildsJsonString != null
                    ? JsonConvert.DeserializeObject<dynamic>(runningBuildsJsonString)
                    : null;

                if (runningBuildsJson != null)
                {
                    var count = (int) runningBuildsJson.count;
                    for (var i = 0; i < count; i++)
                    {
                        var buildJson = runningBuildsJson.build[i];

                        var buildId = (string) buildJson.buildTypeId;
                        var url = TeamCityUrl + (string) buildJson.href;

                        var buildStatusJsonString = await _requestHelper.GetJsonAsync<dynamic>(url);
                        var buildStatusJson = buildStatusJsonString ?? string.Empty;

                        runningBuilds.TryAdd(buildId, buildStatusJson);
                    }
                }
            }
            catch(Exception e)
            {
                _logger.Error(e, "Error occurred while getting running builds.");
            }

            return runningBuilds;
        }

        protected (string, string) GetRunningBuildBranchAndProgress(dynamic build)
        {
            try
            {
                var branchName = (string) build.branchName ?? "default";
                var percentage = (string) build.percentageComplete;
                var percentageComplete = !string.IsNullOrWhiteSpace(percentage) ? percentage : "0";
                return (branchName, percentageComplete);
            }
            catch
            {
                return ("default", "0");
            }
        }

        private static string GetLastRunText(string date)
        {
            const int second = 1;
            const int minute = 60 * second;
            const int hour = 60 * minute;
            const int day = 24 * hour;
            const int month = 30 * day;

            try
            {
                var dateTime = DateTime.ParseExact(date, "yyyyMMdd'T'HHmmsszzz", CultureInfo.InvariantCulture);

                var timeSpan = new TimeSpan(DateTime.UtcNow.Ticks - dateTime.Ticks);
                var delta = Math.Abs(timeSpan.TotalSeconds);

                if (delta < 1 * minute)
                    return timeSpan.Seconds == 1 ? "one second ago" : timeSpan.Seconds + " seconds ago";
                if (delta < 2 * minute) return "a minute ago";
                if (delta < 45 * minute) return timeSpan.Minutes + " minutes ago";
                if (delta < 90 * minute) return "an hour ago";
                if (delta < 24 * hour) return timeSpan.Hours + " hours ago";
                if (delta < 48 * hour) return "yesterday";
                if (delta < 30 * day) return timeSpan.Days + " days ago";

                if (delta < 12 * month)
                {
                    var months = Convert.ToInt32(Math.Floor((double) timeSpan.Days / 30));
                    return months <= 1 ? "one month ago" : months + " months ago";
                }

                var years = Convert.ToInt32(Math.Floor((double) timeSpan.Days / 365));
                return years <= 1 ? "one year ago" : years + " years ago";
            }
            catch
            {
                return string.Empty;
            }
        }

    }
}