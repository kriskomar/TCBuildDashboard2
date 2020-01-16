using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using TeamCityBuildMonitor.Helpers;
using TeamCityBuildMonitor.Models.Api;

namespace TeamCityBuildMonitor.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PipelinesController : ControllerBase
    {
        private readonly IBuildMonitorModelHandler _modelHandler;

        public PipelinesController(IBuildMonitorModelHandler modelHandler)
        {
            _modelHandler = modelHandler;
        }

        [HttpGet]
        public async Task<string> Get()
        {
            // Get data
            var model = await _modelHandler.GetModelAsync();

            // project to Json model
            var projectsJson = new ApiBuilds
            {
                UpdatedText = $"Updated {DateTime.UtcNow:ddd, h:mm:ss tt}",
                Pipelines =  model.Pipelines
            };

            // convert and pipe it out!
            var json = JsonConvert.SerializeObject(projectsJson);
            return json;
        }

        // GET: api/Pipelines/5
        [HttpGet("{id}", Name = "Get")]
        public string Get(int id)
        {
            return "value";
        }

    }
}
