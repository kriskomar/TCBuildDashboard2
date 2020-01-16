using System.Threading.Tasks;
using TeamCityBuildMonitor.Models.Home;

namespace TeamCityBuildMonitor.Helpers
{
    public interface IBuildMonitorModelHandler
    {
        Task<BuildMonitorViewModel> GetModelAsync();
    }
}