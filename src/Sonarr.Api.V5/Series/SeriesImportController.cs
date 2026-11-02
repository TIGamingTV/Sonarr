using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Tv;
using Sonarr.Http;

namespace Sonarr.Api.V5.Series
{
    [V5ApiController("series/import")]
    public class SeriesImportController : Controller
    {
        private readonly IAddSeriesService _addSeriesService;
        private readonly IConfigService _configService;

        public SeriesImportController(IAddSeriesService addSeriesService, IConfigService configService)
        {
            _addSeriesService = addSeriesService;
            _configService = configService;
        }

        [HttpPost]
        [Consumes("application/json")]
        [Produces("application/json")]
        public Ok<List<SeriesResource>> Import([FromBody] List<SeriesResource> resource)
        {
            var newSeries = resource.ToModel();

            for (var i = 0; i < resource.Count; i++)
            {
                if (resource[i].MetadataSource == null && newSeries[i].TvdbId > 0)
                {
                    newSeries[i].MetadataSource = _configService.DefaultMetadataSource;
                }
            }

            return TypedResults.Ok(_addSeriesService.AddSeries(newSeries).ToResource());
        }
    }
}
