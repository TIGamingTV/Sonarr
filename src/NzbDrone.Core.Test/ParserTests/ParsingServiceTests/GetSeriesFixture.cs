using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DataAugmentation.Scene;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.ParserTests.ParsingServiceTests
{
    [TestFixture]
    public class GetSeriesFixture : CoreTest<ParsingService>
    {
        [Test]
        public void should_use_passed_in_title_when_it_cannot_be_parsed()
        {
            const string title = "30 Stone";

            Subject.GetSeries(title);

            Mocker.GetMock<ISeriesService>()
                  .Verify(s => s.FindByTitle(title), Times.Once());
        }

        [Test]
        public void should_use_parsed_series_title()
        {
            const string title = "30.Stone.S01E01.720p.hdtv";

            Subject.GetSeries(title);

            Mocker.GetMock<ISeriesService>()
                  .Verify(s => s.FindByTitle(Parser.Parser.ParseTitle(title).SeriesTitle), Times.Once());
        }

        [Test]
        public void should_fallback_to_title_without_year_and_year_when_title_lookup_fails()
        {
            const string title = "Show.2004.S01E01.720p.hdtv";
            var parsedEpisodeInfo = Parser.Parser.ParseTitle(title);

            Subject.GetSeries(title);

            Mocker.GetMock<ISeriesService>()
                  .Verify(s => s.FindByTitle(parsedEpisodeInfo.SeriesTitleInfo.TitleWithoutYear,
                                             parsedEpisodeInfo.SeriesTitleInfo.Year),
                      Times.Once());
        }

        [Test]
        public void should_parse_concatenated_title()
        {
            var series = new Series { TvdbId = 100 };
            Mocker.GetMock<ISeriesService>().Setup(v => v.FindByTitle("Welcome")).Returns(series);
            Mocker.GetMock<ISceneMappingService>().Setup(v => v.FindTvdbId("Mairimashita", It.IsAny<string>(), It.IsAny<int>())).Returns(100);

            var result = Subject.GetSeries("Welcome (Mairimashita).S01E01.720p.WEB-DL-Viva");

            result.Should().NotBeNull();
            result.TvdbId.Should().Be(100);
        }

        [Test]
        public void should_prefer_tmdb_series_matching_title_over_scene_mapping_to_another_tvdb_id()
        {
            const string title = "Dragons.Race.To.The.Edge.S01E01.720p.WEB-DL-Viva";
            var parsedTitle = Parser.Parser.ParseTitle(title).SeriesTitle;
            var series = new Series { TvdbId = -62617, TmdbId = 62617, MetadataSource = MetadataSourceType.Tmdb };

            Mocker.GetMock<ISeriesService>().Setup(v => v.FindByTitle(parsedTitle)).Returns(series);
            Mocker.GetMock<ISceneMappingService>()
                  .Setup(v => v.FindSceneMapping(parsedTitle, It.IsAny<string>(), It.IsAny<int>()))
                  .Returns(new SceneMapping { TvdbId = 261202 });

            Subject.GetSeries(title).Should().Be(series);

            Mocker.GetMock<ISeriesService>()
                  .Verify(v => v.FindByTvdbId(It.IsAny<int>()), Times.Never());
        }

        [Test]
        public void should_use_scene_mapping_when_title_matches_tvdb_series()
        {
            const string title = "Dragons.Race.To.The.Edge.S01E01.720p.WEB-DL-Viva";
            var parsedTitle = Parser.Parser.ParseTitle(title).SeriesTitle;
            var mappedSeries = new Series { TvdbId = 261202 };

            Mocker.GetMock<ISeriesService>().Setup(v => v.FindByTitle(parsedTitle)).Returns(new Series { TvdbId = 100 });
            Mocker.GetMock<ISeriesService>().Setup(v => v.FindByTvdbId(261202)).Returns(mappedSeries);
            Mocker.GetMock<ISceneMappingService>()
                  .Setup(v => v.FindSceneMapping(parsedTitle, It.IsAny<string>(), It.IsAny<int>()))
                  .Returns(new SceneMapping { TvdbId = 261202 });

            Subject.GetSeries(title).Should().Be(mappedSeries);
        }
    }
}
