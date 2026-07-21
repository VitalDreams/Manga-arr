import PropTypes from 'prop-types';
import React from 'react';
import Modal from 'Components/Modal/Modal';
import { sizes } from 'Helpers/Props';
import BookInteractiveSearchModalContent from './BookInteractiveSearchModalContent';

function BookInteractiveSearchModal(props) {
  const {
    isOpen,
    bookId,
    bookTitle,
    authorName,
    isManga,
    onModalClose
  } = props;

  return (
    <Modal
      isOpen={isOpen}
      size={sizes.EXTRA_EXTRA_LARGE}
      closeOnBackgroundClick={false}
      onModalClose={onModalClose}
    >
      <BookInteractiveSearchModalContent
        bookId={bookId}
        bookTitle={bookTitle}
        authorName={authorName}
        isManga={isManga}
        onModalClose={onModalClose}
      />
    </Modal>
  );
}

BookInteractiveSearchModal.propTypes = {
  isOpen: PropTypes.bool.isRequired,
  bookId: PropTypes.number.isRequired,
  bookTitle: PropTypes.string.isRequired,
  authorName: PropTypes.string.isRequired,
  isManga: PropTypes.bool,
  onModalClose: PropTypes.func.isRequired
};

export default BookInteractiveSearchModal;
