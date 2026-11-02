using System.Linq;
using System.Net;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.MetadataSource.Tmdb;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.MetadataSource.Tmdb
{
    [TestFixture]
    public class TmdbProxyFixture : CoreTest<TmdbProxy>
    {
        private const string ShowJson = @"{
  ""id"": 1396,
  ""name"": ""Breaking Bad"",
  ""original_name"": ""Breaking Bad"",
  ""overview"": ""Walter White, a chemistry teacher..."",
  ""status"": ""Ended"",
  ""first_air_date"": ""2008-01-20"",
  ""last_air_date"": ""2013-09-29"",
  ""episode_run_time"": [45],
  ""genres"": [{ ""id"": 18, ""name"": ""Drama"" }],
  ""networks"": [{ ""id"": 174, ""name"": ""AMC"", ""origin_country"": ""US"" }],
  ""origin_country"": [""US""],
  ""original_language"": ""en"",
  ""poster_path"": ""/poster.jpg"",
  ""backdrop_path"": ""/backdrop.jpg"",
  ""vote_average"": 8.9,
  ""vote_count"": 15000,
  ""seasons"": [
    { ""id"": 3577, ""season_number"": 0, ""name"": ""Specials"", ""poster_path"": null, ""episode_count"": 1 },
    { ""id"": 3572, ""season_number"": 1, ""name"": ""Season 1"", ""poster_path"": ""/s1.jpg"", ""episode_count"": 2 }
  ],
  ""last_episode_to_air"": { ""id"": 62161, ""name"": ""Felina"", ""episode_number"": 16, ""season_number"": 5, ""runtime"": 55 },
  ""next_episode_to_air"": null,
  ""external_ids"": { ""imdb_id"": ""tt0903747"", ""tvdb_id"": 81189, ""tvrage_id"": 18164 },
  ""content_ratings"": { ""results"": [{ ""iso_3166_1"": ""DE"", ""rating"": ""16"" }, { ""iso_3166_1"": ""US"", ""rating"": ""tv-ma"" }] },
  ""aggregate_credits"": { ""cast"": [{ ""name"": ""Bryan Cranston"", ""profile_path"": ""/bc.jpg"", ""order"": 0, ""roles"": [{ ""character"": ""Walter White"" }] }] },
  ""translations"": { ""translations"": [
    { ""iso_3166_1"": ""US"", ""iso_639_1"": ""en"", ""data"": { ""name"": """", ""overview"": ""Walter White..."" } },
    { ""iso_3166_1"": ""DE"", ""iso_639_1"": ""de"", ""data"": { ""name"": ""Breaking Bad DE"", ""overview"": ""Walter White DE"" } }
  ] },
  ""images"": { ""logos"": [{ ""file_path"": ""/logo.png"", ""iso_639_1"": ""en"", ""vote_average"": 5.3 }] }
}";

        private const string Season0Json = @"{ ""id"": 3577, ""season_number"": 0, ""name"": ""Specials"", ""episodes"": [
  { ""id"": 1, ""name"": ""Special"", ""overview"": """", ""air_date"": ""2009-02-17"", ""episode_number"": 1, ""season_number"": 0, ""episode_type"": ""standard"", ""runtime"": null, ""still_path"": null, ""vote_average"": 0, ""vote_count"": 0 }
] }";

        private const string Season1Json = @"{ ""id"": 3572, ""season_number"": 1, ""name"": ""Season 1"", ""episodes"": [
  { ""id"": 62085, ""name"": ""Pilot"", ""overview"": ""Pilot overview"", ""air_date"": ""2008-01-20"", ""episode_number"": 1, ""season_number"": 1, ""episode_type"": ""standard"", ""runtime"": 59, ""still_path"": ""/still.jpg"", ""vote_average"": 8.3, ""vote_count"": 300 },
  { ""id"": 62086, ""name"": ""Cat's in the Bag..."", ""overview"": """", ""air_date"": ""2008-01-27"", ""episode_number"": 2, ""season_number"": 1, ""episode_type"": ""finale"", ""runtime"": 49, ""still_path"": null, ""vote_average"": 8, ""vote_count"": 250 },
  { ""id"": 62087, ""name"": ""Unaired"", ""overview"": """", ""air_date"": null, ""episode_number"": 3, ""season_number"": 1, ""episode_type"": ""standard"", ""runtime"": null, ""still_path"": null, ""vote_average"": 0, ""vote_count"": 0 }
] }";

        private const string SearchJson = @"{ ""page"": 1, ""total_results"": 1, ""results"": [
  { ""id"": 1396, ""name"": ""Breaking Bad"", ""original_name"": ""Breaking Bad"", ""overview"": ""..."", ""first_air_date"": ""2008-01-20"", ""poster_path"": ""/poster.jpg"", ""backdrop_path"": null, ""vote_average"": 8.9, ""vote_count"": 15000, ""original_language"": ""en"", ""origin_country"": [""US""] }
] }";

        [SetUp]
        public void Setup()
        {
            Mocker.SetConstant<ICacheManager>(new CacheManager());

            Mocker.GetMock<IConfigService>()
                  .SetupGet(s => s.TmdbApiKey)
                  .Returns("0123456789abcdef0123456789abcdef");

            GivenResponse("/tv/1396/season/0", Season0Json);
            GivenResponse("/tv/1396/season/1", Season1Json);
            GivenResponse("/tv/1396?", ShowJson);
            GivenResponse("/search/tv", SearchJson);
            GivenResponse("/tv/404?", "{}", HttpStatusCode.NotFound);
        }

        private void GivenResponse(string urlPart, string content, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            Mocker.GetMock<IHttpClient>()
                  .Setup(s => s.Execute(It.Is<HttpRequest>(r => r.Url.FullUri.Contains(urlPart))))
                  .Returns<HttpRequest>(r => new HttpResponse(r, new HttpHeader(), content, statusCode));
        }

        [Test]
        public void should_map_series_from_tmdb()
        {
            var series = Subject.GetSeriesInfo(1396, Language.English).Item1;

            series.TmdbId.Should().Be(1396);
            series.TvdbId.Should().Be(81189);
            series.ImdbId.Should().Be("tt0903747");
            series.TvRageId.Should().Be(18164);
            series.MetadataSource.Should().Be(MetadataSourceType.Tmdb);
            series.Title.Should().Be("Breaking Bad");
            series.Year.Should().Be(2008);
            series.Status.Should().Be(SeriesStatusType.Ended);
            series.Network.Should().Be("AMC");
            series.Runtime.Should().Be(45);
            series.Certification.Should().Be("TV-MA");
            series.OriginalCountry.Should().Be("usa");
            series.OriginalLanguage.Should().Be(Language.English);
            series.Genres.Should().BeEquivalentTo("Drama");
            series.Ratings.Votes.Should().Be(15000);
            series.Actors.Should().ContainSingle(a => a.Name == "Bryan Cranston" && a.Character == "Walter White");
            series.Seasons.Select(s => s.SeasonNumber).Should().BeEquivalentTo(new[] { 0, 1 });
            series.Seasons.Single(s => s.SeasonNumber == 0).Monitored.Should().BeFalse();
            series.Images.Should().Contain(i => i.CoverType == MediaCoverTypes.Poster && i.RemoteUrl == TmdbProxy.ImageBaseUrl + "/poster.jpg");
            series.Images.Should().Contain(i => i.CoverType == MediaCoverTypes.Clearlogo);
            series.Translations.Should().Contain(t => t.Language == Language.German && t.Title == "Breaking Bad DE");
            series.TitleSlug.Should().Be("breaking-bad-tmdb-1396");
        }

        [Test]
        public void should_map_episodes_from_tmdb()
        {
            var episodes = Subject.GetSeriesInfo(1396, Language.English).Item2;

            episodes.Should().HaveCount(4);

            var pilot = episodes.Single(e => e.SeasonNumber == 1 && e.EpisodeNumber == 1);
            pilot.Title.Should().Be("Pilot");
            pilot.AirDate.Should().Be("2008-01-20");
            pilot.AirDateUtc.Should().NotBeNull();
            pilot.Runtime.Should().Be(59);
            pilot.AbsoluteEpisodeNumber.Should().Be(1);
            pilot.Images.Should().ContainSingle(i => i.CoverType == MediaCoverTypes.Screenshot);

            episodes.Single(e => e.SeasonNumber == 1 && e.EpisodeNumber == 2).FinaleType.Should().Be("season");
            episodes.Single(e => e.SeasonNumber == 1 && e.EpisodeNumber == 3).AirDate.Should().BeNull();
            episodes.Single(e => e.SeasonNumber == 0).AbsoluteEpisodeNumber.Should().BeNull();
        }

        [Test]
        public void should_use_placeholder_tvdb_id_when_tmdb_has_no_tvdb_mapping()
        {
            GivenResponse("/tv/1396?", ShowJson.Replace(@"""tvdb_id"": 81189", @"""tvdb_id"": null"));

            var series = Subject.GetSeries(1396, Language.English);

            series.TvdbId.Should().Be(-1396);
        }

        [Test]
        public void should_throw_series_not_found_for_missing_series()
        {
            Assert.Throws<SeriesNotFoundException>(() => Subject.GetSeriesInfo(404, Language.English));
        }

        [Test]
        public void should_map_search_results()
        {
            var results = Subject.Search("breaking bad", Language.English);

            results.Should().ContainSingle();
            results.First().TmdbId.Should().Be(1396);
            results.First().Year.Should().Be(2008);
        }

        [Test]
        public void should_send_language_and_api_key()
        {
            Subject.Search("breaking bad", Language.German);

            Mocker.GetMock<IHttpClient>()
                  .Verify(v => v.Execute(It.Is<HttpRequest>(r => r.Url.FullUri.Contains("language=de-DE") && r.Url.FullUri.Contains("api_key="))), Times.Once());
        }

        [Test]
        public void should_use_bearer_token_for_read_access_tokens()
        {
            Mocker.GetMock<IConfigService>()
                  .SetupGet(s => s.TmdbApiKey)
                  .Returns("eyJhbGciOiJIUzI1NiJ9.token");

            Subject.Search("breaking bad", Language.English);

            Mocker.GetMock<IHttpClient>()
                  .Verify(v => v.Execute(It.Is<HttpRequest>(r => !r.Url.FullUri.Contains("api_key=") && r.Headers.GetSingleValue("Authorization") == "Bearer eyJhbGciOiJIUzI1NiJ9.token")), Times.Once());
        }
    }
}
