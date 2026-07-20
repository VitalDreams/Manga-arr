import _ from 'lodash';
import { createAction } from 'redux-actions';
import { batchActions } from 'redux-batched-actions';
import { filterTypePredicates, filterTypes, sortDirections } from 'Helpers/Props';
import { createThunk, handleThunks } from 'Store/thunks';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import dateFilterPredicate from 'Utilities/Date/dateFilterPredicate';
import { removeItem, set, update, updateItem } from './baseActions';
import createHandleActions from './Creators/createHandleActions';
import createSetClientSideCollectionSortReducer from './Creators/Reducers/createSetClientSideCollectionSortReducer';
import createSetSettingValueReducer from './Creators/Reducers/createSetSettingValueReducer';
import createSetTableOptionReducer from './Creators/Reducers/createSetTableOptionReducer';

//
// Variables

export const section = 'volumes';

//
// State

export const defaultState = {
  isFetching: false,
  isPopulated: false,
  error: null,
  isSaving: false,
  saveError: null,
  sortKey: 'releaseDate',
  sortDirection: sortDirections.DESCENDING,
  items: [],
  pendingChanges: {},

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
      name: 'monitored',
      columnLabel: 'Monitored',
      isVisible: true,
      isModifiable: false
    },
    {
      name: 'title',
      label: 'Title',
      isSortable: true,
      isVisible: true
    },
    {
      name: 'releaseDate',
      label: 'Release Date',
      isSortable: true,
      isVisible: true
    },
    {
      name: 'pageCount',
      label: 'Pages',
      isSortable: true,
      isVisible: true
    },
    {
      name: 'status',
      label: 'Status',
      isVisible: true,
      isSortable: true
    },
    {
      name: 'actions',
      columnLabel: 'Actions',
      isVisible: true,
      isModifiable: false
    }
  ]
};

export const persistState = [
  'volumes.sortKey',
  'volumes.sortDirection',
  'volumes.columns'
];

//
// Actions Types

export const FETCH_VOLUMES = 'volumes/fetchVolumes';
export const SET_VOLUMES_SORT = 'volumes/setVolumesSort';
export const SET_VOLUMES_TABLE_OPTION = 'volumes/setVolumesTableOption';
export const CLEAR_VOLUMES = 'volumes/clearVolumes';
export const SET_VOLUME_VALUE = 'volumes/setVolumeValue';
export const SAVE_VOLUME = 'volumes/saveVolume';
export const TOGGLE_VOLUME_MONITORED = 'volumes/toggleVolumeMonitored';
export const TOGGLE_VOLUMES_MONITORED = 'volumes/toggleVolumesMonitored';

//
// Action Creators

export const fetchVolumes = createThunk(FETCH_VOLUMES);
export const setVolumesSort = createAction(SET_VOLUMES_SORT);
export const setVolumesTableOption = createAction(SET_VOLUMES_TABLE_OPTION);
export const clearVolumes = createAction(CLEAR_VOLUMES);
export const toggleVolumeMonitored = createThunk(TOGGLE_VOLUME_MONITORED);
export const toggleVolumesMonitored = createThunk(TOGGLE_VOLUMES_MONITORED);
export const saveVolume = createThunk(SAVE_VOLUME);

export const setVolumeValue = createAction(SET_VOLUME_VALUE, (payload) => {
  return {
    section: 'volumes',
    ...payload
  };
});

//
// Action Handlers

export const actionHandlers = handleThunks({
  [FETCH_VOLUMES]: function(getState, payload, dispatch) {
    dispatch(set({ section, isFetching: true }));

    const { mangaId, ...otherPayload } = payload;
    const url = `/manga/${mangaId}/books`;

    const { request, abortRequest } = createAjaxRequest({
      url,
      data: otherPayload,
      traditional: true
    });

    request.done((data) => {
      // Preserve volumes for other manga we didn't fetch
      if (mangaId) {
        const oldVolumes = getState().volumes.items;
        const newVolumes = oldVolumes.filter((x) => x.authorId !== mangaId);
        data = newVolumes.concat(data);
      }

      dispatch(batchActions([
        update({ section, data }),
        set({
          section,
          isFetching: false,
          isPopulated: true,
          error: null
        })
      ]));
    });

    request.fail((xhr) => {
      dispatch(set({
        section,
        isFetching: false,
        isPopulated: false,
        error: xhr.aborted ? null : xhr
      }));
    });

    return abortRequest;
  },

  [SAVE_VOLUME]: function(getState, payload, dispatch) {
    dispatch(set({ section, isSaving: true }));

    const { id, ...otherPayload } = payload;

    const promise = createAjaxRequest({
      url: `/book/${id}`,
      method: 'PUT',
      contentType: 'application/json',
      dataType: 'json',
      data: JSON.stringify(otherPayload)
    }).request;

    promise.done((data) => {
      dispatch(batchActions([
        updateItem({ section, ...data }),
        set({
          section,
          isSaving: false,
          saveError: null
        })
      ]));
    });

    promise.fail((xhr) => {
      dispatch(set({
        section,
        isSaving: false,
        saveError: xhr
      }));
    });
  },

  [TOGGLE_VOLUME_MONITORED]: function(getState, payload, dispatch) {
    const { volumeId, monitored } = payload;

    dispatch(updateItem({
      id: volumeId,
      section,
      isSaving: true
    }));

    const promise = createAjaxRequest({
      url: `/book/${volumeId}`,
      method: 'PUT',
      data: JSON.stringify({ monitored }),
      dataType: 'json'
    }).request;

    promise.done(() => {
      dispatch(updateItem({
        id: volumeId,
        section,
        isSaving: false,
        monitored
      }));
    });

    promise.fail(() => {
      dispatch(updateItem({
        id: volumeId,
        section,
        isSaving: false
      }));
    });
  },

  [TOGGLE_VOLUMES_MONITORED]: function(getState, payload, dispatch) {
    const { volumeIds, monitored } = payload;

    dispatch(batchActions(
      volumeIds.map((volumeId) => {
        return updateItem({
          id: volumeId,
          section,
          isSaving: true
        });
      })
    ));

    const promise = createAjaxRequest({
      url: '/book/monitor',
      method: 'PUT',
      data: JSON.stringify({ bookIds: volumeIds, monitored }),
      dataType: 'json'
    }).request;

    promise.done(() => {
      dispatch(batchActions(
        volumeIds.map((volumeId) => {
          return updateItem({
            id: volumeId,
            section,
            isSaving: false,
            monitored
          });
        })
      ));
    });

    promise.fail(() => {
      dispatch(batchActions(
        volumeIds.map((volumeId) => {
          return updateItem({
            id: volumeId,
            section,
            isSaving: false
          });
        })
      ));
    });
  }
});

//
// Reducers

export const reducers = createHandleActions({

  [SET_VOLUMES_SORT]: createSetClientSideCollectionSortReducer(section),

  [SET_VOLUMES_TABLE_OPTION]: createSetTableOptionReducer(section),

  [SET_VOLUME_VALUE]: createSetSettingValueReducer(section),

  [CLEAR_VOLUMES]: (state) => {
    return Object.assign({}, state, {
      isFetching: false,
      isPopulated: false,
      error: null,
      items: []
    });
  }

}, defaultState, section);
