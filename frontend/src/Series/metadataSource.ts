import { useMemo } from 'react';
import { EnhancedSelectInputValue } from 'Components/Form/Select/EnhancedSelectInput';
import { useMetadataSourceSettingsValues } from 'Settings/MetadataSource/useMetadataSourceSettings';
import translate from 'Utilities/String/translate';
import { MetadataSource } from './Series';

interface MetadataSourceIds {
  tvdbId: number;
  tmdbId: number;
}

export function isOnTvdb({ tvdbId }: Pick<MetadataSourceIds, 'tvdbId'>) {
  return tvdbId > 0;
}

export function getMetadataSourceName(metadataSource?: MetadataSource) {
  return metadataSource === 'tmdb' ? 'TMDB' : 'TheTVDB';
}

export function getTmdbUrl(tmdbId: number) {
  return `https://www.themoviedb.org/tv/${tmdbId}`;
}

export function getTvdbUrl(tvdbId: number) {
  return `https://www.thetvdb.com/?tab=series&id=${tvdbId}`;
}

export function useMetadataSourceSettingsInfo() {
  const { tmdbApiKey, defaultMetadataSource } =
    useMetadataSourceSettingsValues();

  return {
    isTmdbConfigured: !!tmdbApiKey,
    defaultMetadataSource: defaultMetadataSource ?? 'tvdb',
  };
}

// Available sources for a series, TMDB can also be resolved from the TVDB ID by the server
export function useMetadataSourceOptions(
  { tvdbId, tmdbId }: MetadataSourceIds,
  requireTmdbId = false
) {
  const { isTmdbConfigured } = useMetadataSourceSettingsInfo();

  return useMemo<EnhancedSelectInputValue<string>[]>(() => {
    const isTvdbAvailable = tvdbId > 0;
    const isTmdbAvailable =
      isTmdbConfigured && (tmdbId > 0 || tvdbId < 0 || !requireTmdbId);

    let tmdbHint: string | undefined = undefined;

    if (!isTmdbConfigured) {
      tmdbHint = translate('TmdbApiKeyNotConfigured');
    } else if (!isTmdbAvailable) {
      tmdbHint = translate('NotAvailable');
    }

    return [
      {
        key: 'tvdb',
        value: 'TheTVDB',
        isDisabled: !isTvdbAvailable,
        hint: isTvdbAvailable ? undefined : translate('NotAvailable'),
      },
      {
        key: 'tmdb',
        value: 'TMDB',
        isDisabled: !isTmdbAvailable,
        hint: tmdbHint,
      },
    ];
  }, [tvdbId, tmdbId, isTmdbConfigured, requireTmdbId]);
}

// Picks the default source for a new series based on what's available
export function useDefaultMetadataSource({
  tvdbId,
  tmdbId,
}: MetadataSourceIds): MetadataSource {
  const { isTmdbConfigured, defaultMetadataSource } =
    useMetadataSourceSettingsInfo();

  if (tvdbId <= 0) {
    return 'tmdb';
  }

  if (defaultMetadataSource === 'tmdb' && isTmdbConfigured && tmdbId > 0) {
    return 'tmdb';
  }

  return 'tvdb';
}
