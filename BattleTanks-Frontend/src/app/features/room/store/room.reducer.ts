import { createReducer, on } from '@ngrx/store';
import { createEntityAdapter, EntityState } from '@ngrx/entity';
import { roomActions } from './room.actions';
import { BulletStateDto, ChatMessageDto, PlayerStateDto } from '../../../core/models/game.models';

export interface PlayerEntity extends PlayerStateDto {}
export interface BulletEntity extends BulletStateDto {}

export interface RoomState {
  roomCode: string | null;
  joined: boolean;
  hubConnected: boolean;
  error: string | null;
  players: EntityState<PlayerEntity>;
  bullets: EntityState<BulletEntity>;
  chat: ChatMessageDto[];
  lastUsername: string | null;
  gameStarted: boolean;
  gameFinished: boolean;
  winnerId: string | null;
  didWin: boolean | null;
}

const playersAdapter = createEntityAdapter<PlayerEntity>({
  selectId: (p) => p.playerId,
});
const bulletsAdapter = createEntityAdapter<BulletEntity>({
  selectId: (b) => b.bulletId,
});

const PLAYER_COLOURS = [
  '#f87171', // red-400
  '#34d399', // green-400
  '#60a5fa', // blue-400
  '#fbbf24', // yellow-400
  '#d946ef', // fuchsia-500
  '#fb923c', // orange-400
  '#2dd4bf', // teal-400
  '#a78bfa', // purple-400
];

function recolor(state: EntityState<PlayerEntity>): EntityState<PlayerEntity> {
  const list = Object.values(state.entities).filter((p): p is PlayerEntity => !!p);
  const sorted = [...list].sort((a, b) => a.playerId.localeCompare(b.playerId));
  const coloured = sorted.map((p, i) => ({ ...p, color: PLAYER_COLOURS[i % PLAYER_COLOURS.length] }));
  return playersAdapter.setAll(coloured, state);
}

const initialState: RoomState = {
  roomCode: null,
  joined: false,
  hubConnected: false,
  error: null,
  players: playersAdapter.getInitialState(),
  bullets: bulletsAdapter.getInitialState(),
  chat: [],
  lastUsername: null,
  gameStarted: false,
  gameFinished: false,
  winnerId: null,
  didWin: null,
};

export const roomReducer = createReducer(
  initialState,

  // Hub connection state
  on(roomActions.hubConnect, (s) => ({ ...s, error: null })),
  on(roomActions.hubConnected, (s) => ({ ...s, hubConnected: true })),
  on(roomActions.hubDisconnected, (s) => ({ ...s, hubConnected: false, joined: false })),
  on(roomActions.hubError, (s, { error }) => ({ ...s, error })),

  // Room lifecycle
  on(roomActions.joinRoom, (s, { code, username }) => ({ ...s, roomCode: code, lastUsername: username, error: null, gameFinished: false, winnerId: null, didWin: null })),
  on(roomActions.joined, (s) => ({ ...s, joined: true, gameStarted: false, gameFinished: false, winnerId: null, didWin: null })),
  on(roomActions.leaveRoom, (s) => ({ ...s, joined: false })),
  on(roomActions.left, (s) => ({
    ...s,
    joined: false,
    roomCode: null,
    players: playersAdapter.removeAll(s.players),
    bullets: bulletsAdapter.removeAll(s.bullets),
    chat: [],
    gameStarted: false,
    gameFinished: false,
    winnerId: null,
    didWin: null,
  })),

  // Player events
  on(roomActions.playerJoined, (s, { userId, username }) => {
    const existing = s.players.entities[userId];
      const upsert: PlayerStateDto = {
        playerId: userId,
        username: username ?? existing?.username ?? 'Unknown',
        x: existing?.x ?? 0,
        y: existing?.y ?? 0,
        rotation: existing?.rotation ?? 0,
        lives: existing?.lives ?? 3,
        isAlive: existing?.isAlive ?? true,
        score: existing?.score ?? 0,
        hasShield: existing?.hasShield ?? false,
        speed: existing?.speed ?? 200,
        isReady: existing?.isReady ?? false,
        color: existing?.color,
      };
    let playersState = playersAdapter.upsertOne(upsert, s.players);
    playersState = recolor(playersState);
    return { ...s, players: playersState };
  }),

  on(roomActions.playerLeft, (s, { userId }) => {
    let playersState = playersAdapter.removeOne(userId, s.players);
    playersState = recolor(playersState);
    return { ...s, players: playersState };
  }),

  on(roomActions.playerMoved, (s, { player }) => {
    const incomingId = (player as any).playerId;
    const existing = s.players.entities[incomingId];

      const merged: PlayerStateDto = {
        playerId: incomingId,
        username: (player as any).username ?? existing?.username ?? 'Unknown',
        x: (player as any).x,
        y: (player as any).y,
        rotation: (player as any).rotation,
        lives: (player as any).lives ?? existing?.lives ?? 3,
        isAlive: (player as any).isAlive ?? existing?.isAlive ?? true,
        score: (player as any).score ?? existing?.score ?? 0,
        hasShield: (player as any).hasShield ?? existing?.hasShield ?? false,
        speed: (player as any).speed ?? existing?.speed ?? 200,
        isReady: existing?.isReady ?? false,
        color: existing?.color,
      };
    return { ...s, players: playersAdapter.upsertOne(merged, s.players) };
  }),

  on(roomActions.playerHit, (s, { dto }) => {
    const playerId = dto.targetPlayerId;
    const existing = s.players.entities[playerId];
    if (!existing) return s;
      let playersState = s.players;
      const updatedTarget: PlayerStateDto = {
        ...existing,
        lives: dto.targetLivesAfter,
        isAlive: dto.targetIsAlive,
        score: existing.score,
        hasShield: existing.hasShield,
        speed: existing.speed,
        isReady: existing.isReady,
        color: existing.color,
      };
      playersState = playersAdapter.upsertOne(updatedTarget, playersState);

      const shooter = s.players.entities[dto.shooterId];
      if (shooter) {
        const updatedShooter: PlayerStateDto = {
          ...shooter,
          score: dto.shooterScoreAfter,
          isReady: shooter.isReady,
          color: shooter.color,
        };
        playersState = playersAdapter.upsertOne(updatedShooter, playersState);
      }
      return { ...s, players: playersState };
  }),

  on(roomActions.playerDied, (s, { playerId }) => {
    const existing = s.players.entities[playerId];
    if (!existing) return s;
      const updated: PlayerStateDto = {
        ...existing,
        lives: 0,
        isAlive: false,
        hasShield: existing.hasShield,
        speed: existing.speed,
        isReady: existing.isReady,
        color: existing.color,
      };
    return { ...s, players: playersAdapter.upsertOne(updated, s.players) };
  }),

  // Bullet events
  on(roomActions.bulletSpawned, (s, { bullet }) => ({
    ...s,
    bullets: bulletsAdapter.upsertOne(bullet, s.bullets),
  })),
  on(roomActions.bulletDespawned, (s, { bulletId }) => ({
    ...s,
    bullets: bulletsAdapter.removeOne(bulletId, s.bullets),
  })),

  // Roster load
  on(roomActions.rosterLoaded, (s, { players }) => {
    let playersState = playersAdapter.upsertMany(players, s.players);
    playersState = recolor(playersState);
    return { ...s, players: playersState };
  }),

  on(roomActions.playerReady, (s, { userId, ready }) => {
    const existing = s.players.entities[userId];
    if (!existing) return s;
    const updated: PlayerStateDto = { ...existing, isReady: ready, color: existing.color };
    return { ...s, players: playersAdapter.upsertOne(updated, s.players) };
  }),

  on(roomActions.gameStarted, (s) => ({ ...s, gameStarted: true })),
  on(roomActions.gameFinished, (s, { winnerId }) => ({ ...s, gameFinished: true, winnerId })),
  on(roomActions.matchResult, (s, { didWin }) => ({ ...s, didWin })),

  // Chat messages
  on(roomActions.messageReceived, (s, { msg }) => ({
    ...s,
    chat: [...s.chat, msg].slice(-200),
  })),
);

export const roomPlayersAdapter = playersAdapter;
export const roomBulletsAdapter = bulletsAdapter;
