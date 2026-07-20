import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { deleteManga } from 'Store/Actions/mangaActions';
import _ from 'lodash';
import DeleteMangaModalContent from './DeleteMangaModalContent';

function createMapStateToProps() {
  return createSelector(
    (state, { mangaId }) => _.find(state.manga.items, { id: mangaId }),
    (state) => state.manga,
    (manga, mangaState) => {
      return {
        mangaTitle: manga ? manga.title : '',
        isDeleting: mangaState.isDeleting || false
      };
    }
  );
}

const mapDispatchToProps = {
  deleteManga
};

class DeleteMangaModalContentConnector extends Component {

  //
  // Listeners

  onDeletePress = (deleteFiles) => {
    this.props.deleteManga({
      id: this.props.mangaId,
      deleteFiles
    });

    this.props.onModalClose(true);
  };

  //
  // Render

  render() {
    return (
      <DeleteMangaModalContent
        {...this.props}
        onDeletePress={this.onDeletePress}
      />
    );
  }
}

DeleteMangaModalContentConnector.propTypes = {
  mangaId: PropTypes.number.isRequired,
  onModalClose: PropTypes.func.isRequired,
  deleteManga: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(DeleteMangaModalContentConnector);
