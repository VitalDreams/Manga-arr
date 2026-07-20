import PropTypes from 'prop-types';
import React, { useState, useCallback } from 'react';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import { sortDirections } from 'Helpers/Props';
import getToggledRange from 'Utilities/Table/getToggledRange';
import VolumeRow from '../Volume/VolumeRow';
import styles from './MangaDetailsSeason.css';

function MangaDetailsSeason(props) {
  const {
    items,
    columns,
    sortKey,
    sortDirection,
    mangaId,
    isSmallScreen,
    onSortPress,
    onTableOptionChange,
    onMonitorVolumePress
  } = props;

  const [lastToggledVolume, setLastToggledVolume] = useState(null);

  const onMonitorVolumeItemPress = useCallback((volumeId, monitored, { shiftKey }) => {
    const volumeIds = [volumeId];

    if (shiftKey && lastToggledVolume) {
      const { lower, upper } = getToggledRange(items, volumeId, lastToggledVolume);

      for (let i = lower; i < upper; i++) {
        volumeIds.push(items[i].id);
      }
    }

    setLastToggledVolume(volumeId);
    onMonitorVolumePress(volumeIds, monitored);
  }, [items, lastToggledVolume, onMonitorVolumePress]);

  return (
    <Table
      columns={columns}
      sortKey={sortKey}
      sortDirection={sortDirection}
      onSortPress={onSortPress}
      onTableOptionChange={onTableOptionChange}
    >
      <TableBody>
        {
          items.map((volume) => {
            return (
              <VolumeRow
                key={volume.id}
                {...volume}
                mangaId={mangaId}
                onMonitorVolumePress={onMonitorVolumeItemPress}
                columns={columns}
              />
            );
          })
        }
      </TableBody>
    </Table>
  );
}

MangaDetailsSeason.propTypes = {
  items: PropTypes.arrayOf(PropTypes.object).isRequired,
  columns: PropTypes.arrayOf(PropTypes.object).isRequired,
  sortKey: PropTypes.string.isRequired,
  sortDirection: PropTypes.string.isRequired,
  mangaId: PropTypes.number.isRequired,
  isSmallScreen: PropTypes.bool.isRequired,
  onSortPress: PropTypes.func.isRequired,
  onTableOptionChange: PropTypes.func.isRequired,
  onMonitorVolumePress: PropTypes.func.isRequired
};

export default MangaDetailsSeason;
