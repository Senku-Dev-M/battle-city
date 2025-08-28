import { createActionGroup, emptyProps, props } from '@ngrx/store';
import {
  ChatMessageDto,
  PlayerPositionDto,
  PlayerStateDto,
  BulletStateDto,
  PlayerHitDto,
} from '../../../core/models/game.models';

export const roomActions = createActionGroup({
  source: 'Room',
  events: {
    // Hub
    'Hub Connect': emptyProps(),
    'Hub Connected': emptyProps(),
    'Hub Disconnected': emptyProps(),
    'Hub Reconnected': emptyProps(),
    'Hub Error': props<{ error: string }>(),

    // Sala
    'Join Room': props<{ code: string; username: string }>(),
    'Joined': emptyProps(),
    'Leave Room': emptyProps(),
    'Left': emptyProps(),

    // Roster HTTP
    'Roster Loaded': props<{ players: PlayerStateDto[] }>(),

    // Server→cliente
    'Player Joined': props<{ userId: string; username: string }>(),
    'Player Left': props<{ userId: string }>(),
    'Player Moved': props<{ player: PlayerStateDto | { playerId: string; x: number; y: number; rotation: number } }>(),
    'Bullet Spawned': props<{ bullet: BulletStateDto }>(),
    'Bullet Despawned': props<{ bulletId: string }>(),
    'Player Hit': props<{ dto: PlayerHitDto }>(),
    'Player Died': props<{ playerId: string }>(),
    'Message Received': props<{ msg: ChatMessageDto }>(),

    // Cliente→servidor
    'Send Message': props<{ content: string }>(),
    'Update Position': props<{ dto: PlayerPositionDto }>(),
    'Spawn Bullet': props<{ x: number; y: number; dir: number; speed: number }>(),
  },
});
