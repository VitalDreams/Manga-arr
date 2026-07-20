import PropTypes from 'prop-types';
import React, { useState, useEffect, useRef, useCallback } from 'react';
import { Grid, WindowScroller } from 'react-virtualized';
import Measure from 'Components/Measure';
import dimensions from 'Styles/Variables/dimensions';
import getIndexOfFirstCharacter from 'Utilities/Array/getIndexOfFirstCharacter';
import hasDifferentItemsOrOrder from 'Utilities/Object/hasDifferentItemsOrOrder';
import MangaIndexPoster from './MangaIndexPoster';
import styles from './MangaIndexPosters.css';

const columnPadding = parseInt(dimensions.authorIndexColumnPadding);
const columnPaddingSmallScreen = parseInt(dimensions.authorIndexColumnPaddingSmallScreen);
const progressBarHeight = parseInt(dimensions.progressBarSmallHeight);
const detailedProgressBarHeight = parseInt(dimensions.progressBarMediumHeight);

const additionalColumnCount = {
  small: 3,
  medium: 2,
  large: 1
};

function calculateColumnWidth(width, posterSize, isSmallScreen) {
  const maxiumColumnWidth = isSmallScreen ? 172 : 182;
  const columns = Math.floor(width / maxiumColumnWidth);
  const remainder = width % maxiumColumnWidth;

  if (remainder === 0 && posterSize === 'large') {
    return maxiumColumnWidth;
  }

  return Math.floor(width / (columns + additionalColumnCount[posterSize]));
}

function calculateRowHeight(posterHeight, isSmallScreen, posterOptions) {
  const {
    detailedProgressBar,
    showTitle,
    showMonitored
  } = posterOptions;

  const heights = [
    posterHeight,
    detailedProgressBar ? detailedProgressBarHeight : progressBarHeight,
    isSmallScreen ? columnPaddingSmallScreen : columnPadding
  ];

  if (showTitle !== 'no') {
    heights.push(19);
  }

  if (showMonitored) {
    heights.push(19);
  }

  return heights.reduce((acc, height) => acc + height, 0);
}

function calculatePosterHeight(posterWidth) {
  return Math.ceil(posterWidth);
}

function MangaIndexPosters(props) {
  const {
    items,
    sortKey,
    posterOptions,
    jumpToCharacter,
    scrollTop: initialScrollTop,
    scroller,
    isSmallScreen,
    selectedState,
    onSelectedChange,
    isEditorActive
  } = props;

  const gridRef = useRef(null);
  const [width, setWidth] = useState(0);
  const [columnWidth, setColumnWidth] = useState(182);
  const [columnCount, setColumnCount] = useState(1);
  const [posterWidth, setPosterWidth] = useState(238);
  const [posterHeight, setPosterHeight] = useState(238);
  const [rowHeight, setRowHeight] = useState(calculateRowHeight(238, isSmallScreen, {}));
  const [scrollRestored, setScrollRestored] = useState(false);

  const _padding = isSmallScreen ? columnPaddingSmallScreen : columnPadding;

  const calculateGrid = useCallback((newWidth, smallScreen) => {
    const newColumnWidth = calculateColumnWidth(newWidth, posterOptions.size, smallScreen);
    const newColumnCount = Math.max(Math.floor(newWidth / newColumnWidth), 1);
    const newPosterWidth = newColumnWidth - _padding * 2;
    const newPosterHeight = calculatePosterHeight(newPosterWidth);
    const newRowHeight = calculateRowHeight(newPosterHeight, smallScreen, posterOptions);

    setWidth(newWidth);
    setColumnWidth(newColumnWidth);
    setColumnCount(newColumnCount);
    setPosterWidth(newPosterWidth);
    setPosterHeight(newPosterHeight);
    setRowHeight(newRowHeight);
  }, [posterOptions, _padding]);

  useEffect(() => {
    calculateGrid(width, isSmallScreen);
  }, [sortKey, posterOptions, calculateGrid, width, isSmallScreen]);

  useEffect(() => {
    if (gridRef.current) {
      gridRef.current.recomputeGridSize();
    }
  }, [width, columnWidth, columnCount, rowHeight, items, isEditorActive, selectedState, posterOptions.showTitle]);

  useEffect(() => {
    if (gridRef.current && initialScrollTop !== 0 && !scrollRestored) {
      setScrollRestored(true);
      gridRef.current.scrollToPosition({ scrollTop: initialScrollTop });
    }
  }, [initialScrollTop, scrollRestored]);

  useEffect(() => {
    if (jumpToCharacter != null && gridRef.current) {
      const index = getIndexOfFirstCharacter(items, sortKey, jumpToCharacter);
      if (index != null) {
        const row = Math.floor(index / columnCount);
        gridRef.current.scrollToCell({
          rowIndex: row,
          columnIndex: 0
        });
      }
    }
  }, [jumpToCharacter, items, sortKey, columnCount]);

  const onMeasure = useCallback(({ width: measuredWidth }) => {
    calculateGrid(measuredWidth, isSmallScreen);
  }, [calculateGrid, isSmallScreen]);

  const cellRenderer = useCallback(({ key, rowIndex, columnIndex, style }) => {
    const {
      detailedProgressBar,
      showTitle,
      showMonitored
    } = posterOptions;

    const mangaIdx = rowIndex * columnCount + columnIndex;
    const manga = items[mangaIdx];

    if (!manga) {
      return null;
    }

    return (
      <div
        key={key}
        style={{
          ...style,
          padding: _padding
        }}
      >
        <MangaIndexPoster
          key={manga.id}
          id={manga.id}
          title={manga.title}
          monitored={manga.monitored}
          titleSlug={manga.titleSlug}
          status={manga.status}
          statistics={manga.statistics}
          images={manga.images || []}
          posterWidth={posterWidth}
          posterHeight={posterHeight}
          detailedProgressBar={detailedProgressBar}
          showTitle={showTitle}
          showMonitored={showMonitored}
          showSearchAction={false}
          isRefreshingManga={false}
          isSearchingManga={false}
          onRefreshMangaPress={() => {}}
          onSearchPress={() => {}}
          isEditorActive={isEditorActive}
          isSelected={selectedState[manga.id]}
          onSelectedChange={onSelectedChange}
        />
      </div>
    );
  }, [items, columnCount, posterOptions, posterWidth, posterHeight, _padding, isEditorActive, selectedState, onSelectedChange]);

  const rowCount = Math.ceil(items.length / columnCount);

  return (
    <Measure
      onMeasure={onMeasure}
    >
      <WindowScroller
        scrollElement={typeof window !== "undefined" ? window : undefined}
      >
        {({ height, registerChild, onChildScroll, scrollTop }) => {
          if (!height) {
            return <div />;
          }

          return (
            <div ref={registerChild}>
              <Grid
                ref={gridRef}
                className={styles.grid}
                autoHeight={true}
                height={height}
                columnCount={columnCount}
                columnWidth={columnWidth}
                rowCount={rowCount}
                rowHeight={rowHeight}
                width={width}
                onScroll={onChildScroll}
                scrollTop={scrollTop}
                overscanRowCount={2}
                cellRenderer={cellRenderer}
                scrollToAlignment={'start'}
                isScrollingOptOut={true}
              />
            </div>
          );
        }
        }
      </WindowScroller>
    </Measure>
  );
}

MangaIndexPosters.propTypes = {
  items: PropTypes.arrayOf(PropTypes.object).isRequired,
  sortKey: PropTypes.string,
  posterOptions: PropTypes.object.isRequired,
  jumpToCharacter: PropTypes.string,
  scrollTop: PropTypes.number.isRequired,
  scroller: PropTypes.instanceOf(Element).isRequired,
  isSmallScreen: PropTypes.bool.isRequired,
  selectedState: PropTypes.object.isRequired,
  onSelectedChange: PropTypes.func.isRequired,
  isEditorActive: PropTypes.bool.isRequired
};

export default MangaIndexPosters;
