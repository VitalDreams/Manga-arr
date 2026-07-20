import React, { useEffect, useCallback, useRef } from 'react';
import { useSelector, useDispatch } from 'react-redux';
import * as commandNames from 'Commands/commandNames';
import { saveMangaEditor, setMangaFilter, setMangaSort, setMangaView } from 'Store/Actions/mangaIndexActions';
import { executeCommand } from 'Store/Actions/commandActions';
import createMangaClientSideCollectionItemsSelector from 'Store/Selectors/createMangaClientSideCollectionItemsSelector';
import createCommandExecutingSelector from 'Store/Selectors/createCommandExecutingSelector';
import createDimensionsSelector from 'Store/Selectors/createDimensionsSelector';
import scrollPositions from 'Store/scrollPositions';
import MangaIndex from './MangaIndex';

const mangaItemsSelector = createMangaClientSideCollectionItemsSelector('mangaIndex');
const refreshCommandSelector = createCommandExecutingSelector(commandNames.BULK_REFRESH_AUTHOR);
const rssSyncSelector = createCommandExecutingSelector(commandNames.RSS_SYNC);

function MangaIndexConnector(props) {
  const dispatch = useDispatch();
  const scrollRef = useRef(null);

  const mangaData = useSelector(mangaItemsSelector);
  const isRefreshingManga = useSelector(refreshCommandSelector);
  const isRssSyncExecuting = useSelector(rssSyncSelector);
  const dimensionsState = useSelector(createDimensionsSelector());

  const {
    items,
    sortKey,
    sortDirection,
    view,
    posterOptions,
    totalItems,
    isFetching,
    isPopulated,
    error
  } = mangaData;

  const isSmallScreen = dimensionsState.isSmallScreen;

  useEffect(() => {
    scrollRef.current = scrollPositions.mangaIndex || 0;
  }, []);

  const onSortSelect = useCallback((newSortKey) => {
    dispatch(setMangaSort({ sortKey: newSortKey }));
  }, [dispatch]);

  const onFilterSelect = useCallback((selectedFilterKey) => {
    dispatch(setMangaFilter({ selectedFilterKey }));
  }, [dispatch]);

  const onViewSelect = useCallback((newView) => {
    dispatch(setMangaView({ view: newView }));
  }, [dispatch]);

  const onRefreshMangaPress = useCallback(() => {
    dispatch(executeCommand({
      name: commandNames.BULK_REFRESH_AUTHOR
    }));
  }, [dispatch]);

  const onRssSyncPress = useCallback(() => {
    dispatch(executeCommand({
      name: commandNames.RSS_SYNC
    }));
  }, [dispatch]);

  const onSaveSelected = useCallback((payload) => {
    dispatch(saveMangaEditor(payload));
  }, [dispatch]);

  const onScroll = useCallback(({ scrollTop }) => {
    scrollPositions.mangaIndex = scrollTop;
  }, []);

  return (
    <MangaIndex
      isFetching={isFetching}
      isPopulated={isPopulated}
      error={error}
      items={items}
      sortKey={sortKey}
      sortDirection={sortDirection}
      view={view}
      posterOptions={posterOptions}
      totalItems={totalItems}
      isSmallScreen={isSmallScreen}
      isRefreshingManga={isRefreshingManga}
      isRssSyncExecuting={isRssSyncExecuting}
      onSortSelect={onSortSelect}
      onFilterSelect={onFilterSelect}
      onViewSelect={onViewSelect}
      onRefreshMangaPress={onRefreshMangaPress}
      onRssSyncPress={onRssSyncPress}
      onSaveSelected={onSaveSelected}
      onScroll={onScroll}
    />
  );
}

export default MangaIndexConnector;
