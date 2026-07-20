import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import Button from 'Components/Link/Button';
import SpinnerButton from 'Components/Link/SpinnerButton';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { inputTypes, kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';

class DeleteMangaModalContent extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.state = {
      deleteFiles: false
    };
  }

  //
  // Listeners

  onDeleteFilesChange = ({ value }) => {
    this.setState({ deleteFiles: value });
  };

  onDeletePress = () => {
    this.props.onDeletePress(this.state.deleteFiles);
  };

  //
  // Render

  render() {
    const {
      mangaTitle,
      onModalClose,
      isDeleting
    } = this.props;

    const { deleteFiles } = this.state;

    return (
      <ModalContent onModalClose={onModalClose}>
        <ModalHeader>
          Delete - {mangaTitle}
        </ModalHeader>

        <ModalBody>
          <Form>
            <FormGroup>
              <FormLabel>
                {translate('DeleteFiles')}
              </FormLabel>

              <FormInputGroup
                type={inputTypes.CHECK}
                name="deleteFiles"
                value={deleteFiles}
                helpText={translate('DeleteFilesHelpText')}
                onChange={this.onDeleteFilesChange}
              />
            </FormGroup>
          </Form>
        </ModalBody>

        <ModalFooter>
          <Button
            onPress={onModalClose}
          >
            Cancel
          </Button>

          <SpinnerButton
            kind={kinds.DANGER}
            isSpinning={isDeleting}
            onPress={this.onDeletePress}
          >
            Delete
          </SpinnerButton>
        </ModalFooter>
      </ModalContent>
    );
  }
}

DeleteMangaModalContent.propTypes = {
  mangaTitle: PropTypes.string.isRequired,
  isDeleting: PropTypes.bool,
  onDeletePress: PropTypes.func.isRequired,
  onModalClose: PropTypes.func.isRequired
};

DeleteMangaModalContent.defaultProps = {
  isDeleting: false
};

export default DeleteMangaModalContent;
