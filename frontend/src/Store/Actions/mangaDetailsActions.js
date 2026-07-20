import { createAction } from 'redux-actions';
import { sortDirections } from 'Helpers/Props';
import { createThunk, handleThunks } from 'Store/thunks';
import createHandleActions from './Creators/createHandleActions';
import createSetClientSideCollectionSortReducer from './Creators/Reducers/createSetClientSideCollectionSortReducer';

//
// Variables

export const section = 'mangaDetails';

//
// State

export const defaultState = {
  sortKey: 'releaseDate',
  sortDirection: sortDirections.DESCENDING,
  secondarySortKey: 'releaseDate',
  secondarySortDirection: sortDirections.DESCENDING,
  selectedFilterKey: 'all'
};

export const persistState = [
  'mangaDetails.sortKey',
  'mangaDetails.sortDirection'
];

//
// Actions Types

export const SET_MANGA_DETAILS_ID = 'mangaDetails/setMangaDetailsId';
export const SET_MANGA_DETAILS_SORT = 'mangaDetails/setMangaDetailsSort';

//
// Action Creators

export const setMangaDetailsId = createAction(SET_MANGA_DETAILS_ID);
export const setMangaDetailsSort = createAction(SET_MANGA_DETAILS_SORT);

//
// Reducers

export const reducers = createHandleActions({

  [SET_MANGA_DETAILS_ID]: function(state, { payload }) {
    const { mangaId } = payload;
    return {
      ...state,
      selectedFilterKey: 'all'
    };
  },

  [SET_MANGA_DETAILS_SORT]: createSetClientSideCollectionSortReducer(section)

}, defaultState, section);
