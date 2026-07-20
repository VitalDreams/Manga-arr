import { createSelector, createSelectorCreator, defaultMemoize } from 'reselect';
import hasDifferentItemsOrOrder from 'Utilities/Object/hasDifferentItemsOrOrder';
import createClientSideCollectionSelector from './createClientSideCollectionSelector';

function createUnoptimizedSelector(uiSection) {
  return createSelector(
    createClientSideCollectionSelector('manga', uiSection),
    (manga) => {
      const items = manga.items.map((m) => {
        const {
          id,
          title
        } = m;

        return {
          id,
          title
        };
      });

      return {
        ...manga,
        items
      };
    }
  );
}

function mangaListEqual(a, b) {
  return hasDifferentItemsOrOrder(a, b);
}

const createMangaEqualSelector = createSelectorCreator(
  defaultMemoize,
  mangaListEqual
);

function createMangaClientSideCollectionItemsSelector(uiSection) {
  return createMangaEqualSelector(
    createUnoptimizedSelector(uiSection),
    (manga) => manga
  );
}

export default createMangaClientSideCollectionItemsSelector;
