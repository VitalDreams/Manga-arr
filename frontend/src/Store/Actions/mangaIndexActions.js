import { createAction } from 'redux-actions';
import { filterBuilderTypes, filterBuilderValueTypes, sortDirections } from 'Helpers/Props';
import { createThunk, handleThunks } from 'Store/thunks';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import { filterPredicates, filters, sortPredicates } from './mangaActions';
import { set, updateItem } from './baseActions';
import createHandleActions from './Creators/createHandleActions';
import createSetClientSideCollectionFilterReducer from './Creators/Reducers/createSetClientSideCollectionFilterReducer';
import createSetClientSideCollectionSortReducer from './Creators/Reducers/createSetClientSideCollectionSortReducer';
import createSetTableOptionReducer from './Creators/Reducers/createSetTableOptionReducer';

//
// Variables

export const section = 'mangaIndex';

//
// State

export const defaultState = {
  isSaving: false,
  saveError: null,
  isDeleting: false,
  deleteError: null,
  sortKey: 'title',
  sortDirection: sortDirections.ASCENDING,
  secondarySortKey: 'title',
  secondarySortDirection: sortDirections.ASCENDING,
  view: 'posters',

  posterOptions: {
    detailedProgressBar: false,
    size: 'large',
    showTitle: 'firstLast',
    showMonitored: true,
    showSearchAction: false
  },

  overviewOptions: {
    showTitle: 'firstLast',
    detailedProgressBar: false,
    size: 'medium',
    showMonitored: true,
    showLastBook: false,
    showAdded: false,
    showBookCount: true,
    showPath: false,
    showSizeOnDisk: false,
    showSearchAction: false
  },

  tableOptions: {
    showTitle: 'firstLast',
    showSearchAction: false
  },

  columns: [
    {
      name: 'select',
      columnLabel: 'Select',
      isSortable: false,
      isVisible: true,
      isModifiable: false,
      isHidden: true
    },
    {
      name: 'status',
      columnLabel: 'Status',
      isSortable: true,
      isVisible: true,
      isModifiable: false
    },
    {
      name: 'title',
      label: 'Title',
      isSortable: true,
      isVisible: true,
      isModifiable: false
    },
    {
      name: 'year',
      label: 'Year',
      isSortable: true,
      isVisible: true
    },
    {
      name: 'added',
      label: 'Added',
      isSortable: true,
      isVisible: true
    },
    {
      name: 'bookCount',
      label: 'Volumes',
      isSortable: true,
      isVisible: true
    },
    {
      name: 'path',
      label: 'Path',
      isSortable: true,
      isVisible: false
    },
    {
      name: 'sizeOnDisk',
      label: 'Size on Disk',
      isSortable: true,
      isVisible: false
    },
    {
      name: 'genres',
      label: 'Genres',
      isSortable: false,
      isVisible: false
    },
    {
      name: 'ratings',
      label: 'Rating',
      isSortable: true,
      isVisible: true
    },
    {
      name: 'actions',
      columnLabel: 'Actions',
      isVisible: true,
      isModifiable: false
    }
  ],

  filterBuilderProps: [
    {
      name: 'monitored',
      label: 'Monitored',
      type: filterBuilderTypes.EXACT,
      valueType: filterBuilderValueTypes.BOOL
    },
    {
      name: 'status',
      label: 'Status',
      type: filterBuilderTypes.EXACT,
      valueType: filterBuilderValueTypes.MOVIE_STATUS
    },
    {
      name: 'year',
      label: 'Year',
      type: filterBuilderTypes.NUMBER
    },
    {
      name: 'ratings',
      label: 'Rating',
      type: filterBuilderTypes.NUMBER
    },
    {
      name: 'added',
      label: 'Added',
      type: filterBuilderTypes.DATE
    },
    {
      name: 'bookCount',
      label: 'Volume Count',
      type: filterBuilderTypes.NUMBER
    },
    {
      name: 'sizeOnDisk',
      label: 'Size on Disk',
      type: filterBuilderTypes.NUMBER
    }
  ],

  selectedFilterKey: 'all'
};

export const persistState = [
  'mangaIndex.sortKey',
  'mangaIndex.sortDirection',
  'mangaIndex.view',
  'mangaIndex.posterOptions',
  'mangaIndex.overviewOptions',
  'mangaIndex.tableOptions',
  'mangaIndex.columns',
  'mangaIndex.selectedFilterKey'
];

//
// Actions Types

export const SET_MANGA_SORT = 'mangaIndex/setMangaSort';
export const SET_MANGA_FILTER = 'mangaIndex/setMangaFilter';
export const SET_MANGA_VIEW = 'mangaIndex/setMangaView';
export const SET_MANGA_TABLE_OPTION = 'mangaIndex/setMangaTableOption';
export const SAVE_MANGA_EDITOR = 'mangaIndex/saveMangaEditor';

//
// Action Creators

export const setMangaSort = createAction(SET_MANGA_SORT);
export const setMangaFilter = createAction(SET_MANGA_FILTER);
export const setMangaView = createAction(SET_MANGA_VIEW);
export const setMangaTableOption = createAction(SET_MANGA_TABLE_OPTION);
export const saveMangaEditor = createThunk(SAVE_MANGA_EDITOR);

//
// Action Handlers

export const actionHandlers = handleThunks({

  [SAVE_MANGA_EDITOR]: function(getState, payload, dispatch) {
    const {
      ids,
      monitored,
      ...otherPayload
    } = payload;

    dispatch(set({ section, isSaving: true }));

    const promise = createAjaxRequest({
      url: '/manga/editor',
      method: 'PUT',
      dataType: 'json',
      data: JSON.stringify({
        mangaIds: ids,
        monitored,
        ...otherPayload
      })
    }).request;

    promise.done(() => {
      ids.forEach((id) => {
        if (monitored !== undefined) {
          dispatch(updateItem({
            id,
            section: 'manga',
            monitored
          }));
        }
      });

      dispatch(set({
        section,
        isSaving: false,
        saveError: null
      }));
    });

    promise.fail((xhr) => {
      dispatch(set({
        section,
        isSaving: false,
        saveError: xhr
      }));
    });
  }
});

//
// Reducers

export const reducers = createHandleActions({

  [SET_MANGA_SORT]: createSetClientSideCollectionSortReducer(section, filters, filterPredicates, sortPredicates),

  [SET_MANGA_FILTER]: createSetClientSideCollectionFilterReducer(section, filters, filterPredicates),

  [SET_MANGA_VIEW]: function(state, { payload }) {
    const newState = { ...state };
    newState.view = payload.view;
    return newState;
  },

  [SET_MANGA_TABLE_OPTION]: createSetTableOptionReducer(section)

}, defaultState, section);
