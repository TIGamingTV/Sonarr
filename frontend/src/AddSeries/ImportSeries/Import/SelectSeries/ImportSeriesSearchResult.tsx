import classNames from 'classnames';
import React, { useCallback, useEffect, useRef } from 'react';
import IconButton from 'Components/Link/IconButton';
import Link from 'Components/Link/Link';
import { icons } from 'Helpers/Props';
import { getTmdbUrl, getTvdbUrl } from 'Series/metadataSource';
import useExistingSeries from 'Series/useExistingSeries';
import translate from 'Utilities/String/translate';
import ImportSeriesTitle from './ImportSeriesTitle';
import styles from './ImportSeriesSearchResult.module.css';

interface ImportSeriesSearchResultProps {
  id: string;
  tvdbId: number;
  tmdbId: number;
  title: string;
  year: number;
  network?: string;
  isHighlighted: boolean;
  onPress: (tvdbId: number) => void;
}

function ImportSeriesSearchResult({
  id,
  tvdbId,
  tmdbId,
  title,
  year,
  network,
  isHighlighted,
  onPress,
}: ImportSeriesSearchResultProps) {
  const isExistingSeries = useExistingSeries(tvdbId);
  const linkTitle =
    tvdbId > 0
      ? translate('ViewSeriesOnTvdb', { title })
      : translate('ViewSeriesOnTmdb', { title });

  const rowRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (isHighlighted) {
      rowRef.current?.scrollIntoView({ block: 'nearest' });
    }
  }, [isHighlighted]);

  const handlePress = useCallback(() => {
    onPress(tvdbId);
  }, [tvdbId, onPress]);

  return (
    <div
      ref={rowRef}
      className={classNames(
        styles.container,
        isHighlighted ? styles.highlighted : undefined
      )}
      role="row"
      id={id}
      aria-selected={isHighlighted}
    >
      <div className={styles.seriesCell} role="gridcell">
        <Link className={styles.series} onPress={handlePress}>
          <ImportSeriesTitle
            title={title}
            year={year}
            network={network}
            isExistingSeries={isExistingSeries}
          />
        </Link>
      </div>

      <div className={styles.tvdbLinkCell} role="gridcell">
        <IconButton
          className={styles.tvdbLink}
          iconClassName={styles.tvdbLinkIcon}
          name={icons.EXTERNAL_LINK}
          size={16}
          to={tvdbId > 0 ? getTvdbUrl(tvdbId) : getTmdbUrl(tmdbId)}
          title={linkTitle}
          aria-label={linkTitle}
        />
      </div>
    </div>
  );
}

export default ImportSeriesSearchResult;
