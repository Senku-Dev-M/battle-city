import { PlayerStateDto } from './game.models';

export interface CreateRoomDto {
  name: string;
  maxPlayers: number;
  isPublic: boolean;
}

export interface RoomStateDto {
  roomId: string;
  roomCode: string;
  status: string;
  players: PlayerStateDto[];
}
