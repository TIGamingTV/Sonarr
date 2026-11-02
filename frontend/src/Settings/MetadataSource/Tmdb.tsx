import React from 'react';
import InlineMarkdown from 'Components/Markdown/InlineMarkdown';
import SectionHeading from 'Components/SectionHeading';
import translate from 'Utilities/String/translate';
import styles from './Tmdb.module.css';

function Tmdb() {
  return (
    <div className={styles.container}>
      <SectionHeading
        title="TMDB"
        description={
          <InlineMarkdown
            data={translate('SeriesAndEpisodeInformationIsProvidedByTmdb', {
              url: 'https://www.themoviedb.org/settings/api',
            })}
          />
        }
      />
    </div>
  );
}

export default Tmdb;
