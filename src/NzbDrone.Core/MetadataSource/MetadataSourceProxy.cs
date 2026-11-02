using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.MetadataSource.SkyHook;
using NzbDrone.Core.MetadataSource.Tmdb;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MetadataSource
{
    // Routes metadata requests to TheTVDB (via SkyHook) or TMDB and merges search results from both.
    public class MetadataSourceProxy : IProvideSeriesInfo, ISearchForNewSeries
    {
        private const int MaxTmdbDetailLookups = 20;

        private readonly ISkyHookProxy _skyHookProxy;
        private readonly ITmdbProxy _tmdbProxy;
        private readonly ISeriesService _seriesService;
        private readonly Logger _logger;

        public MetadataSourceProxy(ISkyHookProxy skyHookProxy,
                                   ITmdbProxy tmdbProxy,
                                   ISeriesService seriesService,
                                   Logger logger)
        {
            _skyHookProxy = skyHookProxy;
            _tmdbProxy = tmdbProxy;
            _seriesService = seriesService;
            _logger = logger;
        }

        public Tuple<Series, List<Episode>> GetSeriesInfo(int tvdbSeriesId, Language language, string seasonType)
        {
            // Negative TVDB IDs are placeholders for series that only exist on TMDB
            if (tvdbSeriesId < 0)
            {
                return _tmdbProxy.GetSeriesInfo(-tvdbSeriesId, language);
            }

            return _skyHookProxy.GetSeriesInfo(tvdbSeriesId, language, seasonType);
        }

        public Tuple<Series, List<Episode>> GetSeriesInfo(Series series)
        {
            if (GetMetadataSource(series) == MetadataSourceType.Tvdb)
            {
                return _skyHookProxy.GetSeriesInfo(series.TvdbId, series.Language, series.SeasonType);
            }

            var tmdbId = ResolveTmdbId(series);

            if (tmdbId <= 0)
            {
                throw new TmdbException("Unable to find a TMDB ID for series '{0}' (tvdbid {1})", series.Title, series.TvdbId);
            }

            return _tmdbProxy.GetSeriesInfo(tmdbId, series.Language);
        }

        public MetadataSourceType GetMetadataSource(Series series)
        {
            return series.TvdbId <= 0 ? MetadataSourceType.Tmdb : series.MetadataSource;
        }

        public int ResolveTmdbId(Series series)
        {
            if (series.TmdbId > 0)
            {
                return series.TmdbId;
            }

            if (series.TvdbId < 0)
            {
                return -series.TvdbId;
            }

            if (!_tmdbProxy.IsConfigured)
            {
                return 0;
            }

            var tmdbId = _tmdbProxy.FindTmdbIdByTvdbId(series.TvdbId);

            if (tmdbId <= 0 && series.ImdbId.IsNotNullOrWhiteSpace())
            {
                tmdbId = _tmdbProxy.FindTmdbIdByImdbId(series.ImdbId);
            }

            return tmdbId;
        }

        public List<Series> SearchForNewSeriesByImdbId(string imdbId, Language language)
        {
            var results = _skyHookProxy.SearchForNewSeriesByImdbId(imdbId, language);

            if (results.Any() || !_tmdbProxy.IsConfigured)
            {
                return results;
            }

            var normalizedImdbId = Parser.Parser.NormalizeImdbId(imdbId);
            var tmdbId = normalizedImdbId == null ? 0 : _tmdbProxy.FindTmdbIdByImdbId(normalizedImdbId);

            return tmdbId > 0 ? SearchTmdbById(tmdbId, language) : results;
        }

        public List<Series> SearchForNewSeriesByAniListId(int aniListId, Language language)
        {
            return _skyHookProxy.SearchForNewSeriesByAniListId(aniListId, language);
        }

        public List<Series> SearchForNewSeriesByMyAnimeListId(int malId, Language language)
        {
            return _skyHookProxy.SearchForNewSeriesByMyAnimeListId(malId, language);
        }

        public List<Series> SearchForNewSeriesByTmdbId(int tmdbId, Language language)
        {
            if (!_tmdbProxy.IsConfigured)
            {
                return _skyHookProxy.SearchForNewSeriesByTmdbId(tmdbId, language);
            }

            List<Series> skyHookResults;

            try
            {
                skyHookResults = _skyHookProxy.SearchForNewSeriesByTmdbId(tmdbId, language);
            }
            catch (SkyHookException ex)
            {
                _logger.Warn(ex, "SkyHook lookup for TMDB ID {0} failed, falling back to TMDB", tmdbId);
                skyHookResults = new List<Series>();
            }

            if (skyHookResults.Any())
            {
                skyHookResults.Where(s => s.TmdbId == 0).ToList().ForEach(s => s.TmdbId = tmdbId);

                return skyHookResults;
            }

            return SearchTmdbById(tmdbId, language);
        }

        public List<Series> SearchForNewSeries(string title, Language language)
        {
            if (!_tmdbProxy.IsConfigured || title.IsNullOrWhiteSpace())
            {
                return _skyHookProxy.SearchForNewSeries(title, language);
            }

            var term = title.Trim();
            var lowerTerm = term.ToLowerInvariant();

            if (TryGetIdTerm(lowerTerm, out var prefix, out var value))
            {
                switch (prefix)
                {
                    case "tmdb":
                    case "tmdbid":
                        return int.TryParse(value, out var tmdbId) && tmdbId > 0
                            ? SearchForNewSeriesByTmdbId(tmdbId, language)
                            : new List<Series>();
                    case "tvdb":
                    case "tvdbid":
                        // Negative TVDB IDs are placeholders for TMDB only series
                        if (int.TryParse(value, out var tvdbId) && tvdbId < 0)
                        {
                            return SearchTmdbById(-tvdbId, language);
                        }

                        return _skyHookProxy.SearchForNewSeries(term, language);
                    case "imdb":
                    case "imdbid":
                        return SearchForNewSeriesByImdbId(value, language);
                    default:
                        return _skyHookProxy.SearchForNewSeries(term, language);
                }
            }

            return SearchMerged(term, language);
        }

        private static bool TryGetIdTerm(string lowerTerm, out string prefix, out string value)
        {
            prefix = null;
            value = null;

            var index = lowerTerm.IndexOf(':');

            if (index <= 0)
            {
                return false;
            }

            var candidate = lowerTerm.Substring(0, index).Trim();

            if (!new[] { "tvdb", "tvdbid", "tmdb", "tmdbid", "imdb", "imdbid", "anilist", "mal" }.Contains(candidate))
            {
                return false;
            }

            prefix = candidate;
            value = lowerTerm.Substring(index + 1).Trim();

            return true;
        }

        private List<Series> SearchTmdbById(int tmdbId, Language language)
        {
            try
            {
                var tmdbSeries = _tmdbProxy.GetSeries(tmdbId, language);

                return new List<Series> { FindExisting(tmdbSeries) ?? tmdbSeries };
            }
            catch (SeriesNotFoundException)
            {
                return new List<Series>();
            }
        }

        private List<Series> SearchMerged(string title, Language language)
        {
            var skyHookTask = Task.Run(() => _skyHookProxy.SearchForNewSeries(title, language));
            var tmdbTask = Task.Run(() => _tmdbProxy.Search(title, language));

            try
            {
                Task.WaitAll(skyHookTask, tmdbTask);
            }
            catch (AggregateException)
            {
                // Handled below, a single failing source should not fail the search
            }

            if (tmdbTask.IsFaulted)
            {
                _logger.Warn(tmdbTask.Exception?.GetBaseException(), "TMDB search for '{0}' failed, only using TheTVDB results", title);

                if (skyHookTask.IsFaulted)
                {
                    throw skyHookTask.Exception.GetBaseException();
                }

                return skyHookTask.Result;
            }

            var skyHookResults = new List<Series>();

            if (skyHookTask.IsFaulted)
            {
                var exception = skyHookTask.Exception.GetBaseException();

                // Invalid search terms are rejected the same way regardless of the source
                if (exception is InvalidSearchTermException)
                {
                    throw exception;
                }

                _logger.Warn(exception, "TheTVDB search for '{0}' failed, only using TMDB results", title);
            }
            else
            {
                skyHookResults = skyHookTask.Result;
            }

            return Merge(skyHookResults, tmdbTask.Result, language);
        }

        private List<Series> Merge(List<Series> skyHookResults, List<Series> tmdbResults, Language language)
        {
            var entries = skyHookResults
                .Select((series, index) => new SearchEntry(series, index, 0))
                .ToList();

            var byTvdbId = new Dictionary<int, SearchEntry>();
            var byTmdbId = new Dictionary<int, SearchEntry>();

            foreach (var entry in entries)
            {
                byTvdbId.TryAdd(entry.Series.TvdbId, entry);

                if (entry.Series.TmdbId > 0)
                {
                    byTmdbId.TryAdd(entry.Series.TmdbId, entry);
                }
            }

            var unmatched = new List<(Series Result, int Index)>();

            for (var i = 0; i < tmdbResults.Count; i++)
            {
                var tmdbResult = tmdbResults[i];

                if (byTmdbId.TryGetValue(tmdbResult.TmdbId, out var match))
                {
                    match.Merge(tmdbResult, i);
                }
                else
                {
                    unmatched.Add((tmdbResult, i));
                }
            }

            // Search results don't include external IDs, fetch details to find out if the series is on TheTVDB as well
            var details = new Series[unmatched.Count];

            Parallel.For(0, Math.Min(unmatched.Count, MaxTmdbDetailLookups), new ParallelOptions { MaxDegreeOfParallelism = 5 }, i =>
            {
                try
                {
                    details[i] = _tmdbProxy.GetSeries(unmatched[i].Result.TmdbId, language);
                }
                catch (Exception ex)
                {
                    _logger.Debug(ex, "Unable to fetch TMDB details for {0}", unmatched[i].Result.TmdbId);
                }
            });

            for (var i = 0; i < unmatched.Count; i++)
            {
                var (tmdbResult, index) = unmatched[i];
                var series = details[i];

                if (series == null)
                {
                    // Not enough details to be added reliably, skip it
                    continue;
                }

                if (byTmdbId.TryGetValue(series.TmdbId, out var existingEntry) ||
                    (series.TvdbId > 0 && byTvdbId.TryGetValue(series.TvdbId, out existingEntry)))
                {
                    existingEntry.Merge(series, index);
                    byTmdbId.TryAdd(series.TmdbId, existingEntry);
                    continue;
                }

                // Series exists on TheTVDB too, use TheTVDB as the default source
                if (series.TvdbId > 0)
                {
                    series.MetadataSource = MetadataSourceType.Tvdb;
                }

                var entry = new SearchEntry(FindExisting(series) ?? series, index, 1);

                entries.Add(entry);
                byTvdbId.TryAdd(entry.Series.TvdbId, entry);
                byTmdbId.TryAdd(series.TmdbId, entry);
            }

            return entries
                .OrderBy(e => e.Rank)
                .ThenBy(e => e.Order)
                .Select(e => e.Series)
                .ToList();
        }

        private Series FindExisting(Series series)
        {
            var existing = _seriesService.FindByTvdbId(series.TvdbId);

            if (existing == null && series.TvdbId > 0 && series.TmdbId > 0)
            {
                // Might have been added as TMDB only before TMDB had the TVDB mapping
                existing = _seriesService.FindByTvdbId(-series.TmdbId);
            }

            return existing;
        }

        private class SearchEntry
        {
            public SearchEntry(Series series, int rank, int order)
            {
                Series = series;
                Rank = rank;
                Order = order;
            }

            public Series Series { get; }
            public int Rank { get; private set; }
            public int Order { get; }

            public void Merge(Series tmdbSeries, int tmdbRank)
            {
                Rank = Math.Min(Rank, tmdbRank);

                // Existing library series are returned as is
                if (Series.Id > 0)
                {
                    return;
                }

                if (Series.TmdbId == 0)
                {
                    Series.TmdbId = tmdbSeries.TmdbId;
                }

                if (Series.Overview.IsNullOrWhiteSpace())
                {
                    Series.Overview = tmdbSeries.Overview;
                }

                if (Series.Year == 0 && tmdbSeries.Year > 0)
                {
                    Series.Year = tmdbSeries.Year;
                    Series.FirstAired ??= tmdbSeries.FirstAired;
                }

                if ((Series.Ratings == null || Series.Ratings.Votes == 0) && tmdbSeries.Ratings is { Votes: > 0 })
                {
                    Series.Ratings = tmdbSeries.Ratings;
                }

                if (Series.Genres.Empty() && tmdbSeries.Genres.Any())
                {
                    Series.Genres = tmdbSeries.Genres;
                }

                foreach (var image in tmdbSeries.Images)
                {
                    if ((image.CoverType == MediaCoverTypes.Poster || image.CoverType == MediaCoverTypes.Fanart) &&
                        Series.Images.None(i => i.CoverType == image.CoverType))
                    {
                        Series.Images.Add(image);
                    }
                }
            }
        }
    }
}
