using FluentValidation;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.MetadataSource.Tmdb;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Validation;
using NzbDrone.Core.Validation.Paths;

namespace Sonarr.Api.V5.Series;

public class SeriesEditorValidator : AbstractValidator<NzbDrone.Core.Tv.Series>
{
    public SeriesEditorValidator(RootFolderExistsValidator rootFolderExistsValidator, QualityProfileExistsValidator qualityProfileExistsValidator, ITmdbProxy tmdbProxy)
    {
        RuleFor(s => s.MetadataSource)
            .Must(_ => tmdbProxy.IsConfigured)
            .When(s => s.MetadataSource == MetadataSourceType.Tmdb)
            .WithMessage("A TMDB API key must be configured to use TMDB as the metadata source");

        RuleFor(s => s.MetadataSource)
            .Must((series, _) => series.TvdbId > 0)
            .When(s => s.MetadataSource == MetadataSourceType.Tvdb)
            .WithMessage(s => $"'{s.Title}' is not available on TheTVDB");

        RuleFor(s => s.RootFolderPath).Cascade(CascadeMode.Stop)
            .IsValidPath()
            .SetValidator(rootFolderExistsValidator)
            .When(s => s.RootFolderPath.IsNotNullOrWhiteSpace());

        RuleFor(c => c.QualityProfileId).Cascade(CascadeMode.Stop)
            .ValidId()
            .SetValidator(qualityProfileExistsValidator);

        RuleFor(s => s.Language).Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(l => IsoLanguages.Get(l) != null)
            .WithMessage("Invalid Language value");
    }
}
