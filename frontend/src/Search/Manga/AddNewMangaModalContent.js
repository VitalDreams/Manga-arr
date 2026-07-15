import PropTypes from 'prop-types';
import React, { Component } from 'react';
import TextTruncate from 'react-text-truncate';
import SpinnerButton from 'Components/Link/SpinnerButton';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { kinds } from 'Helpers/Props';
import getProxiedCoverUrl from 'Utilities/Manga/getProxiedCoverUrl';
import translate from 'Utilities/String/translate';
import AddAuthorOptionsForm from '../Common/AddAuthorOptionsForm.js';
import styles from './AddNewMangaModalContent.css';

class AddNewMangaModalContent extends Component {

  //
  // Render

  render() {
    const {
      title,
      author,
      coverUrl,
      overview,
      isAdding,
      isSmallScreen,
      onModalClose,
      onAddMangaPress,
      ...otherProps
    } = this.props;

    return (
      <ModalContent onModalClose={onModalClose}>
        <ModalHeader>
          Add New Manga
        </ModalHeader>

        <ModalBody>
          <div className={styles.container}>
            {
              isSmallScreen || !coverUrl ?
                null:
                <div className={styles.poster}>
                  <img
                    className={styles.posterImage}
                    src={getProxiedCoverUrl(coverUrl)}
                  />
                </div>
            }

            <div className={styles.info}>
              <div className={styles.title}>
                {title}
              </div>

              {
                author ?
                  <div className={styles.author}>
                    By: {author}
                  </div> :
                  null
              }

              {
                overview ?
                  <div className={styles.overview}>
                    <TextTruncate
                      truncateText="…"
                      line={8}
                      text={overview}
                    />
                  </div> :
                  null
              }

              <AddAuthorOptionsForm
                includeNoneMetadataProfile={true}
                {...otherProps}
              />
            </div>
          </div>
        </ModalBody>

        <ModalFooter className={styles.modalFooter}>
          <SpinnerButton
            className={styles.addButton}
            kind={kinds.SUCCESS}
            isSpinning={isAdding}
            onPress={onAddMangaPress}
          >
            Add {title}
          </SpinnerButton>
        </ModalFooter>
      </ModalContent>
    );
  }
}

AddNewMangaModalContent.propTypes = {
  title: PropTypes.string.isRequired,
  author: PropTypes.string,
  coverUrl: PropTypes.string,
  overview: PropTypes.string,
  isAdding: PropTypes.bool.isRequired,
  addError: PropTypes.object,
  isSmallScreen: PropTypes.bool.isRequired,
  onModalClose: PropTypes.func.isRequired,
  onAddMangaPress: PropTypes.func.isRequired
};

export default AddNewMangaModalContent;
