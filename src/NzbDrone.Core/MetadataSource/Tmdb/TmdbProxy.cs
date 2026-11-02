using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.DataAugmentation.DailySeries;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.MetadataSource.Tmdb.Resource;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MetadataSource.Tmdb
{
    public interface ITmdbProxy
    {
        bool IsConfigured { get; }
        Tuple<Series, List<Episode>> GetSeriesInfo(int tmdbId, Language language);
        Series GetSeries(int tmdbId, Language language);
        List<Series> Search(string title, Language language);
        int FindTmdbIdByTvdbId(int tvdbId);
        int FindTmdbIdByImdbId(string imdbId);
        bool ValidateApiKey(string apiKey);
    }

    public class TmdbProxy : ITmdbProxy
    {
        public const string ApiBaseUrl = "https://api.themoviedb.org/3";
        public const string ImageBaseUrl = "https://image.tmdb.org/t/p/original";

        private static readonly TimeSpan SeriesCacheLifetime = TimeSpan.FromHours(6);
        private static readonly TimeSpan FindCacheLifetime = TimeSpan.FromHours(24);

        private readonly IHttpClient _httpClient;
        private readonly IConfigService _configService;
        private readonly IDailySeriesService _dailySeriesService;
        private readonly ICached<TmdbShowResource> _showCache;
        private readonly ICached<string> _findCache;
        private readonly Logger _logger;

        public TmdbProxy(IHttpClient httpClient,
                         IConfigService configService,
                         IDailySeriesService dailySeriesService,
                         ICacheManager cacheManager,
                         Logger logger)
        {
            _httpClient = httpClient;
            _configService = configService;
            _dailySeriesService = dailySeriesService;
            _showCache = cacheManager.GetCache<TmdbShowResource>(GetType(), "shows");
            _findCache = cacheManager.GetCache<string>(GetType(), "find");
            _logger = logger;
        }

        public bool IsConfigured => _configService.TmdbApiKey.IsNotNullOrWhiteSpace();

        public Tuple<Series, List<Episode>> GetSeriesInfo(int tmdbId, Language language)
        {
            var show = GetShow(tmdbId, language);
            var series = MapSeries(show, language);

            var episodes = new List<Episode>();

            foreach (var season in show.Seasons.DistinctBy(s => s.SeasonNumber))
            {
                var seasonResource = Get<TmdbSeasonResource>($"tv/{tmdbId}/season/{season.SeasonNumber}", language, null, out var statusCode);

                if (seasonResource == null)
                {
                    _logger.Debug("Unable to fetch season {0} for TMDB series {1}, status code {2}", season.SeasonNumber, tmdbId, statusCode);
                    continue;
                }

                episodes.AddRange(seasonResource.Episodes.Select(MapEpisode));
            }

            MapFinales(show, series, episodes);
            MapAbsoluteEpisodeNumbers(episodes);

            // Keep the cache in sync with the latest data
            _showCache.Set(GetCacheKey(tmdbId, language), show, SeriesCacheLifetime);

            return new Tuple<Series, List<Episode>>(series, episodes);
        }

        public Series GetSeries(int tmdbId, Language language)
        {
            var show = _showCache.Get(GetCacheKey(tmdbId, language),
                () => GetShow(tmdbId, language),
                SeriesCacheLifetime);

            // Map on every call, callers are allowed to modify the returned series
            return MapSeries(show, language);
        }

        public List<Series> Search(string title, Language language)
        {
            var parameters = new Dictionary<string, string>
            {
                { "query", title.Trim() },
                { "include_adult", "false" },
                { "page", "1" }
            };

            var response = Get<TmdbSearchResponse>("search/tv", language, parameters, out var statusCode);

            if (response == null)
            {
                throw new TmdbException("Search for '{0}' failed. TMDB returned status code {1}", title, statusCode);
            }

            return response.Results.Select(MapSearchResult).ToList();
        }

        public int FindTmdbIdByTvdbId(int tvdbId)
        {
            if (tvdbId <= 0)
            {
                return 0;
            }

            return Find(tvdbId.ToString(CultureInfo.InvariantCulture), "tvdb_id");
        }

        public int FindTmdbIdByImdbId(string imdbId)
        {
            if (imdbId.IsNullOrWhiteSpace())
            {
                return 0;
            }

            return Find(imdbId, "imdb_id");
        }

        public bool ValidateApiKey(string apiKey)
        {
            if (apiKey.IsNullOrWhiteSpace())
            {
                return false;
            }

            try
            {
                var request = BuildRequest("authentication", null, null, apiKey);
                var response = _httpClient.Execute(request);

                return response.StatusCode != HttpStatusCode.Unauthorized && response.StatusCode != HttpStatusCode.Forbidden;
            }
            catch (Exception ex)
            {
                // Don't block saving the key because TMDB is temporarily unreachable
                _logger.Warn(ex, "Unable to validate TMDB API key");
                return true;
            }
        }

        private int Find(string externalId, string externalSource)
        {
            var cacheKey = $"{externalSource}:{externalId}";

            var cached = _findCache.Get(cacheKey, () => FindUncached(externalId, externalSource), FindCacheLifetime);

            return int.TryParse(cached, out var tmdbId) ? tmdbId : 0;
        }

        private string FindUncached(string externalId, string externalSource)
        {
            var parameters = new Dictionary<string, string> { { "external_source", externalSource } };
            var response = Get<TmdbFindResponse>($"find/{externalId}", null, parameters, out _);

            return (response?.TvResults.FirstOrDefault()?.Id ?? 0).ToString(CultureInfo.InvariantCulture);
        }

        private TmdbShowResource GetShow(int tmdbId, Language language)
        {
            var languageCode = GetLanguageCode(language);
            var imageLanguages = new[] { languageCode.Split('-')[0], "en", "null" }.Distinct().Join(",");

            var parameters = new Dictionary<string, string>
            {
                { "append_to_response", "external_ids,content_ratings,aggregate_credits,translations,images" },
                { "include_image_language", imageLanguages }
            };

            var show = Get<TmdbShowResource>($"tv/{tmdbId}", language, parameters, out var statusCode);

            if (show == null)
            {
                if (statusCode == HttpStatusCode.NotFound)
                {
                    throw new SeriesNotFoundException(-tmdbId, $"Series with TMDB ID {tmdbId} was not found, it may have been removed from TMDB.");
                }

                throw new TmdbException("Unable to fetch series with TMDB ID {0}. TMDB returned status code {1}", tmdbId, statusCode);
            }

            return show;
        }

        private T Get<T>(string resource, Language language, Dictionary<string, string> parameters, out HttpStatusCode statusCode)
            where T : class, new()
        {
            var apiKey = _configService.TmdbApiKey;

            if (apiKey.IsNullOrWhiteSpace())
            {
                throw new TmdbException("TMDB API key is not configured. Add it in Settings > Metadata Source.");
            }

            var request = BuildRequest(resource, language, parameters, apiKey);

            HttpResponse response;

            try
            {
                response = _httpClient.Execute(request);
            }
            catch (HttpException ex)
            {
                _logger.Warn(ex);
                throw new TmdbException("Unable to communicate with TMDB. {0}", ex, ex.Message);
            }
            catch (WebException ex)
            {
                _logger.Warn(ex);
                throw new TmdbException("Unable to communicate with TMDB. {0}", ex, ex.Message);
            }

            statusCode = response.StatusCode;

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new TmdbException("TMDB rejected the configured API key (401 Unauthorized)");
            }

            if (response.HasHttpError)
            {
                return null;
            }

            try
            {
                return STJson.Deserialize<T>(response.Content);
            }
            catch (Exception ex)
            {
                throw new TmdbException("Invalid response received from TMDB. {0}", ex, ex.Message);
            }
        }

        private HttpRequest BuildRequest(string resource, Language language, Dictionary<string, string> parameters, string apiKey)
        {
            var builder = new HttpRequestBuilder(ApiBaseUrl)
                .Resource(resource)
                .Accept(HttpAccept.Json);

            apiKey = apiKey.Trim();

            // v4 "API Read Access Tokens" are JWTs, v3 API keys are 32 character hex strings
            if (apiKey.StartsWith("eyJ"))
            {
                builder.SetHeader("Authorization", $"Bearer {apiKey}");
            }
            else
            {
                builder.AddQueryParam("api_key", apiKey);
            }

            if (language != null)
            {
                builder.AddQueryParam("language", GetLanguageCode(language));
            }

            if (parameters != null)
            {
                foreach (var parameter in parameters)
                {
                    builder.AddQueryParam(parameter.Key, parameter.Value);
                }
            }

            var request = builder.Build();

            request.AllowAutoRedirect = true;
            request.SuppressHttpError = true;
            request.LogResponseContent = false;

            return request;
        }

        private static string GetCacheKey(int tmdbId, Language language)
        {
            return $"{tmdbId}:{GetLanguageCode(language)}";
        }

        public static string GetLanguageCode(Language language)
        {
            var isoLanguage = IsoLanguages.Get(language ?? Language.English) ?? IsoLanguages.Get(Language.English);

            if (isoLanguage.CountryCode.IsNotNullOrWhiteSpace())
            {
                return $"{isoLanguage.TwoLetterCode}-{isoLanguage.CountryCode.ToUpperInvariant()}";
            }

            return isoLanguage.TwoLetterCode;
        }

        private Series MapSearchResult(TmdbSearchResultResource result)
        {
            var series = new Series
            {
                TmdbId = result.Id,
                Title = result.Name.IsNotNullOrWhiteSpace() ? result.Name : result.OriginalName,
                OriginalTitle = result.OriginalName,
                Overview = result.Overview,
                Ratings = new Ratings { Value = result.VoteAverage, Votes = result.VoteCount },
                OriginalCountry = MapCountry(result.OriginCountry.FirstOrDefault()),
                MetadataSource = MetadataSourceType.Tmdb
            };

            series.CleanTitle = series.Title?.CleanSeriesTitle();

            var firstAired = ParseDate(result.FirstAirDate);

            if (firstAired.HasValue)
            {
                series.FirstAired = firstAired;
                series.Year = firstAired.Value.Year;
            }

            AddImage(series.Images, MediaCoverTypes.Poster, result.PosterPath);
            AddImage(series.Images, MediaCoverTypes.Fanart, result.BackdropPath);

            return series;
        }

        private Series MapSeries(TmdbShowResource show, Language language)
        {
            var series = new Series();
            var tvdbId = show.ExternalIds?.TvdbId ?? 0;

            series.TmdbId = show.Id;
            series.TvdbId = tvdbId > 0 ? tvdbId : -show.Id;
            series.MetadataSource = MetadataSourceType.Tmdb;

            if (show.ExternalIds?.TvRageId is > 0)
            {
                series.TvRageId = show.ExternalIds.TvRageId.Value;
            }

            series.ImdbId = show.ExternalIds?.ImdbId.IsNotNullOrWhiteSpace() == true ? show.ExternalIds.ImdbId : null;

            series.Title = show.Name.IsNotNullOrWhiteSpace() ? show.Name : show.OriginalName;
            series.CleanTitle = series.Title.CleanSeriesTitle();
            series.SortTitle = SeriesTitleNormalizer.Normalize(series.Title, series.TvdbId);
            series.TitleSlug = $"{series.Title.ToUrlSlug()}-tmdb-{show.Id}".TrimStart('-');

            if (show.OriginalName.IsNotNullOrWhiteSpace())
            {
                series.OriginalTitle = show.OriginalName;

                var cleanOriginalTitle = show.OriginalName.CleanSeriesTitle();
                series.CleanOriginalTitle = cleanOriginalTitle.IsNotNullOrWhiteSpace() ? cleanOriginalTitle : null;
            }

            series.OriginalLanguage = show.OriginalLanguage.IsNotNullOrWhiteSpace()
                ? IsoLanguages.Find(show.OriginalLanguage.ToLowerInvariant())?.Language ?? Language.English
                : Language.English;

            series.FirstAired = ParseDate(show.FirstAirDate);

            if (series.FirstAired.HasValue)
            {
                series.Year = series.FirstAired.Value.Year;
            }

            series.Overview = show.Overview;
            series.Runtime = show.EpisodeRunTime.FirstOrDefault(r => r > 0);

            if (series.Runtime == 0)
            {
                series.Runtime = show.LastEpisodeToAir?.Runtime ?? show.NextEpisodeToAir?.Runtime ?? 0;
            }

            series.Network = show.Networks.FirstOrDefault()?.Name;
            series.Status = MapSeriesStatus(show);

            if (series.Status == SeriesStatusType.Ended)
            {
                series.LastAired = ParseDate(show.LastAirDate);
            }

            series.Ratings = new Ratings { Value = show.VoteAverage, Votes = show.VoteCount };
            series.Genres = show.Genres.Select(g => g.Name).Where(g => g.IsNotNullOrWhiteSpace()).ToList();
            series.OriginalCountry = MapCountry(show.OriginCountry.FirstOrDefault());
            series.Certification = MapCertification(show);

            if (tvdbId > 0 && _dailySeriesService.IsDailySeries(tvdbId))
            {
                series.SeriesType = SeriesTypes.Daily;
            }

            series.Actors = show.AggregateCredits?.Cast
                .OrderBy(c => c.Order)
                .Take(50)
                .Select(MapActor)
                .ToList() ?? new List<Actor>();

            series.Seasons = show.Seasons
                .DistinctBy(s => s.SeasonNumber)
                .Select(MapSeason)
                .ToList();

            AddImage(series.Images, MediaCoverTypes.Poster, show.PosterPath);
            AddImage(series.Images, MediaCoverTypes.Fanart, show.BackdropPath);
            AddImage(series.Images, MediaCoverTypes.Clearlogo, SelectLogo(show, language));

            series.Monitored = true;
            series.SeasonType = SeasonType.Official;
            series.Translations = MapTranslations(show);

            return series;
        }

        private static SeriesStatusType MapSeriesStatus(TmdbShowResource show)
        {
            var status = show.Status ?? string.Empty;

            if (status.Equals("Ended", StringComparison.InvariantCultureIgnoreCase) ||
                status.Equals("Canceled", StringComparison.InvariantCultureIgnoreCase) ||
                status.Equals("Cancelled", StringComparison.InvariantCultureIgnoreCase))
            {
                return SeriesStatusType.Ended;
            }

            if (show.LastEpisodeToAir == null &&
                (status.Equals("Planned", StringComparison.InvariantCultureIgnoreCase) ||
                 status.Equals("In Production", StringComparison.InvariantCultureIgnoreCase) ||
                 status.Equals("Pilot", StringComparison.InvariantCultureIgnoreCase)))
            {
                return SeriesStatusType.Upcoming;
            }

            return SeriesStatusType.Continuing;
        }

        private static string MapCertification(TmdbShowResource show)
        {
            var ratings = show.ContentRatings?.Results ?? new List<TmdbContentRatingResource>();

            var rating = ratings.FirstOrDefault(r => r.Country == "US" && r.Rating.IsNotNullOrWhiteSpace()) ??
                         ratings.FirstOrDefault(r => show.OriginCountry.Contains(r.Country) && r.Rating.IsNotNullOrWhiteSpace());

            return rating?.Rating.ToUpperInvariant();
        }

        private static string MapCountry(string twoLetterCountryCode)
        {
            if (twoLetterCountryCode.IsNullOrWhiteSpace())
            {
                return null;
            }

            // Skyhook uses ISO 3166-1 alpha-3 codes, TMDB uses alpha-2, convert for consistency
            try
            {
                return new RegionInfo(twoLetterCountryCode).ThreeLetterISORegionName.ToLowerInvariant();
            }
            catch (Exception)
            {
                return twoLetterCountryCode.ToLowerInvariant();
            }
        }

        private static string SelectLogo(TmdbShowResource show, Language language)
        {
            var logos = show.Images?.Logos ?? new List<TmdbImageResource>();

            if (logos.Empty())
            {
                return null;
            }

            var languageCode = GetLanguageCode(language).Split('-')[0];

            return (logos.Where(l => l.Language == languageCode).MaxBy(l => l.VoteAverage) ??
                    logos.Where(l => l.Language == "en").MaxBy(l => l.VoteAverage) ??
                    logos.MaxBy(l => l.VoteAverage))?.FilePath;
        }

        private static Actor MapActor(TmdbCastMemberResource cast)
        {
            var actor = new Actor
            {
                Name = cast.Name,
                Character = cast.Roles.FirstOrDefault(r => r.Character.IsNotNullOrWhiteSpace())?.Character
            };

            if (cast.ProfilePath.IsNotNullOrWhiteSpace())
            {
                actor.Images = new List<MediaCover.MediaCover>
                {
                    new(MediaCoverTypes.Headshot, ImageBaseUrl + cast.ProfilePath)
                };
            }

            return actor;
        }

        private static Season MapSeason(TmdbSeasonSummaryResource season)
        {
            var result = new Season
            {
                SeasonNumber = season.SeasonNumber,
                Title = season.Name,
                Monitored = season.SeasonNumber > 0
            };

            AddImage(result.Images, MediaCoverTypes.Poster, season.PosterPath);

            return result;
        }

        private static Episode MapEpisode(TmdbEpisodeResource resource)
        {
            var episode = new Episode
            {
                // TMDB episodes do not carry a TVDB ID, Sonarr matches episodes by season/episode number
                TvdbId = 0,
                SeasonNumber = resource.SeasonNumber,
                EpisodeNumber = resource.EpisodeNumber,
                Title = resource.Name,
                Overview = resource.Overview,
                Runtime = resource.Runtime ?? 0,
                Ratings = new Ratings { Value = resource.VoteAverage, Votes = resource.VoteCount }
            };

            var airDate = ParseDate(resource.AirDate);

            if (airDate.HasValue)
            {
                episode.AirDate = airDate.Value.ToString(Episode.AIR_DATE_FORMAT, CultureInfo.InvariantCulture);

                // TMDB does not provide air times, use the start of the day (UTC)
                episode.AirDateUtc = airDate.Value;
            }

            if (resource.StillPath.IsNotNullOrWhiteSpace())
            {
                episode.Images.Add(new MediaCover.MediaCover(MediaCoverTypes.Screenshot, ImageBaseUrl + resource.StillPath));
            }

            episode.FinaleType = resource.EpisodeType?.ToLowerInvariant() switch
            {
                "finale" => "season",
                "mid_season" => "midseason",
                _ => null
            };

            return episode;
        }

        private static void MapFinales(TmdbShowResource show, Series series, List<Episode> episodes)
        {
            if (series.Status != SeriesStatusType.Ended)
            {
                return;
            }

            var lastEpisode = episodes
                .Where(e => e.SeasonNumber > 0)
                .OrderBy(e => e.SeasonNumber)
                .ThenBy(e => e.EpisodeNumber)
                .LastOrDefault();

            if (lastEpisode != null && (lastEpisode.FinaleType != null || show.LastEpisodeToAir?.Id > 0))
            {
                lastEpisode.FinaleType = "series";
            }
        }

        private static void MapAbsoluteEpisodeNumbers(List<Episode> episodes)
        {
            var absoluteEpisodeNumber = 1;

            foreach (var episode in episodes.Where(e => e.SeasonNumber > 0).OrderBy(e => e.SeasonNumber).ThenBy(e => e.EpisodeNumber))
            {
                episode.AbsoluteEpisodeNumber = absoluteEpisodeNumber++;
            }
        }

        private static List<SeriesTranslation> MapTranslations(TmdbShowResource show)
        {
            var result = new List<SeriesTranslation>();
            var translations = show.Translations?.Translations ?? new List<TmdbTranslationResource>();

            foreach (var translation in translations)
            {
                if (translation.Language.IsNullOrWhiteSpace())
                {
                    continue;
                }

                var isoLanguage = translation.Country.IsNotNullOrWhiteSpace()
                    ? IsoLanguages.Find($"{translation.Language}-{translation.Country}")
                    : IsoLanguages.Find(translation.Language);

                isoLanguage ??= IsoLanguages.Find(translation.Language);

                var title = translation.Data?.Name;

                if (title.IsNullOrWhiteSpace() && translation.Language == show.OriginalLanguage)
                {
                    title = show.OriginalName;
                }

                if (isoLanguage == null || title.IsNullOrWhiteSpace() || result.Any(r => r.Language == isoLanguage.Language))
                {
                    continue;
                }

                result.Add(new SeriesTranslation
                {
                    Language = isoLanguage.Language,
                    Title = title,
                    CleanTitle = title.CleanSeriesTitle(),
                    Overview = translation.Data?.Overview
                });
            }

            return result;
        }

        private static void AddImage(List<MediaCover.MediaCover> images, MediaCoverTypes coverType, string path)
        {
            if (path.IsNullOrWhiteSpace())
            {
                return;
            }

            images.Add(new MediaCover.MediaCover
            {
                CoverType = coverType,
                RemoteUrl = ImageBaseUrl + path
            });
        }

        private static DateTime? ParseDate(string date)
        {
            if (date.IsNullOrWhiteSpace())
            {
                return null;
            }

            if (DateTime.TryParseExact(date, "yyyy-MM-dd", DateTimeFormatInfo.InvariantInfo, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var result))
            {
                return result;
            }

            return null;
        }
    }
}
