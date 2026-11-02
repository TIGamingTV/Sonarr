using FluentValidation;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.MetadataSource.Tmdb;
using Sonarr.Http;

namespace Sonarr.Api.V5.Settings;

[V5ApiController("settings/metadata")]
public class MetadataSourceSettingsController : SettingsController<MetadataSourceSettingsResource>
{
    public MetadataSourceSettingsController(IConfigFileProvider configFileProvider, IConfigService configService, ITmdbProxy tmdbProxy)
        : base(configFileProvider, configService)
    {
        SharedValidator.RuleFor(c => c.TmdbApiKey)
                       .Must(key => tmdbProxy.ValidateApiKey(key!))
                       .When(c => c.TmdbApiKey.IsNotNullOrWhiteSpace() && c.TmdbApiKey != configService.TmdbApiKey)
                       .WithMessage("TMDB rejected the API key, make sure you're using a valid API key or API Read Access Token");

        SharedValidator.RuleFor(c => c.DefaultMetadataSource)
                       .Must((c, _) => c.TmdbApiKey.IsNotNullOrWhiteSpace())
                       .When(c => c.DefaultMetadataSource == MetadataSourceType.Tmdb)
                       .WithMessage("A TMDB API key is required to use TMDB as the default metadata source");

        SharedValidator.RuleFor(c => c.PreferredMetadataLanguage).Custom((value, context) =>
        {
            if (!Language.All.Any(o => o.Id == value))
            {
                context.AddFailure("Invalid Metadata Language value");
            }
        });

        SharedValidator.RuleFor(c => c.PreferredMetadataLanguage)
                       .GreaterThanOrEqualTo(1)
                       .WithMessage("The Metadata Language value cannot be less than 1");
    }

    protected override MetadataSourceSettingsResource ToResource(IConfigFileProvider configFile, IConfigService model)
    {
        return MetadataSourceSettingsResourceMapper.ToResource(model);
    }
}
