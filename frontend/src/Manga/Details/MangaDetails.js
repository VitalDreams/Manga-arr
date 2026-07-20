import PropTypes from 'prop-types';
import React, { useState } from 'react';
import { Tab, TabList, TabPanel, Tabs } from 'react-tabs';
import MangaPoster from 'Manga/MangaPoster';
import MonitorToggleButton from 'Components/MonitorToggleButton';
import IconButton from 'Components/Link/IconButton';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageToolbar from 'Components/Page/Toolbar/PageToolbar';
import PageToolbarButton from 'Components/Page/Toolbar/PageToolbarButton';
import PageToolbarSection from 'Components/Page/Toolbar/PageToolbarSection';
import { icons } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import MangaDetailsSeasonConnector from './MangaDetailsSeasonConnector';
import styles from './MangaDetails.css';

function MangaDetails(props) {
  const {
    manga,
    isSmallScreen,
    isRefreshingManga,
    onRefreshMangaPress,
    onMonitorTogglePress
  } = props;

  const {
    id,
    title,
    year,
    overview,
    status,
    monitored,
    coverUrl,
    images = [],
    author,
    artist,
    path,
    totalVolumes,
    totalChapters,
    statistics = {}
  } = manga;

  const {
    bookCount = 0,
    bookFileCount = 0,
    sizeOnDisk = 0
  } = statistics;

  const [selectedTabIndex, setSelectedTabIndex] = useState(0);

  return (
    <PageContent title={title}>
      <PageToolbar>
        <PageToolbarSection>
          <PageToolbarButton
            label={translate('RefreshManga')}
            iconName={icons.REFRESH}
            isSpinning={isRefreshingManga}
            onPress={onRefreshMangaPress}
          />

          <PageToolbarButton
            label={monitored ? translate('Unmonitor') : translate('Monitor')}
            iconName={monitored ? icons.MONITORED : icons.UNMONITORED}
            onPress={() => onMonitorTogglePress(!monitored)}
          />
        </PageToolbarSection>
      </PageToolbar>

      <PageContentBody>
        <div className={styles.header}>
          <div className={styles.posterContainer}>
            <MangaPoster
              className={styles.poster}
              images={images}
              size={250}
              lazy={false}
              overflow={true}
              blurBackground={true}
            />
          </div>

          <div className={styles.info}>
            <div className={styles.titleRow}>
              <div className={styles.title}>
                {title}
              </div>

              {
                year ?
                  <div className={styles.year}>
                    ({year})
                  </div> :
                  null
              }
            </div>

            {
              overview ?
                <div className={styles.overview}>
                  {overview}
                </div> :
                null
            }

            <div className={styles.details}>
              {
                author &&
                  <div className={styles.detailRow}>
                    <div className={styles.detailLabel}>
                      {translate('Author')}
                    </div>
                    <div className={styles.detailValue}>
                      {author}
                    </div>
                  </div>
              }

              {
                artist &&
                  <div className={styles.detailRow}>
                    <div className={styles.detailLabel}>
                      {translate('Artist')}
                    </div>
                    <div className={styles.detailValue}>
                      {artist}
                    </div>
                  </div>
              }

              <div className={styles.detailRow}>
                <div className={styles.detailLabel}>
                  {translate('Status')}
                </div>
                <div className={styles.detailValue}>
                  {status}
                </div>
              </div>

              {
                path &&
                  <div className={styles.detailRow}>
                    <div className={styles.detailLabel}>
                      {translate('Path')}
                    </div>
                    <div className={styles.detailValue}>
                      {path}
                    </div>
                  </div>
              }
            </div>

            <div className={styles.stats}>
              <div className={styles.statItem}>
                <div className={styles.statLabel}>
                  {translate('Volumes')}
                </div>
                <div className={styles.statValue}>
                  {bookFileCount} / {bookCount}
                </div>
              </div>

              {
                totalVolumes > 0 &&
                  <div className={styles.statItem}>
                    <div className={styles.statLabel}>
                      {translate('TotalVolumes')}
                    </div>
                    <div className={styles.statValue}>
                      {totalVolumes}
                    </div>
                  </div>
              }

              {
                totalChapters > 0 &&
                  <div className={styles.statItem}>
                    <div className={styles.statLabel}>
                      {translate('TotalChapters')}
                    </div>
                    <div className={styles.statValue}>
                      {totalChapters}
                    </div>
                  </div>
              }

              <div className={styles.statItem}>
                <div className={styles.statLabel}>
                  {translate('SizeOnDisk')}
                </div>
                <div className={styles.statValue}>
                  {sizeOnDisk}
                </div>
              </div>
            </div>
          </div>
        </div>

        <Tabs
          className={styles.tabs}
          selectedIndex={selectedTabIndex}
          onSelect={setSelectedTabIndex}
        >
          <TabList>
            <Tab>{translate('Volumes')}</Tab>
            <Tab>{translate('History')}</Tab>
          </TabList>

          <TabPanel>
            <MangaDetailsSeasonConnector
              mangaId={id}
            />
          </TabPanel>

          <TabPanel>
            <div>
              {translate('NoHistory')}
            </div>
          </TabPanel>
        </Tabs>
      </PageContentBody>
    </PageContent>
  );
}

MangaDetails.propTypes = {
  manga: PropTypes.object.isRequired,
  isSmallScreen: PropTypes.bool.isRequired,
  isRefreshingManga: PropTypes.bool.isRequired,
  onRefreshMangaPress: PropTypes.func.isRequired,
  onMonitorTogglePress: PropTypes.func.isRequired
};

export default MangaDetails;
