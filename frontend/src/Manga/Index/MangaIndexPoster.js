import PropTypes from 'prop-types';
import React, { useState, useCallback } from 'react';
import MangaPoster from 'Manga/MangaPoster';
import EditMangaModalConnector from 'Manga/Edit/EditMangaModalConnector';
import DeleteMangaModal from 'Manga/Delete/DeleteMangaModal';
import AuthorIndexProgressBar from 'Author/Index/ProgressBar/AuthorIndexProgressBar';
import CheckInput from 'Components/Form/CheckInput';
import Label from 'Components/Label';
import IconButton from 'Components/Link/IconButton';
import Link from 'Components/Link/Link';
import SpinnerIconButton from 'Components/Link/SpinnerIconButton';
import { icons } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import styles from './MangaIndexPoster.css';

function MangaIndexPoster(props) {
  const {
    id,
    title,
    monitored,
    titleSlug,
    status,
    statistics = {},
    images,
    posterWidth,
    posterHeight,
    detailedProgressBar,
    showTitle,
    showMonitored,
    showSearchAction,
    isRefreshingManga,
    isSearchingManga,
    onRefreshMangaPress,
    onSearchPress,
    isEditorActive,
    isSelected,
    onSelectedChange
  } = props;

  const [hasPosterError, setHasPosterError] = useState(false);
  const [isEditMangaModalOpen, setIsEditMangaModalOpen] = useState(false);
  const [isDeleteMangaModalOpen, setIsDeleteMangaModalOpen] = useState(false);

  const onEditMangaPress = useCallback(() => {
    setIsEditMangaModalOpen(true);
  }, []);

  const onEditMangaModalClose = useCallback(() => {
    setIsEditMangaModalOpen(false);
  }, []);

  const onDeleteMangaPress = useCallback(() => {
    setIsEditMangaModalOpen(false);
    setIsDeleteMangaModalOpen(true);
  }, []);

  const onDeleteMangaModalClose = useCallback(() => {
    setIsDeleteMangaModalOpen(false);
  }, []);

  const {
    bookCount = 0,
    availableBookCount = 0,
    bookFileCount = 0,
    totalBookCount = 0
  } = statistics;

  const link = `/manga/${titleSlug}`;

  const elementStyle = {
    width: `${posterWidth}px`,
    height: `${posterHeight}px`,
    objectFit: 'contain'
  };

  const onPosterLoad = useCallback(() => {
    setHasPosterError(false);
  }, []);

  const onPosterLoadError = useCallback(() => {
    setHasPosterError(true);
  }, []);

  const onChange = useCallback(({ value, shiftKey }) => {
    onSelectedChange({ id, value, shiftKey });
  }, [id, onSelectedChange]);

  return (
    <div>
      <div className={styles.content}>
        <div className={styles.posterContainer}>
          {
            isEditorActive &&
              <div className={styles.editorSelect}>
                <CheckInput
                  className={styles.checkInput}
                  name={id.toString()}
                  value={isSelected}
                  onChange={onChange}
                />
              </div>
          }

          <Label className={styles.controls}>
            <SpinnerIconButton
              className={styles.action}
              name={icons.REFRESH}
              title={translate('RefreshManga')}
              isSpinning={isRefreshingManga}
              onPress={onRefreshMangaPress}
            />

            {
              showSearchAction &&
                <SpinnerIconButton
                  className={styles.action}
                  name={icons.SEARCH}
                  title={translate('SearchForMonitoredBooks')}
                  isSpinning={isSearchingManga}
                  onPress={onSearchPress}
                />
            }

            <IconButton
              className={styles.action}
              name={icons.EDIT}
              title={translate('EditManga')}
              onPress={onEditMangaPress}
            />
          </Label>

          {
            status === 'ended' &&
              <div
                className={styles.ended}
                title={translate('Ended')}
              />
          }

          <Link
            className={styles.link}
            style={elementStyle}
            to={link}
          >
            <MangaPoster
              className={styles.poster}
              style={elementStyle}
              images={images}
              size={250}
              lazy={false}
              overflow={true}
              blurBackground={true}
              onError={onPosterLoadError}
              onLoad={onPosterLoad}
            />

            {
              hasPosterError &&
                <div className={styles.overlayTitle}>
                  {title}
                </div>
            }

          </Link>
        </div>

        <AuthorIndexProgressBar
          monitored={monitored}
          status={status}
          bookCount={bookCount}
          availableBookCount={availableBookCount}
          bookFileCount={bookFileCount}
          totalBookCount={totalBookCount}
          posterWidth={posterWidth}
          detailedProgressBar={detailedProgressBar}
        />

        {
          showTitle !== 'no' &&
            <div className={styles.title}>
              {title}
            </div>
        }

        {
          showMonitored &&
            <div className={styles.title}>
              {monitored ? 'Monitored' : 'Unmonitored'}
            </div>
        }

        <EditMangaModalConnector
          isOpen={isEditMangaModalOpen}
          mangaId={id}
          onModalClose={onEditMangaModalClose}
          onDeleteMangaPress={onDeleteMangaPress}
        />

        <DeleteMangaModal
          isOpen={isDeleteMangaModalOpen}
          mangaId={id}
          onModalClose={onDeleteMangaModalClose}
        />
      </div>
    </div>
  );
}

MangaIndexPoster.propTypes = {
  id: PropTypes.number.isRequired,
  title: PropTypes.string.isRequired,
  monitored: PropTypes.bool.isRequired,
  status: PropTypes.string.isRequired,
  titleSlug: PropTypes.string.isRequired,
  statistics: PropTypes.object.isRequired,
  images: PropTypes.arrayOf(PropTypes.object).isRequired,
  posterWidth: PropTypes.number.isRequired,
  posterHeight: PropTypes.number.isRequired,
  detailedProgressBar: PropTypes.bool.isRequired,
  showTitle: PropTypes.string.isRequired,
  showMonitored: PropTypes.bool.isRequired,
  showSearchAction: PropTypes.bool.isRequired,
  isRefreshingManga: PropTypes.bool.isRequired,
  isSearchingManga: PropTypes.bool.isRequired,
  onRefreshMangaPress: PropTypes.func.isRequired,
  onSearchPress: PropTypes.func.isRequired,
  isEditorActive: PropTypes.bool.isRequired,
  isSelected: PropTypes.bool,
  onSelectedChange: PropTypes.func.isRequired
};

MangaIndexPoster.defaultProps = {
  statistics: {
    bookCount: 0,
    bookFileCount: 0,
    totalBookCount: 0
  }
};

export default MangaIndexPoster;
