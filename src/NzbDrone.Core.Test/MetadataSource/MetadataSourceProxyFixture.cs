using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.MetadataSource.SkyHook;
using NzbDrone.Core.MetadataSource.Tmdb;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MetadataSource
{
    [TestFixture]
    public class MetadataSourceProxyFixture : CoreTest<MetadataSourceProxy>
    {
        private List<Series> _skyHookResults;
        private List<Series> _tmdbResults;

        [SetUp]
        public void Setup()
        {
            _skyHookResults = new List<Series>();
            _tmdbResults = new List<Series>();

            Mocker.GetMock<ITmdbProxy>()
                  .SetupGet(s => s.IsConfigured)
                  .Returns(true);

            Mocker.GetMock<ISkyHookProxy>()
                  .Setup(s => s.SearchForNewSeries(It.IsAny<string>(), It.IsAny<Language>()))
                  .Returns(() => _skyHookResults);

            Mocker.GetMock<ITmdbProxy>()
                  .Setup(s => s.Search(It.IsAny<string>(), It.IsAny<Language>()))
                  .Returns(() => _tmdbResults);
        }

        private static Series TvdbSeries(int tvdbId, int tmdbId, string title)
        {
            return new Series { TvdbId = tvdbId, TmdbId = tmdbId, Title = title };
        }

        private static Series TmdbSeries(int tmdbId, string title)
        {
            return new Series { TmdbId = tmdbId, Title = title, MetadataSource = MetadataSourceType.Tmdb };
        }

        private void GivenTmdbDetails(int tmdbId, int tvdbId, string title)
        {
            Mocker.GetMock<ITmdbProxy>()
                  .Setup(s => s.GetSeries(tmdbId, It.IsAny<Language>()))
                  .Returns(() => new Series
                  {
                      TmdbId = tmdbId,
                      TvdbId = tvdbId > 0 ? tvdbId : -tmdbId,
                      Title = title,
                      MetadataSource = MetadataSourceType.Tmdb
                  });
        }

        [Test]
        public void should_only_use_skyhook_when_tmdb_is_not_configured()
        {
            Mocker.GetMock<ITmdbProxy>()
                  .SetupGet(s => s.IsConfigured)
                  .Returns(false);

            _skyHookResults.Add(TvdbSeries(1, 10, "Series"));

            var results = Subject.SearchForNewSeries("series", Language.English);

            results.Should().HaveCount(1);

            Mocker.GetMock<ITmdbProxy>()
                  .Verify(v => v.Search(It.IsAny<string>(), It.IsAny<Language>()), Times.Never());
        }

        [Test]
        public void should_merge_results_existing_on_both_by_tmdb_id()
        {
            _skyHookResults.Add(TvdbSeries(1, 10, "Series"));
            _tmdbResults.Add(TmdbSeries(10, "Series"));

            var results = Subject.SearchForNewSeries("series", Language.English);

            results.Should().HaveCount(1);
            results.First().TvdbId.Should().Be(1);
            results.First().TmdbId.Should().Be(10);
            results.First().MetadataSource.Should().Be(MetadataSourceType.Tvdb);
        }

        [Test]
        public void should_merge_results_existing_on_both_by_tvdb_id_from_tmdb_details()
        {
            _skyHookResults.Add(TvdbSeries(1, 0, "Series"));
            _tmdbResults.Add(TmdbSeries(10, "Series"));
            GivenTmdbDetails(10, 1, "Series");

            var results = Subject.SearchForNewSeries("series", Language.English);

            results.Should().HaveCount(1);
            results.First().TvdbId.Should().Be(1);
            results.First().TmdbId.Should().Be(10);
        }

        [Test]
        public void should_add_tmdb_only_result_with_placeholder_tvdb_id()
        {
            _skyHookResults.Add(TvdbSeries(1, 10, "Series"));
            _tmdbResults.Add(TmdbSeries(10, "Series"));
            _tmdbResults.Add(TmdbSeries(20, "Other Series"));
            GivenTmdbDetails(20, 0, "Other Series");

            var results = Subject.SearchForNewSeries("series", Language.English);

            results.Should().HaveCount(2);

            var tmdbOnly = results.Single(r => r.TmdbId == 20);
            tmdbOnly.TvdbId.Should().Be(-20);
            tmdbOnly.MetadataSource.Should().Be(MetadataSourceType.Tmdb);
        }

        [Test]
        public void should_add_tmdb_result_found_on_tvdb_as_tvdb_series()
        {
            _tmdbResults.Add(TmdbSeries(20, "Other Series"));
            GivenTmdbDetails(20, 2, "Other Series");

            var results = Subject.SearchForNewSeries("series", Language.English);

            results.Should().HaveCount(1);
            results.First().TvdbId.Should().Be(2);
            results.First().MetadataSource.Should().Be(MetadataSourceType.Tvdb);
        }

        [Test]
        public void should_return_skyhook_results_if_tmdb_search_fails()
        {
            _skyHookResults.Add(TvdbSeries(1, 10, "Series"));

            Mocker.GetMock<ITmdbProxy>()
                  .Setup(s => s.Search(It.IsAny<string>(), It.IsAny<Language>()))
                  .Throws(new TmdbException("failed"));

            var results = Subject.SearchForNewSeries("series", Language.English);

            results.Should().HaveCount(1);

            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public void should_return_tmdb_results_if_skyhook_search_fails()
        {
            _tmdbResults.Add(TmdbSeries(20, "Other Series"));
            GivenTmdbDetails(20, 0, "Other Series");

            Mocker.GetMock<ISkyHookProxy>()
                  .Setup(s => s.SearchForNewSeries(It.IsAny<string>(), It.IsAny<Language>()))
                  .Throws(new SkyHookException("failed"));

            var results = Subject.SearchForNewSeries("series", Language.English);

            results.Should().HaveCount(1);
            results.First().TvdbId.Should().Be(-20);

            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public void should_search_tmdb_for_negative_tvdb_id()
        {
            GivenTmdbDetails(20, 0, "Other Series");

            var results = Subject.SearchForNewSeries("tvdb:-20", Language.English);

            results.Should().HaveCount(1);
            results.First().TmdbId.Should().Be(20);
        }

        [Test]
        public void should_fall_back_to_tmdb_for_tmdb_id_unknown_to_skyhook()
        {
            Mocker.GetMock<ISkyHookProxy>()
                  .Setup(s => s.SearchForNewSeriesByTmdbId(20, It.IsAny<Language>()))
                  .Returns(new List<Series>());

            GivenTmdbDetails(20, 0, "Other Series");

            var results = Subject.SearchForNewSeriesByTmdbId(20, Language.English);

            results.Should().HaveCount(1);
            results.First().TvdbId.Should().Be(-20);
        }

        [Test]
        public void should_route_series_info_by_metadata_source()
        {
            var series = new Series { TvdbId = 1, TmdbId = 10, MetadataSource = MetadataSourceType.Tmdb, Language = Language.English };

            Mocker.GetMock<ITmdbProxy>()
                  .Setup(s => s.GetSeriesInfo(10, Language.English))
                  .Returns(new Tuple<Series, List<Episode>>(new Series(), new List<Episode>()));

            Subject.GetSeriesInfo(series);

            Mocker.GetMock<ITmdbProxy>().Verify(v => v.GetSeriesInfo(10, Language.English), Times.Once());
            Mocker.GetMock<ISkyHookProxy>().Verify(v => v.GetSeriesInfo(It.IsAny<int>(), It.IsAny<Language>(), It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_use_tmdb_for_negative_tvdb_id_regardless_of_source()
        {
            var series = new Series { TvdbId = -10, MetadataSource = MetadataSourceType.Tvdb, Language = Language.English };

            Mocker.GetMock<ITmdbProxy>()
                  .Setup(s => s.GetSeriesInfo(10, Language.English))
                  .Returns(new Tuple<Series, List<Episode>>(new Series(), new List<Episode>()));

            Subject.GetSeriesInfo(series);

            Mocker.GetMock<ITmdbProxy>().Verify(v => v.GetSeriesInfo(10, Language.English), Times.Once());
        }
    }
}
