import PropTypes from 'prop-types';
import React from 'react';
import Button from 'Components/Link/Button';
import { kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import styles from './NoAuthor.css';

function NoAuthor(props) {
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
          {translate('AddNewAuthor')}
        </Button>
      </div>
    </div>
  );
}

NoAuthor.propTypes = {
  totalItems: PropTypes.number.isRequired,
  itemType: PropTypes.string.isRequired
};

NoAuthor.defaultProps = {
  itemType: 'authors'
};

export default NoAuthor;
