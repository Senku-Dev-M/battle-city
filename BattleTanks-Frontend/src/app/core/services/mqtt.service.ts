import { Injectable } from '@angular/core';
import mqtt, { MqttClient, IClientOptions } from 'mqtt';
import { Subject } from 'rxjs';
import { env } from '../utils/env';
import {
  ChatMessageDto,
  PlayerPositionDto,
  PlayerStateDto,
  BulletStateDto,
  PlayerHitDto,
} from '../models/game.models';

@Injectable({ providedIn: 'root' })
export class MqttService {
  private client: MqttClient | null = null;

  readonly playerJoined$ = new Subject<{ userId: string; username: string }>();
  readonly playerLeft$ = new Subject<string>();
  readonly chatMessage$ = new Subject<ChatMessageDto>();
  readonly playerMoved$ = new Subject<
    PlayerStateDto | { playerId: string; x: number; y: number; rotation: number }
  >();
  readonly bulletSpawned$ = new Subject<BulletStateDto>();
  readonly bulletDespawned$ = new Subject<{ bulletId: string; reason: string }>();
  readonly playerHit$ = new Subject<PlayerHitDto>();
  readonly playerDied$ = new Subject<string>();

  connect(roomCode: string): Promise<void> {
    return new Promise((resolve, reject) => {
      if (this.client) {
        console.warn('[MQTT] Existing client found, closing it...');
        this.client.end(true);
        this.client = null;
      }

      const url = env.MQTT_URL;
      console.log(`[MQTT] Connecting to broker: ${url} for room ${roomCode}`);

      try {
        const opts: IClientOptions = {
          clientId: 'client-' + Math.random().toString(16).substring(2, 10),
          clean: true,
        };
        this.client = mqtt.connect(url, opts);
      } catch (err: any) {
        console.error('[MQTT] Connection error', err);
        reject(err);
        return;
      }

      const topic = `game/${roomCode}/events/#`;
      const client = this.client!;

      client.on('connect', async () => {
        console.log('[MQTT] Connected to broker');
        try {
          const granted = await client.subscribe(topic, { qos: 0 });
          console.log('[MQTT] Subscribed to topic:', topic, granted);
          resolve();
        } catch (err) {
          console.error('[MQTT] Subscribe error', err);
          reject(err);
        }
      });

      client.on('message', (receivedTopic: string, payload: Uint8Array) => {
        try {
          const json = new TextDecoder().decode(payload);
          const data = JSON.parse(json);
          const segments = receivedTopic.split('/');
          const eventType = segments[segments.length - 1];

          console.log(`[MQTT] Message received - Topic: ${receivedTopic}, Event: ${eventType}, Data:`, data);

          switch (eventType) {
            case 'playerJoined':
              this.playerJoined$.next(data);
              break;
            case 'playerLeft':
              const uid = typeof data === 'string' ? data : data.userId || data.playerId;
              this.playerLeft$.next(uid);
              break;
            case 'chatMessage':
              this.chatMessage$.next(data as ChatMessageDto);
              break;
            case 'playerMoved':
              this.playerMoved$.next(data as any);
              break;
            case 'bulletSpawned':
              this.bulletSpawned$.next(data as BulletStateDto);
              break;
            case 'bulletDespawned':
              this.bulletDespawned$.next(data as { bulletId: string; reason: string });
              break;
            case 'playerHit':
              this.playerHit$.next(data as PlayerHitDto);
              break;
            case 'playerDied':
              const id = typeof data === 'string' ? data : data.playerId;
              this.playerDied$.next(id);
              break;
            default:
              console.warn('[MQTT] Unknown event type received:', eventType, data);
              break;
          }
        } catch (err) {
          console.error('[MQTT] Failed to process message', err);
        }
      });

      client.on('error', (err) => {
        console.error('[MQTT] Client error', err);
      });

      client.on('close', () => {
        console.warn('[MQTT] Connection closed');
      });

      client.on('reconnect', () => {
        console.log('[MQTT] Attempting to reconnect...');
      });

      client.on('offline', () => {
        console.warn('[MQTT] Client went offline');
      });
    });
  }

  disconnect(): void {
    if (this.client) {
      console.log('[MQTT] Disconnecting client...');
      this.client.end(true);
      this.client = null;
      console.log('[MQTT] Disconnected');
    }
  }
}
