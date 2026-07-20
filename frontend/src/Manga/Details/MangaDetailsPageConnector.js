import { push } from 'connected-react-router';
import _ from 'lodash';
import PropTypes from 'prop-types';
import React, { useEffect } from 'react';
import { useSelector, useDispatch } from 'react-redux';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import NotFound from 'Components/NotFound';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import { fetchManga } from 'Store/Actions/mangaActions';
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

  const mangaItem = _.find(items, { titleSlug });

  useEffect(() => {
    if (!isPopulated && !isFetching) {
      dispatch(fetchManga());
    }
  }, [isPopulated, isFetching, dispatch]);

  if (!titleSlug) {
    return (
      <NotFound message={translate('SorryThatAuthorCannotBeFound')} />
    );
  }

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

  if (isPopulated && !isFetching && !mangaItem) {
    return (
      <NotFound message={translate('SorryThatAuthorCannotBeFound')} />
    );
  }

  if (!mangaItem) {
    return (
      <PageContent title={translate('Loading')}>
        <PageContentBody>
          <LoadingIndicator />
        </PageContentBody>
      </PageContent>
    );
  }

  return (
    <MangaDetailsConnector titleSlug={titleSlug} />
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
