import _ from 'lodash';
import { createAction } from 'redux-actions';
import { batchActions } from 'redux-batched-actions';
import { filterTypePredicates, filterTypes, sortDirections } from 'Helpers/Props';
import { createThunk, handleThunks } from 'Store/thunks';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import dateFilterPredicate from 'Utilities/Date/dateFilterPredicate';
import getProxiedCoverUrl from 'Utilities/Manga/getProxiedCoverUrl';
import { set, update, updateItem } from './baseActions';
import createHandleActions from './Creators/createHandleActions';
import createRemoveItemHandler from './Creators/createRemoveItemHandler';
import createSaveProviderHandler from './Creators/createSaveProviderHandler';
import createSetSettingValueReducer from './Creators/Reducers/createSetSettingValueReducer';

//
// Helpers

function prepareMangaForStore(manga) {
  const coverUrl = getProxiedCoverUrl(manga.coverUrl);
  const apiStats = manga.statistics || {};

  return {
    ...manga,
    images: coverUrl ? [{ coverType: 'poster', url: coverUrl }] : [],
    statistics: {
      bookCount: apiStats.totalVolumes ?? 0,
      bookFileCount: apiStats.downloadedVolumes ?? 0,
      availableBookCount: apiStats.downloadedVolumes ?? 0,
      totalBookCount: apiStats.totalVolumes ?? 0,
      sizeOnDisk: 0
    }
  };
}

function prepareMangaResponse(data) {
  if (Array.isArray(data)) {
    return data.map(prepareMangaForStore);
  }
  return prepareMangaForStore(data);
}

//
// Variables

export const section = 'manga';

export const filters = [
  {
    key: 'all',
    label: 'All',
    filters: []
  },
  {
    key: 'monitored',
    label: 'Monitored Only',
    filters: [
      {
        key: 'monitored',
        value: true,
        type: filterTypes.EQUAL
      }
    ]
  },
  {
    key: 'unmonitored',
    label: 'Unmonitored Only',
    filters: [
      {
        key: 'monitored',
        value: false,
        type: filterTypes.EQUAL
      }
    ]
  },
  {
    key: 'continuing',
    label: 'Continuing Only',
    filters: [
      {
        key: 'status',
        value: 'continuing',
        type: filterTypes.EQUAL
      }
    ]
  },
  {
    key: 'ended',
    label: 'Ended Only',
    filters: [
      {
        key: 'status',
        value: 'ended',
        type: filterTypes.EQUAL
      }
    ]
  }
];

export const filterPredicates = {
  missing: function(item) {
    const { statistics = {} } = item;
    return statistics.bookCount - statistics.bookFileCount > 0;
  },

  added: function(item, filterValue, type) {
    return dateFilterPredicate(item.added, filterValue, type);
  },

  ratings: function(item, filterValue, type) {
    const predicate = filterTypePredicates[type];
    return predicate(item.ratings ? item.ratings.value * 10 : 0, filterValue);
  },

  sizeOnDisk: function(item, filterValue, type) {
    const predicate = filterTypePredicates[type];
    const sizeOnDisk = item.statistics && item.statistics.sizeOnDisk ?
      item.statistics.sizeOnDisk : 0;
    return predicate(sizeOnDisk, filterValue);
  }
};

export const sortPredicates = {
  status: function(item) {
    let result = 0;
    if (item.monitored) {
      result += 2;
    }
    if (item.status === 'continuing') {
      result++;
    }
    return result;
  },

  sizeOnDisk: function(item) {
    const { statistics = {} } = item;
    return statistics.sizeOnDisk || 0;
  }
};

//
// State

export const defaultState = {
  isFetching: false,
  isPopulated: false,
  error: null,
  isSaving: false,
  saveError: null,
  items: [],
  sortKey: 'title',
  sortDirection: sortDirections.ASCENDING,
  pendingChanges: {}
};

//
// Actions Types

export const FETCH_MANGA = 'manga/fetchManga';
export const SET_MANGA_VALUE = 'manga/setMangaValue';
export const SAVE_MANGA = 'manga/saveManga';
export const DELETE_MANGA = 'manga/deleteManga';
export const TOGGLE_MANGA_MONITORED = 'manga/toggleMangaMonitored';

//
// Action Creators

export const fetchManga = createThunk(FETCH_MANGA);
export const saveManga = createThunk(SAVE_MANGA, (payload) => {
  const newPayload = { ...payload };
  if (payload.moveFiles) {
    newPayload.queryParams = { moveFiles: true };
  }
  delete newPayload.moveFiles;
  return newPayload;
});

export const deleteManga = createThunk(DELETE_MANGA, (payload) => {
  return {
    ...payload,
    queryParams: {
      deleteFiles: payload.deleteFiles,
      addImportListExclusion: payload.addImportListExclusion
    }
  };
});

export const toggleMangaMonitored = createThunk(TOGGLE_MANGA_MONITORED);

export const setMangaValue = createAction(SET_MANGA_VALUE, (payload) => {
  return {
    section,
    ...payload
  };
});

//
// Helpers

function getSaveAjaxOptions({ ajaxOptions, payload }) {
  if (payload.moveFolder) {
    ajaxOptions.url = `${ajaxOptions.url}?moveFolder=true`;
  }
  return ajaxOptions;
}

//
// Action Handlers

export const actionHandlers = handleThunks({

  [FETCH_MANGA]: function(getState, payload, dispatch) {
    console.log("FETCH_MANGA dispatched", payload);
    dispatch(set({ section, isFetching: true }));

    const { id, ...otherPayload } = payload;

    const { request, abortRequest } = createAjaxRequest({
      url: id == null ? '/manga' : `/manga/${id}`,
      data: otherPayload,
      traditional: true
    });

    request.done((data) => {
      console.log("FETCH_MANGA success", data.length, "items");
      const prepared = prepareMangaResponse(data);
      dispatch(batchActions([
        id == null ?
          update({ section, data: prepared }) :
          updateItem({ section, ...prepared }),
        set({
          section,
          isFetching: false,
          isPopulated: true,
          error: null
        })
      ]));
    });

    request.fail((xhr) => {
      console.log("FETCH_MANGA fail", xhr.status, xhr.responseText);
      dispatch(set({
        section,
        isFetching: false,
        isPopulated: false,
        error: xhr.aborted ? null : xhr
      }));
    });

    return abortRequest;
  },

  [SAVE_MANGA]: createSaveProviderHandler(section, '/manga', { getAjaxOptions: getSaveAjaxOptions }),
  [DELETE_MANGA]: createRemoveItemHandler(section, '/manga'),

  [TOGGLE_MANGA_MONITORED]: (getState, payload, dispatch) => {
    const { mangaId: id, monitored } = payload;
    const manga = _.find(getState().manga.items, { id });

    dispatch(updateItem({
      id,
      section,
      isSaving: true
    }));

    const promise = createAjaxRequest({
      url: `/manga/${id}`,
      method: 'PUT',
      data: JSON.stringify({
        ...manga,
        monitored
      }),
      dataType: 'json'
    }).request;

    promise.done(() => {
      dispatch(updateItem({
        id,
        section,
        isSaving: false,
        monitored
      }));
    });

    promise.fail(() => {
      dispatch(updateItem({
        id,
        section,
        isSaving: false
      }));
    });
  }
});

//
// Reducers

export const reducers = createHandleActions({

  [SET_MANGA_VALUE]: createSetSettingValueReducer(section)

}, defaultState, section);
