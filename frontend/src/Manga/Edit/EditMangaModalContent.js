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

class EditMangaModalContent extends Component {

  //
  // Render

  render() {
    const {
      mangaTitle,
      item,
      isSaving,
      onInputChange,
      onSavePress,
      onModalClose,
      onDeleteMangaPress,
      ...otherProps
    } = this.props;

    const {
      monitored,
      path,
      tags
    } = item;

    return (
      <ModalContent onModalClose={onModalClose}>
        <ModalHeader>
          Edit - {mangaTitle}
        </ModalHeader>

        <ModalBody>
          <Form {...otherProps}>
            <FormGroup>
              <FormLabel>
                {translate('Monitored')}
              </FormLabel>

              <FormInputGroup
                type={inputTypes.CHECK}
                name="monitored"
                helpText={translate('MonitoredHelpText')}
                {...monitored}
                onChange={onInputChange}
              />
            </FormGroup>

            <FormGroup>
              <FormLabel>
                {translate('Path')}
              </FormLabel>

              <FormInputGroup
                type={inputTypes.PATH}
                name="path"
                {...path}
                onChange={onInputChange}
              />
            </FormGroup>

            <FormGroup>
              <FormLabel>
                {translate('Tags')}
              </FormLabel>

              <FormInputGroup
                type={inputTypes.TAG}
                name="tags"
                {...tags}
                onChange={onInputChange}
              />
            </FormGroup>
          </Form>
        </ModalBody>
        <ModalFooter>
          {
            onDeleteMangaPress &&
              <Button
                kind={kinds.DANGER}
                onPress={onDeleteMangaPress}
              >
                Delete
              </Button>
          }

          <Button
            onPress={onModalClose}
          >
            Cancel
          </Button>

          <SpinnerButton
            isSpinning={isSaving}
            onPress={onSavePress}
          >
            Save
          </SpinnerButton>
        </ModalFooter>

      </ModalContent>
    );
  }
}

EditMangaModalContent.propTypes = {
  mangaId: PropTypes.number.isRequired,
  mangaTitle: PropTypes.string.isRequired,
  item: PropTypes.object.isRequired,
  isSaving: PropTypes.bool.isRequired,
  onInputChange: PropTypes.func.isRequired,
  onSavePress: PropTypes.func.isRequired,
  onModalClose: PropTypes.func.isRequired,
  onDeleteMangaPress: PropTypes.func
};

export default EditMangaModalContent;
