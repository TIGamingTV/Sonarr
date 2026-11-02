import React from 'react';
import Link from 'Components/Link/Link';
import translate from 'Utilities/String/translate';
import styles from './MetadataAttribution.module.css';

interface MetadataAttributionProps {
  providers?: string[];
}

export default function MetadataAttribution({
  providers = ['TheTVDB'],
}: MetadataAttributionProps) {
  return (
    <div className={styles.container}>
      <Link className={styles.attribution} to="/settings/metadatasource">
        {translate('MetadataProvidedBy', { provider: providers.join(' & ') })}
      </Link>
    </div>
  );
}
