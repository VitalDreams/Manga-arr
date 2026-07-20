import { push } from 'connected-react-router';
import _ from 'lodash';
import PropTypes from 'prop-types';
import React, { useEffect } from 'react';
import { useSelector, useDispatch } from 'react-redux';
import { createSelector } from 'reselect';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import NotFound from 'Components/NotFound';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import getErrorMessage from 'Utilities/Object/getErrorMessage';
import translate from 'Utilities/String/translate';
import MangaDetailsConnector from './MangaDetailsConnector';
import styles from './MangaDetails.css';

function MangaDetailsPageConnector(props) {
  const { match } = props;
  const dispatch = useDispatch();

  const titleSlug = match.params.titleSlug;

  const manga = useSelector((state) => state.manga);

  const {
    isFetching,
    isPopulated,
    error,
    items
  } = manga;

  const mangaIndex = _.findIndex(items, { titleSlug });

  useEffect(() => {
    if (!titleSlug || (isPopulated && mangaIndex === -1)) {
      dispatch(push(`${window.Readarr.urlBase}/`));
    }
  }, [titleSlug, isPopulated, mangaIndex, dispatch]);

  if (isFetching && !isPopulated) {
    return (
      <PageContent title={translate('Loading')}>
        <PageContentBody>
          <LoadingIndicator />
        </PageContentBody>
      </PageContent>
    );
  }

  if (!isFetching && !!error) {
    return (
      <div className={styles.errorMessage}>
        {getErrorMessage(error, 'Failed to load manga from API')}
      </div>
    );
  }

  if (!titleSlug || (isPopulated && mangaIndex === -1)) {
    return (
      <NotFound
        message={translate('SorryThatAuthorCannotBeFound')}
      />
    );
  }

  return (
    <MangaDetailsConnector
      titleSlug={titleSlug}
    />
  );
}

MangaDetailsPageConnector.propTypes = {
  match: PropTypes.shape({
    params: PropTypes.shape({
      titleSlug: PropTypes.string.isRequired
    }).isRequired
  }).isRequired
};

export default MangaDetailsPageConnector;
