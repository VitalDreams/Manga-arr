import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { addManga, setMangaAddDefault } from 'Store/Actions/searchActions';
import createDimensionsSelector from 'Store/Selectors/createDimensionsSelector';
import selectSettings from 'Store/Selectors/selectSettings';
import AddNewMangaModalContent from './AddNewMangaModalContent';

function createMapStateToProps() {
  return createSelector(
    (state) => state.search,
    createDimensionsSelector(),
    (searchState, dimensions) => {
      const {
        isAdding,
        isAdded,
        addError,
        mangaDefaults
      } = searchState;

      const {
        settings,
        validationErrors,
        validationWarnings
      } = selectSettings(mangaDefaults, {}, addError);

      return {
        isAdding,
        isAdded,
        addError,
        isSmallScreen: dimensions.isSmallScreen,
        validationErrors,
        validationWarnings,
        ...settings
      };
    }
  );
}

const mapDispatchToProps = {
  setMangaAddDefault,
  addManga
};

class AddNewMangaModalContentConnector extends Component {

  //
  // Lifecycle

  componentDidUpdate(prevProps) {
    if (!prevProps.isAdded && this.props.isAdded) {
      this.props.onModalClose();
    }
  }

  //
  // Listeners

  onInputChange = ({ name, value }) => {
    this.props.setMangaAddDefault({ [name]: value });
  };

  onAddMangaPress = () => {
    const {
      foreignMangaId,
      rootFolderPath,
      qualityProfileId,
      metadataProfileId,
      tags
    } = this.props;

    this.props.addManga({
      foreignMangaId,
      rootFolderPath: rootFolderPath.value,
      qualityProfileId: qualityProfileId.value,
      metadataProfileId: metadataProfileId.value,
      tags: tags.value
    });
  };

  //
  // Render

  render() {
    return (
      <AddNewMangaModalContent
        {...this.props}
        onInputChange={this.onInputChange}
        onAddMangaPress={this.onAddMangaPress}
      />
    );
  }
}

AddNewMangaModalContentConnector.propTypes = {
  foreignMangaId: PropTypes.string.isRequired,
  rootFolderPath: PropTypes.object,
  qualityProfileId: PropTypes.object,
  metadataProfileId: PropTypes.object,
  tags: PropTypes.object.isRequired,
  isAdded: PropTypes.bool.isRequired,
  onModalClose: PropTypes.func.isRequired,
  setMangaAddDefault: PropTypes.func.isRequired,
  addManga: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(AddNewMangaModalContentConnector);
