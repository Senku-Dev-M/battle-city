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
}

const playersAdapter = createEntityAdapter<PlayerEntity>({
  selectId: (p) => p.playerId,
});
const bulletsAdapter = createEntityAdapter<BulletEntity>({
  selectId: (b) => b.bulletId,
});

const initialState: RoomState = {
  roomCode: null,
  joined: false,
  hubConnected: false,
  error: null,
  players: playersAdapter.getInitialState(),
  bullets: bulletsAdapter.getInitialState(),
  chat: [],
  lastUsername: null,
};

export const roomReducer = createReducer(
  initialState,

  // Hub connection state
  on(roomActions.hubConnect, (s) => ({ ...s, error: null })),
  on(roomActions.hubConnected, (s) => ({ ...s, hubConnected: true })),
  on(roomActions.hubDisconnected, (s) => ({ ...s, hubConnected: false, joined: false })),
  on(roomActions.hubError, (s, { error }) => ({ ...s, error })),

  // Room lifecycle
  on(roomActions.joinRoom, (s, { code, username }) => ({ ...s, roomCode: code, lastUsername: username, error: null })),
  on(roomActions.joined, (s) => ({ ...s, joined: true })),
  on(roomActions.leaveRoom, (s) => ({ ...s, joined: false })),
  on(roomActions.left, (s) => ({
    ...s,
    joined: false,
    roomCode: null,
    players: playersAdapter.removeAll(s.players),
    bullets: bulletsAdapter.removeAll(s.bullets),
    chat: [],
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
      };
    return { ...s, players: playersAdapter.upsertOne(upsert, s.players) };
  }),

  on(roomActions.playerLeft, (s, { userId }) => ({
    ...s,
    players: playersAdapter.removeOne(userId, s.players),
  })),

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
      };
      playersState = playersAdapter.upsertOne(updatedTarget, playersState);

      const shooter = s.players.entities[dto.shooterId];
      if (shooter) {
        const updatedShooter: PlayerStateDto = {
          ...shooter,
          score: dto.shooterScoreAfter,
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
  on(roomActions.rosterLoaded, (s, { players }) => ({
    ...s,
    players: playersAdapter.upsertMany(players, s.players),
  })),

  // Chat messages
  on(roomActions.messageReceived, (s, { msg }) => ({
    ...s,
    chat: [...s.chat, msg].slice(-200),
  })),
);

export const roomPlayersAdapter = playersAdapter;
export const roomBulletsAdapter = bulletsAdapter;
