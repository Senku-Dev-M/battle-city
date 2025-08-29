export interface PlayerStateDto {
  playerId: string;
  username: string;
  x: number;
  y: number;
  rotation: number;
  lives: number;
  isAlive: boolean;
  score: number;
}

export interface PlayerPositionDto {
  playerId: string;
  x: number;
  y: number;
  rotation: number;
  timestamp: number;
}

export type SystemOrUser = 'System' | 'User';

export interface ChatMessageDto {
  messageId: string;
  userId: string;
  username: string;
  content: string;
  type: SystemOrUser;
  sentAt: string;
}

export interface BulletStateDto {
  bulletId: string;
  roomId: string;
  shooterId: string;
  x: number;
  y: number;
  directionRadians: number;
  speed: number;
  spawnTimestamp: number;
  isActive: boolean;
}

export interface BulletHitReportDto {
  bulletId: string;
  targetPlayerId: string;
  hitX: number;
  hitY: number;
  timestamp: number;
}

export interface PlayerHitDto {
  bulletId: string;
  targetPlayerId: string;
  shooterId: string;
  damage: number;
  targetLivesAfter: number;
  targetIsAlive: boolean;
  shooterScoreAfter: number;
}
