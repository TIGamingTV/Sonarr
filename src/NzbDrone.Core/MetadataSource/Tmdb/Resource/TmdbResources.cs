using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NzbDrone.Core.MetadataSource.Tmdb.Resource
{
    public class TmdbSearchResponse
    {
        public int Page { get; set; }
        public List<TmdbSearchResultResource> Results { get; set; } = new();

        [JsonPropertyName("total_results")]
        public int TotalResults { get; set; }
    }

    public class TmdbSearchResultResource
    {
        public int Id { get; set; }
        public string Name { get; set; }

        [JsonPropertyName("original_name")]
        public string OriginalName { get; set; }

        public string Overview { get; set; }

        [JsonPropertyName("first_air_date")]
        public string FirstAirDate { get; set; }

        [JsonPropertyName("poster_path")]
        public string PosterPath { get; set; }

        [JsonPropertyName("backdrop_path")]
        public string BackdropPath { get; set; }

        [JsonPropertyName("vote_average")]
        public decimal VoteAverage { get; set; }

        [JsonPropertyName("vote_count")]
        public int VoteCount { get; set; }

        [JsonPropertyName("original_language")]
        public string OriginalLanguage { get; set; }

        [JsonPropertyName("origin_country")]
        public List<string> OriginCountry { get; set; } = new();
    }

    public class TmdbFindResponse
    {
        [JsonPropertyName("tv_results")]
        public List<TmdbSearchResultResource> TvResults { get; set; } = new();
    }

    public class TmdbShowResource
    {
        public int Id { get; set; }
        public string Name { get; set; }

        [JsonPropertyName("original_name")]
        public string OriginalName { get; set; }

        public string Overview { get; set; }
        public string Status { get; set; }

        [JsonPropertyName("first_air_date")]
        public string FirstAirDate { get; set; }

        [JsonPropertyName("last_air_date")]
        public string LastAirDate { get; set; }

        [JsonPropertyName("episode_run_time")]
        public List<int> EpisodeRunTime { get; set; } = new();

        public List<TmdbNamedResource> Genres { get; set; } = new();
        public List<TmdbNamedResource> Networks { get; set; } = new();

        [JsonPropertyName("origin_country")]
        public List<string> OriginCountry { get; set; } = new();

        [JsonPropertyName("original_language")]
        public string OriginalLanguage { get; set; }

        [JsonPropertyName("poster_path")]
        public string PosterPath { get; set; }

        [JsonPropertyName("backdrop_path")]
        public string BackdropPath { get; set; }

        [JsonPropertyName("vote_average")]
        public decimal VoteAverage { get; set; }

        [JsonPropertyName("vote_count")]
        public int VoteCount { get; set; }

        public List<TmdbSeasonSummaryResource> Seasons { get; set; } = new();

        [JsonPropertyName("last_episode_to_air")]
        public TmdbEpisodeResource LastEpisodeToAir { get; set; }

        [JsonPropertyName("next_episode_to_air")]
        public TmdbEpisodeResource NextEpisodeToAir { get; set; }

        [JsonPropertyName("external_ids")]
        public TmdbExternalIdsResource ExternalIds { get; set; }

        [JsonPropertyName("content_ratings")]
        public TmdbResultsResource<TmdbContentRatingResource> ContentRatings { get; set; }

        [JsonPropertyName("aggregate_credits")]
        public TmdbAggregateCreditsResource AggregateCredits { get; set; }

        public TmdbTranslationsResource Translations { get; set; }

        public TmdbImagesResource Images { get; set; }
    }

    public class TmdbNamedResource
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class TmdbSeasonSummaryResource
    {
        public int Id { get; set; }

        [JsonPropertyName("season_number")]
        public int SeasonNumber { get; set; }

        public string Name { get; set; }

        [JsonPropertyName("poster_path")]
        public string PosterPath { get; set; }

        [JsonPropertyName("episode_count")]
        public int EpisodeCount { get; set; }
    }

    public class TmdbSeasonResource
    {
        public int Id { get; set; }

        [JsonPropertyName("season_number")]
        public int SeasonNumber { get; set; }

        public string Name { get; set; }
        public List<TmdbEpisodeResource> Episodes { get; set; } = new();
    }

    public class TmdbEpisodeResource
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Overview { get; set; }

        [JsonPropertyName("air_date")]
        public string AirDate { get; set; }

        [JsonPropertyName("episode_number")]
        public int EpisodeNumber { get; set; }

        [JsonPropertyName("season_number")]
        public int SeasonNumber { get; set; }

        [JsonPropertyName("episode_type")]
        public string EpisodeType { get; set; }

        public int? Runtime { get; set; }

        [JsonPropertyName("still_path")]
        public string StillPath { get; set; }

        [JsonPropertyName("vote_average")]
        public decimal VoteAverage { get; set; }

        [JsonPropertyName("vote_count")]
        public int VoteCount { get; set; }
    }

    public class TmdbExternalIdsResource
    {
        [JsonPropertyName("imdb_id")]
        public string ImdbId { get; set; }

        [JsonPropertyName("tvdb_id")]
        public int? TvdbId { get; set; }

        [JsonPropertyName("tvrage_id")]
        public int? TvRageId { get; set; }
    }

    public class TmdbResultsResource<T>
    {
        public List<T> Results { get; set; } = new();
    }

    public class TmdbContentRatingResource
    {
        [JsonPropertyName("iso_3166_1")]
        public string Country { get; set; }

        public string Rating { get; set; }
    }

    public class TmdbAggregateCreditsResource
    {
        public List<TmdbCastMemberResource> Cast { get; set; } = new();
    }

    public class TmdbCastMemberResource
    {
        public string Name { get; set; }

        [JsonPropertyName("profile_path")]
        public string ProfilePath { get; set; }

        public int Order { get; set; }
        public List<TmdbRoleResource> Roles { get; set; } = new();
    }

    public class TmdbRoleResource
    {
        public string Character { get; set; }
    }

    public class TmdbTranslationsResource
    {
        public List<TmdbTranslationResource> Translations { get; set; } = new();
    }

    public class TmdbTranslationResource
    {
        [JsonPropertyName("iso_3166_1")]
        public string Country { get; set; }

        [JsonPropertyName("iso_639_1")]
        public string Language { get; set; }

        public TmdbTranslationDataResource Data { get; set; }
    }

    public class TmdbTranslationDataResource
    {
        public string Name { get; set; }
        public string Overview { get; set; }
    }

    public class TmdbImagesResource
    {
        public List<TmdbImageResource> Logos { get; set; } = new();
    }

    public class TmdbImageResource
    {
        [JsonPropertyName("file_path")]
        public string FilePath { get; set; }

        [JsonPropertyName("iso_639_1")]
        public string Language { get; set; }

        [JsonPropertyName("vote_average")]
        public decimal VoteAverage { get; set; }
    }
}
