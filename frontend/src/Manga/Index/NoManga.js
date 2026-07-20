import PropTypes from 'prop-types';
import React from 'react';
import Button from 'Components/Link/Button';
import { kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import styles from './NoManga.css';

function NoManga(props) {
  const {
    totalItems,
    itemType
  } = props;

  if (totalItems > 0) {
    return (
      <div>
        <div className={styles.message}>
          {`All ${itemType} are hidden due to the applied filter.`}
        </div>
      </div>
    );
  }

  return (
    <div>
      <div className={styles.message}>
        {`No ${itemType} found. Add your first manga or set up a library location (Root Folder) to get started.`}
      </div>

      <div className={styles.buttonContainer}>
        <Button
          to="/settings/mediamanagement"
          kind={kinds.PRIMARY}
        >
          {translate('AddRootFolder')}
        </Button>
      </div>

      <div className={styles.buttonContainer}>
        <Button
          to="/add/search"
          kind={kinds.PRIMARY}
        >
          {translate('AddNew')}
        </Button>
      </div>
    </div>
  );
}

NoManga.propTypes = {
  totalItems: PropTypes.number.isRequired,
  itemType: PropTypes.string.isRequired
};

NoManga.defaultProps = {
  itemType: 'manga'
};

export default NoManga;
