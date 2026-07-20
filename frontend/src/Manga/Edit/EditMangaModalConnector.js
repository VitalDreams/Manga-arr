import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { clearPendingChanges } from 'Store/Actions/baseActions';
import EditMangaModal from './EditMangaModal';

const mapDispatchToProps = {
  clearPendingChanges
};

class EditMangaModalConnector extends Component {

  //
  // Listeners

  onModalClose = () => {
    this.props.clearPendingChanges({ section: 'manga' });
    this.props.onModalClose();
  };

  //
  // Render

  render() {
    return (
      <EditMangaModal
        {...this.props}
        onModalClose={this.onModalClose}
      />
    );
  }
}

EditMangaModalConnector.propTypes = {
  onModalClose: PropTypes.func.isRequired,
  clearPendingChanges: PropTypes.func.isRequired
};

export default connect(undefined, mapDispatchToProps)(EditMangaModalConnector);
