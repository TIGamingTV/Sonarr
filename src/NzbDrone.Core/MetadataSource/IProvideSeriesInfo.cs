using System;
using System.Collections.Generic;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MetadataSource
{
    public interface IProvideSeriesInfo
    {
        Tuple<Series, List<Episode>> GetSeriesInfo(int tvdbSeriesId, Language language, string seasonType);

        // Fetches series info from the metadata source selected for the series
        Tuple<Series, List<Episode>> GetSeriesInfo(Series series);

        MetadataSourceType GetMetadataSource(Series series);
        int ResolveTmdbId(Series series);
    }
}
