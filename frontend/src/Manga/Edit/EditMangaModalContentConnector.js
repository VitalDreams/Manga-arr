import _ from 'lodash';
import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { saveManga, setMangaValue } from 'Store/Actions/mangaActions';
import selectSettings from 'Store/Selectors/selectSettings';
import EditMangaModalContent from './EditMangaModalContent';

function createMapStateToProps() {
  return createSelector(
    (state, { mangaId }) => _.find(state.manga.items, { id: mangaId }),
    (state) => state.manga,
    (manga, mangaState) => {
      if (!manga) {
        return {
          isSaving: false,
          saveError: null,
          item: {},
          mangaTitle: ''
        };
      }

      const {
        isSaving,
        saveError,
        pendingChanges
      } = mangaState;

      const mangaSettings = _.pick(manga, [
        'monitored',
        'path',
        'tags'
      ]);

      const settings = selectSettings(mangaSettings, pendingChanges, saveError);

      return {
        mangaTitle: manga.title,
        isSaving,
        saveError,
        item: settings.settings,
        ...settings
      };
    }
  );
}

const mapDispatchToProps = {
  dispatchSetMangaValue: setMangaValue,
  dispatchSaveManga: saveManga
};

class EditMangaModalContentConnector extends Component {

  //
  // Lifecycle

  componentDidUpdate(prevProps) {
    if (prevProps.isSaving && !this.props.isSaving && !this.props.saveError) {
      this.props.onModalClose();
    }
  }

  //
  // Listeners

  onInputChange = ({ name, value }) => {
    this.props.dispatchSetMangaValue({ name, value });
  };

  onSavePress = () => {
    this.props.dispatchSaveManga({
      id: this.props.mangaId
    });
  };

  //
  // Render

  render() {
    return (
      <EditMangaModalContent
        {...this.props}
        onInputChange={this.onInputChange}
        onSavePress={this.onSavePress}
      />
    );
  }
}

EditMangaModalContentConnector.propTypes = {
  mangaId: PropTypes.number.isRequired,
  isSaving: PropTypes.bool.isRequired,
  saveError: PropTypes.object,
  dispatchSetMangaValue: PropTypes.func.isRequired,
  dispatchSaveManga: PropTypes.func.isRequired,
  onModalClose: PropTypes.func.isRequired,
  onDeleteMangaPress: PropTypes.func
};

export default connect(createMapStateToProps, mapDispatchToProps)(EditMangaModalContentConnector);
