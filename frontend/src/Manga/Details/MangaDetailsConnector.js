import _ from 'lodash';
import PropTypes from 'prop-types';
import React, { useEffect, useCallback } from 'react';
import { useSelector, useDispatch } from 'react-redux';
import { createSelector } from 'reselect';
import * as commandNames from 'Commands/commandNames';
import { toggleMangaMonitored } from 'Store/Actions/mangaActions';
import { fetchVolumes } from 'Store/Actions/volumeActions';
import { executeCommand } from 'Store/Actions/commandActions';
import createCommandExecutingSelector from 'Store/Selectors/createCommandExecutingSelector';
import createDimensionsSelector from 'Store/Selectors/createDimensionsSelector';
import MangaDetails from './MangaDetails';

const refreshCommandSelector = createCommandExecutingSelector(commandNames.BULK_REFRESH_AUTHOR);

function MangaDetailsConnector(props) {
  const { titleSlug } = props;
  const dispatch = useDispatch();

  const manga = useSelector((state) => {
    return _.find(state.manga.items, { titleSlug });
  });

  const isRefreshingManga = useSelector(refreshCommandSelector);
  const dimensionsState = useSelector(createDimensionsSelector());

  const isSmallScreen = dimensionsState.isSmallScreen;

  useEffect(() => {
    if (manga) {
      dispatch(fetchVolumes({ mangaId: manga.id }));
    }
  }, [manga, dispatch]);

  const onRefreshMangaPress = useCallback(() => {
    if (manga) {
      dispatch(executeCommand({
        name: commandNames.BULK_REFRESH_AUTHOR,
        authorIds: [manga.id]
      }));
    }
  }, [manga, dispatch]);

  const onMonitorTogglePress = useCallback((monitored) => {
    if (manga) {
      dispatch(toggleMangaMonitored({
        mangaId: manga.id,
        monitored
      }));
    }
  }, [manga, dispatch]);

  if (!manga) {
    return null;
  }

  return (
    <MangaDetails
      manga={manga}
      isSmallScreen={isSmallScreen}
      isRefreshingManga={isRefreshingManga}
      onRefreshMangaPress={onRefreshMangaPress}
      onMonitorTogglePress={onMonitorTogglePress}
    />
  );
}

MangaDetailsConnector.propTypes = {
  titleSlug: PropTypes.string.isRequired
};

export default MangaDetailsConnector;
