import { createSelector } from 'reselect';

function createMangaSelector() {
  return createSelector(
    (state, { mangaId }) => mangaId,
    (state) => state.manga.itemMap,
    (state) => state.manga.items,
    (mangaId, itemMap, allManga) => {
      return allManga[itemMap[mangaId]];
    }
  );
}

export default createMangaSelector;
