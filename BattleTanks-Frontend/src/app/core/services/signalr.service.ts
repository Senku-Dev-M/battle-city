import { Injectable } from '@angular/core';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { env } from '../utils/env';
import { Subject } from 'rxjs';
import {
  ChatMessageDto,
  PlayerPositionDto,
  PlayerStateDto,
  BulletStateDto,
  BulletHitReportDto,
  PlayerHitDto,
  PowerUpDto,
} from '../models/game.models';

@Injectable({ providedIn: 'root' })
export class SignalRService {
  private hub: HubConnection | null = null;

  readonly playerJoined$ = new Subject<{ userId: string; username: string }>();
  readonly playerLeft$ = new Subject<string>();
  readonly chatMessage$ = new Subject<ChatMessageDto>();
  readonly playerMoved$ = new Subject<PlayerStateDto | { playerId: string; x: number; y: number; rotation: number }>();
  readonly bulletSpawned$ = new Subject<BulletStateDto>();
  readonly bulletDespawned$ = new Subject<{ bulletId: string; reason: string }>();
  readonly playerHit$ = new Subject<PlayerHitDto>();
  readonly playerDied$ = new Subject<string>();
  readonly reconnected$ = new Subject<void>();
  readonly disconnected$ = new Subject<void>();
  readonly mapState$ = new Subject<any[]>();
  readonly cellDestroyed$ = new Subject<any>();
  readonly powerUpSpawned$ = new Subject<PowerUpDto>();
  readonly powerUpRemoved$ = new Subject<string>();
  readonly powerUpState$ = new Subject<PowerUpDto[]>();

  get isConnected() {
    return !!this.hub && this.hub.state === 'Connected';
  }

  async connect(): Promise<void> {
    if (this.hub) return;

    console.log('[SignalR] Creating connection to hub:', env.HUB_URL);

    this.hub = new HubConnectionBuilder()
      .withUrl(env.HUB_URL, { withCredentials: true })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Information)
      .build();

    // Event handlers
    this.hub.on('playerJoined', (payload) => {
      console.log('[SignalR] playerJoined:', payload);
      this.playerJoined$.next(payload);
    });

    this.hub.on('playerLeft', (userId: string) => {
      console.log('[SignalR] playerLeft:', userId);
      this.playerLeft$.next(userId);
    });

    this.hub.on('chatMessage', (msg: ChatMessageDto) => {
      console.log('[SignalR] chatMessage:', msg);
      this.chatMessage$.next(msg);
    });

    this.hub.on('playerMoved', (p: any) => {
      console.log('[SignalR] playerMoved:', p);
      this.playerMoved$.next(p);
    });

    this.hub.on('bulletSpawned', (b: BulletStateDto) => {
      console.log('[SignalR] bulletSpawned:', b);
      this.bulletSpawned$.next(b);
    });

    this.hub.on('bulletDespawned', (bulletId: string, reason: string) => {
      console.log('[SignalR] bulletDespawned:', { bulletId, reason });
      this.bulletDespawned$.next({ bulletId, reason });
    });

    this.hub.on('playerHit', (dto: PlayerHitDto) => {
      console.log('[SignalR] playerHit:', dto);
      this.playerHit$.next(dto);
    });

    this.hub.on('playerDied', (playerId: string) => {
      console.log('[SignalR] playerDied:', playerId);
      this.playerDied$.next(playerId);
    });

    this.hub.on('mapState', (map: any[]) => {
      console.log('[SignalR] mapState:', map);
      this.mapState$.next(map);
    });

    this.hub.on('cellDestroyed', (cell: any) => {
      console.log('[SignalR] cellDestroyed:', cell);
      this.cellDestroyed$.next(cell);
    });

    this.hub.on('powerUpSpawned', (p: PowerUpDto) => {
      console.log('[SignalR] powerUpSpawned:', p);
      this.powerUpSpawned$.next(p);
    });

    this.hub.on('powerUpRemoved', (id: string) => {
      console.log('[SignalR] powerUpRemoved:', id);
      this.powerUpRemoved$.next(id);
    });

    this.hub.on('powerUpState', (list: PowerUpDto[]) => {
      console.log('[SignalR] powerUpState:', list);
      this.powerUpState$.next(list);
    });

    this.hub.on('eventHistory', (history: any) => {
      console.log('[SignalR] eventHistory received:', history);
      try {
        if (Array.isArray(history)) {
          for (const entry of history) {
            const evt = typeof entry === 'string' ? JSON.parse(entry) : entry;
            const type = evt?.eventType;
            const data = evt?.data;
            console.log('[SignalR] processing history event:', type, data);
            switch (type) {
              case 'playerJoined':
                this.playerJoined$.next(data);
                break;
              case 'playerLeft':
                const uid = typeof data === 'string' ? data : data?.userId || data?.playerId;
                this.playerLeft$.next(uid);
                break;
              case 'chatMessage':
                this.chatMessage$.next(data as any);
                break;
              case 'playerMoved':
                this.playerMoved$.next(data);
                break;
              case 'bulletSpawned':
                this.bulletSpawned$.next(data as any);
                break;
              case 'bulletDespawned':
                this.bulletDespawned$.next(data as any);
                break;
              case 'playerHit':
                this.playerHit$.next(data as any);
                break;
              case 'playerDied':
                const id = typeof data === 'string' ? data : data?.playerId;
                this.playerDied$.next(id);
                break;
              case 'cellDestroyed':
                console.log('[SignalR] cellDestroyed ignored from history');
                break;
              default:
                console.warn('[SignalR] Unknown event type in history:', type);
                break;
            }
          }
        }
      } catch (err) {
        console.error('[SignalR] Failed to process event history', err);
      }
    });

    this.hub.onreconnected(() => {
      console.log('[SignalR] Reconnected');
      this.reconnected$.next();
    });

    this.hub.onclose(() => {
      console.warn('[SignalR] Connection closed');
      this.disconnected$.next();
    });

    console.log('[SignalR] Starting connection...');
    await this.hub.start();
    console.log('[SignalR] Connected!');
  }

  async disconnect(): Promise<void> {
    if (!this.hub) return;
    try {
      console.log('[SignalR] Disconnecting...');
      await this.hub.stop();
      console.log('[SignalR] Disconnected');
    } finally {
      this.hub = null;
    }
  }

  async joinRoom(roomCode: string, username: string, joinKey?: string | null): Promise<void> {
    if (!this.hub) throw new Error('Hub not connected');
    console.log('[SignalR] joinRoom invoked', { roomCode, username, joinKey });
    await this.hub.invoke('JoinRoom', roomCode, username, joinKey ?? null);
  }

  async sendChat(content: string): Promise<void> {
    if (!this.hub) throw new Error('Hub not connected');
    console.log('[SignalR] sendChat:', content);
    await this.hub.invoke('SendChat', content);
  }

  async updatePosition(dto: PlayerPositionDto): Promise<void> {
    if (!this.hub) throw new Error('Hub not connected');
    console.log('[SignalR] updatePosition:', dto);
    await this.hub.invoke('UpdatePosition', dto);
  }

  async spawnBullet(x: number, y: number, directionRadians: number, speed: number): Promise<void> {
    if (!this.hub) throw new Error('Hub not connected');
    console.log('[SignalR] spawnBullet:', { x, y, directionRadians, speed });
    await this.hub.invoke('SpawnBullet', x, y, directionRadians, speed);
  }

  async reportHit(dto: BulletHitReportDto): Promise<void> {
    if (!this.hub) throw new Error('Hub not connected');
    console.log('[SignalR] reportHit:', dto);
    await this.hub.invoke('ReportHit', dto);
  }

  async reportBulletCollision(bulletId: string): Promise<void> {
    if (!this.hub) throw new Error('Hub not connected');
    console.log('[SignalR] reportBulletCollision:', bulletId);
    await this.hub.invoke('ReportObstacleHit', bulletId);
  }

  async getMap(): Promise<void> {
    if (!this.hub) throw new Error('Hub not connected');
    console.log('[SignalR] getMap');
    await this.hub.invoke('GetMap');
  }

  async destroyCell(x: number, y: number): Promise<void> {
    if (!this.hub) throw new Error('Hub not connected');
    console.log('[SignalR] destroyCell:', { x, y });
    await this.hub.invoke('DestroyCell', x, y);
  }

  async getPowerUps(): Promise<void> {
    if (!this.hub) throw new Error('Hub not connected');
    console.log('[SignalR] getPowerUps');
    await this.hub.invoke('GetPowerUps');
  }

  async collectPowerUp(id: string): Promise<void> {
    if (!this.hub) throw new Error('Hub not connected');
    console.log('[SignalR] collectPowerUp:', id);
    await this.hub.invoke('CollectPowerUp', id);
  }
}
