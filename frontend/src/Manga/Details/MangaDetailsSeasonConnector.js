import _ from 'lodash';
import PropTypes from 'prop-types';
import React, { useEffect, useCallback } from 'react';
import { useSelector, useDispatch } from 'react-redux';
import { createSelector } from 'reselect';
import { setMangaDetailsId, setMangaDetailsSort } from 'Store/Actions/mangaDetailsActions';
import { setVolumesTableOption, toggleVolumesMonitored } from 'Store/Actions/volumeActions';
import createClientSideCollectionSelector from 'Store/Selectors/createClientSideCollectionSelector';
import createDimensionsSelector from 'Store/Selectors/createDimensionsSelector';
import MangaDetailsSeason from './MangaDetailsSeason';

function MangaDetailsSeasonConnector(props) {
  const { mangaId } = props;
  const dispatch = useDispatch();

  const volumes = useSelector(createClientSideCollectionSelector('volumes', 'mangaDetails'));
  const dimensions = useSelector(createDimensionsSelector());

  const isSmallScreen = dimensions.isSmallScreen;

  useEffect(() => {
    dispatch(setMangaDetailsId({ mangaId }));
  }, [mangaId, dispatch]);

  const onSortPress = useCallback((sortKey) => {
    dispatch(setMangaDetailsSort({ sortKey }));
  }, [dispatch]);

  const onTableOptionChange = useCallback((payload) => {
    dispatch(setVolumesTableOption(payload));
  }, [dispatch]);

  const onMonitorVolumePress = useCallback((volumeIds, monitored) => {
    dispatch(toggleVolumesMonitored({
      volumeIds,
      monitored
    }));
  }, [dispatch]);

  let sortDir = 'asc';
  if (volumes.sortDirection === 'descending') {
    sortDir = 'desc';
  }

  const sortedVolumes = _.orderBy(volumes.items, volumes.sortKey, sortDir);

  return (
    <MangaDetailsSeason
      items={sortedVolumes}
      columns={volumes.columns}
      sortKey={volumes.sortKey}
      sortDirection={volumes.sortDirection}
      mangaId={mangaId}
      isSmallScreen={isSmallScreen}
      onSortPress={onSortPress}
      onTableOptionChange={onTableOptionChange}
      onMonitorVolumePress={onMonitorVolumePress}
    />
  );
}

MangaDetailsSeasonConnector.propTypes = {
  mangaId: PropTypes.number.isRequired
};

export default MangaDetailsSeasonConnector;
