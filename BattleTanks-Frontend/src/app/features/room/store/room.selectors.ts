import { createFeatureSelector, createSelector } from '@ngrx/store';
import { roomBulletsAdapter, roomPlayersAdapter, RoomState } from './room.reducer';

export const selectRoomState = createFeatureSelector<RoomState>('room');

export const selectRoomCode = createSelector(selectRoomState, (s) => s.roomCode);
export const selectHubConnected = createSelector(selectRoomState, (s) => s.hubConnected);
export const selectJoined = createSelector(selectRoomState, (s) => s.joined);
export const selectRoomError = createSelector(selectRoomState, (s) => s.error);
export const selectChat = createSelector(selectRoomState, (s) => s.chat);

const playersSelectors = roomPlayersAdapter.getSelectors();
const bulletsSelectors = roomBulletsAdapter.getSelectors();

export const selectPlayers = createSelector(selectRoomState, (s) => playersSelectors.selectAll(s.players));
export const selectBullets = createSelector(selectRoomState, (s) => bulletsSelectors.selectAll(s.bullets));
