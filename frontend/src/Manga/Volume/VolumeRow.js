import PropTypes from 'prop-types';
import React from 'react';
import Icon from 'Components/Icon';
import MonitorToggleButton from 'Components/MonitorToggleButton';
import RelativeDateCellConnector from 'Components/Table/Cells/RelativeDateCellConnector';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import TableRow from 'Components/Table/TableRow';
import { icons, kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';

function VolumeRow(props) {
  const {
    id,
    title,
    monitored,
    releaseDate,
    pageCount,
    isSaving,
    mangaId,
    onMonitorVolumePress,
    columns
  } = props;

  const onMonitorPress = (newMonitored, options) => {
    onMonitorVolumePress(id, newMonitored, options);
  };

  return (
    <TableRow>
      {
        columns.map((column) => {
          const { name } = column;

          if (name === 'monitored') {
            return (
              <TableRowCell key={name}>
                <MonitorToggleButton
                  monitored={monitored}
                  isSaving={isSaving}
                  onPress={onMonitorPress}
                />
              </TableRowCell>
            );
          }

          if (name === 'title') {
            return (
              <TableRowCell key={name}>
                {title}
              </TableRowCell>
            );
          }

          if (name === 'releaseDate') {
            return (
              <RelativeDateCellConnector
                key={name}
                date={releaseDate}
              />
            );
          }

          if (name === 'pageCount') {
            return (
              <TableRowCell key={name}>
                {pageCount}
              </TableRowCell>
            );
          }

          if (name === 'status') {
            const hasFile = pageCount > 0;
            const isReleased = releaseDate && new Date(releaseDate) <= new Date();

            let statusIcon = icons.SERIES;
            let statusKind = kinds.DEFAULT;
            let statusTitle = 'Not Downloaded';

            if (hasFile) {
              statusIcon = icons.DOWNLOADED;
              statusKind = kinds.SUCCESS;
              statusTitle = 'Downloaded';
            } else if (isReleased) {
              statusIcon = icons.MISSING;
              statusKind = kinds.DANGER;
              statusTitle = 'Missing';
            }

            return (
              <TableRowCell key={name}>
                <Icon
                  name={statusIcon}
                  kind={statusKind}
                  title={statusTitle}
                />
              </TableRowCell>
            );
          }

          if (name === 'actions') {
            return (
              <TableRowCell key={name}>
                {/* Search actions placeholder */}
              </TableRowCell>
            );
          }

          if (name === 'select') {
            return null;
          }

          return null;
        })
      }
    </TableRow>
  );
}

VolumeRow.propTypes = {
  id: PropTypes.number.isRequired,
  title: PropTypes.string.isRequired,
  monitored: PropTypes.bool.isRequired,
  releaseDate: PropTypes.string,
  pageCount: PropTypes.number,
  isSaving: PropTypes.bool,
  mangaId: PropTypes.number.isRequired,
  onMonitorVolumePress: PropTypes.func.isRequired,
  columns: PropTypes.arrayOf(PropTypes.object).isRequired
};

VolumeRow.defaultProps = {
  pageCount: 0,
  isSaving: false
};

export default VolumeRow;
