using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using TeamCityBuildMonitor.Helpers;
using TeamCityBuildMonitor.Models.Home;

namespace TeamCityBuildMonitor.Controllers
{
    public class HomeController : Controller
    {
        private readonly IBuildMonitorModelHandler _modelHandler;
        private readonly IRazorViewEngine _razorViewEngine;
        private readonly IServiceProvider _serviceProvider;
        private readonly ITempDataProvider _tempDataProvider;

        public HomeController(
            IBuildMonitorModelHandler modelHandler,
            IConfiguration configuration,
            IRazorViewEngine razorViewEngine,
            ITempDataProvider tempDataProvider,
            IServiceProvider serviceProvider,
            IHttpContextAccessor httpContextAccessor)
        {
            _modelHandler = modelHandler;
            _razorViewEngine = razorViewEngine;
            _tempDataProvider = tempDataProvider;
            _serviceProvider = serviceProvider;
            
        }

        public static string BuildItemView = "_BuildItemFlex";

        public async Task<IActionResult> Index()
        {
            var model = await _modelHandler.GetModelAsync();
            model.Pipelines = SortPipelines(model.Pipelines);

            return View(model);
        }

        [HttpPost]
        public async Task<JsonResult> GetBuildsAsync()
        {
            var model = await _modelHandler.GetModelAsync();
            model.Pipelines = SortPipelines(model.Pipelines);
            var ajaxModel = new BuildsJson();

            foreach (var build in model.Pipelines)
                ajaxModel.Builds.Add(new BuildJson
                {
                    Id = build.BuilderId,
                    Content = await RenderToStringAsync(BuildItemView, build),
                    Status = build.StatusText,
                    BrokenBySpeech = build.BrokenBySpeech
                });

            return Json(ajaxModel);
        }

        private static List<Pipeline> SortPipelines(IEnumerable<Pipeline> pipelines)
        {
            var sortedPipelines = pipelines.OrderByDescending(x => x.ShowBroken).ThenBy(y => y.Name).ToList();
            return sortedPipelines;
        }

        protected async Task<string> RenderToStringAsync(string viewName, object model)
        {
            var httpContext = new DefaultHttpContext {RequestServices = _serviceProvider};
            var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

            using (var sw = new StringWriter())
            {
                var viewResult = _razorViewEngine.FindView(actionContext, viewName, false);

                if (viewResult.View == null)
                    throw new ArgumentNullException($"{viewName} does not match any available view");

                var viewDictionary =
                    new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
                    {
                        Model = model
                    };

                var viewContext = new ViewContext(
                    actionContext,
                    viewResult.View,
                    viewDictionary,
                    new TempDataDictionary(actionContext.HttpContext, _tempDataProvider),
                    sw,
                    new HtmlHelperOptions()
                );

                await viewResult.View.RenderAsync(viewContext);
                return sw.ToString();
            }
        }
    }
}