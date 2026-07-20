import _ from 'lodash';
import PropTypes from 'prop-types';
import React, { useState, useEffect, useCallback, useRef } from 'react';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageJumpBar from 'Components/Page/PageJumpBar';
import PageToolbar from 'Components/Page/Toolbar/PageToolbar';
import PageToolbarButton from 'Components/Page/Toolbar/PageToolbarButton';
import PageToolbarSection from 'Components/Page/Toolbar/PageToolbarSection';
import PageToolbarSeparator from 'Components/Page/Toolbar/PageToolbarSeparator';
import { align, icons, sortDirections } from 'Helpers/Props';
import getErrorMessage from 'Utilities/Object/getErrorMessage';
import hasDifferentItemsOrOrder from 'Utilities/Object/hasDifferentItemsOrOrder';
import translate from 'Utilities/String/translate';
import getSelectedIds from 'Utilities/Table/getSelectedIds';
import selectAll from 'Utilities/Table/selectAll';
import toggleSelected from 'Utilities/Table/toggleSelected';
import NoManga from './NoManga';
import MangaIndexPosters from './MangaIndexPosters';
import styles from './MangaIndex.css';

function MangaIndex(props) {
  const {
    isFetching,
    isPopulated,
    error,
    items,
    sortKey,
    sortDirection,
    view,
    posterOptions,
    jumpToCharacter,
    isSmallScreen,
    isRefreshingManga,
    isRssSyncExecuting,
    totalItems,
    onSortSelect,
    onFilterSelect,
    onViewSelect,
    onRefreshMangaPress,
    onRssSyncPress
  } = props;

  const scrollerRef = useRef(null);
  const [isEditorActive, setIsEditorActive] = useState(false);
  const [allSelected, setAllSelected] = useState(false);
  const [allUnselected, setAllUnselected] = useState(false);
  const [lastToggled, setLastToggled] = useState(null);
  const [selectedState, setSelectedState] = useState({});
  const [jumpBarItems, setJumpBarItems] = useState({ order: [] });

  useEffect(() => {
    if (sortKey === 'title') {
      const firstCharMap = {};
      items.forEach((item) => {
        const firstChar = (item.title || '')[0]?.toUpperCase() || '#';
        if (!firstCharMap[firstChar]) {
          firstCharMap[firstChar] = true;
        }
      });
      setJumpBarItems({
        order: Object.keys(firstCharMap).sort()
      });
    } else {
      setJumpBarItems({ order: [] });
    }
  }, [items, sortKey]);

  const onEditorPress = useCallback(() => {
    setIsEditorActive((prev) => !prev);
  }, []);

  const onSelectAllChange = useCallback(() => {
    const selectAllState = selectAll(selectedState, items.length, !allSelected);
    setSelectedState(selectAllState.selectedState);
    setAllSelected(selectAllState.allSelected);
    setAllUnselected(selectAllState.allUnselected);
  }, [selectedState, items.length, allSelected]);

  const onSelectedChange = useCallback(({ id, value, shiftKey }) => {
    const result = toggleSelected(selectedState, id, value, shiftKey, items, lastToggled);
    setSelectedState(result.selectedState);
    setAllSelected(result.allSelected);
    setAllUnselected(result.allUnselected);
    setLastToggled(result.lastToggled);
  }, [selectedState, items, lastToggled]);

  const onJumpToCharacterPress = useCallback((character) => {
    // JumpToCharacter is handled by the posters component
  }, []);

  const onSaveSelected = useCallback(() => {
    const selectedIds = getSelectedIds(selectedState);
    if (selectedIds.length && props.onSaveSelected) {
      props.onSaveSelected({ ids: selectedIds });
    }
  }, [selectedState, props]);

  if (isFetching && !isPopulated) {
    return (
      <PageContent title={translate('Manga')}>
        <PageContentBody>
          <LoadingIndicator />
        </PageContentBody>
      </PageContent>
    );
  }

  if (!isFetching && !!error) {
    return (
      <PageContent title={translate('Manga')}>
        <PageContentBody>
          <div className={styles.errorMessage}>
            {getErrorMessage(error, 'Failed to load manga from API')}
          </div>
        </PageContentBody>
      </PageContent>
    );
  }

  if (isPopulated && !items.length && !totalItems) {
    return (
      <PageContent title={translate('Manga')}>
        <PageContentBody>
          <NoManga totalItems={totalItems} />
        </PageContentBody>
      </PageContent>
    );
  }

  const hasSelection = getSelectedIds(selectedState).length > 0;

  return (
    <PageContent title={translate('Manga')}>
      <PageToolbar>
        <PageToolbarSection>
          <PageToolbarButton
            label={translate('RefreshManga')}
            iconName={icons.REFRESH}
            isSpinning={isRefreshingManga}
            onPress={onRefreshMangaPress}
          />

          <PageToolbarSeparator />

          <PageToolbarButton
            label={translate('RssSync')}
            iconName={icons.RSS}
            isSpinning={isRssSyncExecuting}
            onPress={onRssSyncPress}
          />

          <PageToolbarSeparator />

          <PageToolbarButton
            label={isEditorActive ? 'Save' : 'Editor'}
            iconName={isEditorActive ? icons.SAVE : icons.EDIT}
            isDisabled={isEditorActive && !hasSelection}
            onPress={isEditorActive ? onSaveSelected : onEditorPress}
          />

          {
            isEditorActive &&
              <PageToolbarButton
                label={allSelected ? 'Unselect All' : 'Select All'}
                iconName={icons.CHECK_INDETERMINATE}
                onPress={onSelectAllChange}
              />
          }
        </PageToolbarSection>
      </PageToolbar>

      <PageContentBody
        ref={scrollerRef}
        className={styles.contentBody}
      >
        <div className={styles.postersInnerContentBody}>
          {
            view === 'posters' &&
              <MangaIndexPosters
                items={items}
                sortKey={sortKey}
                posterOptions={posterOptions}
                jumpToCharacter={jumpToCharacter}
                scrollTop={0}
                scroller={scrollerRef.current}
                isSmallScreen={isSmallScreen}
                selectedState={selectedState}
                onSelectedChange={onSelectedChange}
                isEditorActive={isEditorActive}
              />
          }
        </div>

        {
          jumpBarItems.order.length > 0 &&
            <PageJumpBar
              items={jumpBarItems}
              jumpToCharacter={jumpToCharacter}
              onPress={onJumpToCharacterPress}
            />
        }
      </PageContentBody>
    </PageContent>
  );
}

MangaIndex.propTypes = {
  isFetching: PropTypes.bool.isRequired,
  isPopulated: PropTypes.bool.isRequired,
  error: PropTypes.object,
  items: PropTypes.arrayOf(PropTypes.object).isRequired,
  sortKey: PropTypes.string.isRequired,
  sortDirection: PropTypes.string.isRequired,
  view: PropTypes.string.isRequired,
  posterOptions: PropTypes.object.isRequired,
  jumpToCharacter: PropTypes.string,
  isSmallScreen: PropTypes.bool.isRequired,
  isRefreshingManga: PropTypes.bool.isRequired,
  isRssSyncExecuting: PropTypes.bool.isRequired,
  totalItems: PropTypes.number.isRequired,
  onSortSelect: PropTypes.func.isRequired,
  onFilterSelect: PropTypes.func.isRequired,
  onViewSelect: PropTypes.func.isRequired,
  onRefreshMangaPress: PropTypes.func.isRequired,
  onRssSyncPress: PropTypes.func.isRequired,
  onSaveSelected: PropTypes.func.isRequired
};

export default MangaIndex;
