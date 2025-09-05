import { Injectable, inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { roomActions } from './room.actions';
import { SignalRService } from '../../../core/services/signalr.service';
import { MqttService } from '../../../core/services/mqtt.service';
import { catchError, filter, from, map, merge, mergeMap, of, switchMap, takeUntil, tap, throttleTime, withLatestFrom } from 'rxjs';
import { Store } from '@ngrx/store';
import { selectRoomCode, selectGameFinished, selectMyId } from './room.selectors';
import { selectUser } from '../../auth/store/auth.selectors';
import { RoomService } from '../../../core/services/room.service'; 
import { RoomStateDto } from '../../../core/models/room.models';

@Injectable()
export class RoomEffects {
  private actions$ = inject(Actions);
  private hub = inject(SignalRService);
  private store = inject(Store);
  private roomsHttp = inject(RoomService); 
  private mqtt = inject(MqttService);

  hubConnect$ = createEffect(() =>
    this.actions$.pipe(
      ofType(roomActions.hubConnect),
      switchMap(() =>
        from(this.hub.connect()).pipe(
          map(() => roomActions.hubConnected()),
          catchError((err) => of(roomActions.hubError({ error: err?.message ?? 'No se pudo conectar al hub' })))
        )
      )
    )
  );

  // Suscripción a eventos del hub cuando está conectado
events$ = createEffect(() =>
  this.actions$.pipe(
    ofType(roomActions.hubConnected),
    switchMap(() => {
      const stop$ = this.actions$.pipe(ofType(roomActions.hubDisconnected, roomActions.left));
      return merge(
        // SignalR events
        this.hub.identity$.pipe(map((p) => roomActions.identityReceived({ userId: p.userId }))),
        this.hub.playerJoined$.pipe(map((p) => roomActions.playerJoined(p))),
        this.hub.playerLeft$.pipe(map((userId) => roomActions.playerLeft({ userId }))),
        this.hub.chatMessage$.pipe(map((msg) => roomActions.messageReceived({ msg }))),
        this.hub.playerMoved$.pipe(map((player: any) => roomActions.playerMoved({ player }))),
        this.hub.bulletSpawned$.pipe(map((bullet) => roomActions.bulletSpawned({ bullet }))),
        this.hub.bulletDespawned$.pipe(map(({ bulletId }) => roomActions.bulletDespawned({ bulletId }))),
        this.hub.playerHit$.pipe(map((dto) => roomActions.playerHit({ dto }))),
        this.hub.playerDied$.pipe(map((playerId) => roomActions.playerDied({ playerId }))),
        this.hub.playerReady$.pipe(map((p) => roomActions.playerReady(p))),
        this.hub.gameStarted$.pipe(map(() => roomActions.gameStarted())),
        this.hub.gameFinished$.pipe(
          withLatestFrom(this.store.select(selectMyId)),
          mergeMap(([winnerId, myId]) => {
            const didWin = !!winnerId && winnerId === myId;
            return [
              roomActions.gameFinished({ winnerId }),
              roomActions.matchResult({ didWin }),
            ];
          })
        ),
        this.hub.matchResult$.pipe(
          withLatestFrom(this.store.select(selectMyId)),
          filter(([evt, myId]) => evt.playerId === myId),
          map(([evt]) => roomActions.matchResult({ didWin: evt.didWin }))
        ),
        // MQTT events
        this.mqtt.playerJoined$.pipe(map((p) => roomActions.playerJoined(p))),
        this.mqtt.playerLeft$.pipe(map((userId) => roomActions.playerLeft({ userId }))),
        this.mqtt.chatMessage$.pipe(map((msg) => roomActions.messageReceived({ msg }))),
        this.mqtt.playerMoved$.pipe(map((player: any) => roomActions.playerMoved({ player }))),
        this.mqtt.bulletSpawned$.pipe(map((bullet) => roomActions.bulletSpawned({ bullet }))),
        this.mqtt.bulletDespawned$.pipe(map(({ bulletId }) => roomActions.bulletDespawned({ bulletId }))),
        this.mqtt.playerHit$.pipe(map((dto) => roomActions.playerHit({ dto }))),
        this.mqtt.playerDied$.pipe(map((playerId) => roomActions.playerDied({ playerId }))),
        this.mqtt.gameFinished$.pipe(
          withLatestFrom(this.store.select(selectMyId)),
          mergeMap(([winnerId, myId]) => {
            const didWin = !!winnerId && winnerId === myId;
            return [
              roomActions.gameFinished({ winnerId }),
              roomActions.matchResult({ didWin }),
            ];
          })
        )
      ).pipe(takeUntil(stop$));
    })
  )
);

  // Join room (espera tener conexión)
  joinRoom$ = createEffect(() =>
    this.actions$.pipe(
      ofType(roomActions.joinRoom),
      switchMap(({ code, username }) =>
        from(this.hub.joinRoom(code, username)).pipe(
          map(() => roomActions.joined()),
          catchError((err) => of(roomActions.hubError({ error: err?.message ?? 'join_failed' })))
        )
      )
    )
  );

  /**
   * Establishes the MQTT connection when a join request is dispatched. This effect does not
   * dispatch any actions. If the connection fails it is silently ignored so that the game
   * continues to function using SignalR. When the player leaves the room the connection will
   * be cleaned up by the mqttDisconnect$ effect.
   */
  mqttConnect$ = createEffect(
    () =>
      this.actions$.pipe(
        ofType(roomActions.joinRoom),
        mergeMap(({ code }) => from(this.mqtt.connect(code)).pipe(catchError(() => of(void 0))))
      ),
    { dispatch: false }
  );

  // Rejoin tras reconectar
  rejoin$ = createEffect(() =>
    this.hub.reconnected$.pipe(
      switchMap(() => of(roomActions.hubReconnected()))
    )
  );

  rejoinOnReconnected$ = createEffect(() =>
    this.actions$.pipe(
      ofType(roomActions.hubReconnected),
      withLatestFrom(
        this.store.select(selectRoomCode),
        this.store.select(selectUser),
        this.store.select(selectGameFinished)
      ),
      filter(([_, code, user, finished]) => !!code && !!user?.username && !finished),
      switchMap(([_, code, user]) =>
        from(this.hub.joinRoom(code as string, user!.username)).pipe(
          map(() => roomActions.joined()),
          catchError((err) => of(roomActions.hubError({ error: err?.message ?? 'rejoin_failed' })))
        )
      )
    )
  );

  // Send chat
  sendMessage$ = createEffect(
    () =>
      this.actions$.pipe(
        ofType(roomActions.sendMessage),
        mergeMap(({ content }) =>
          from(this.hub.sendChat(content)).pipe(
            catchError(() => of(void 0)) // silenciamos error en UI por ahora
          )
        )
      ),
    { dispatch: false }
  );

  // Update position (throttle 50ms)
  updatePosition$ = createEffect(
    () =>
      this.actions$.pipe(
        ofType(roomActions.updatePosition),
        throttleTime(50, undefined, { leading: true, trailing: true }),
        mergeMap(({ dto }) => from(this.hub.updatePosition(dto)).pipe(catchError(() => of(void 0))))
      ),
    { dispatch: false }
  );

  // Spawn bullet
  spawnBullet$ = createEffect(
    () =>
      this.actions$.pipe(
        ofType(roomActions.spawnBullet),
        mergeMap(({ x, y, dir, speed }) => from(this.hub.spawnBullet(x, y, dir, speed)).pipe(catchError(() => of(void 0))))
      ),
    { dispatch: false }
  );

  setReady$ = createEffect(
    () =>
      this.actions$.pipe(
        ofType(roomActions.setReady),
        mergeMap(({ ready }) => from(this.hub.setReady(ready)).pipe(catchError(() => of(void 0))))
      ),
    { dispatch: false }
  );

  // Leave / Disconnect
  leave$ = createEffect(() =>
    this.actions$.pipe(
      ofType(roomActions.leaveRoom),
      withLatestFrom(this.store.select(selectRoomCode)),
      switchMap(([_, code]) =>
        (code ? from(this.hub.leaveRoom(code)) : of(void 0)).pipe(
          map(() => roomActions.left()),
          catchError(() => of(roomActions.left()))
        )
      )
    )
  );

  /**
   * Disconnects the MQTT client when leaving the room. Runs in parallel with the SignalR disconnect.
   */
  mqttDisconnect$ = createEffect(
    () =>
      this.actions$.pipe(
        ofType(roomActions.leaveRoom),
        tap(() => this.mqtt.disconnect())
      ),
    { dispatch: false }
  );

    rosterAfterJoin$ = createEffect(() =>
    this.actions$.pipe(
      ofType(roomActions.joined),
      withLatestFrom(this.store.select(selectRoomCode)),
      filter(([_, code]) => !!code),
      switchMap(([_, code]) =>
        this.roomsHttp.getRooms().pipe(
          map((res: any) => {
            // normalizar paginado { items: [...] }
            const list: RoomStateDto[] = Array.isArray(res?.items) ? res.items : Array.isArray(res) ? res : [];
            const match = list.find(r => r.roomCode === code);
            return match?.roomId ?? null;
          }),
          switchMap((roomId) => roomId
            ? this.roomsHttp.getRoom(roomId)
            : of(null)
          ),
          map((room) => roomActions.rosterLoaded({ players: room?.players ?? [] })),
          catchError(() => of(roomActions.rosterLoaded({ players: [] })))
        )
      )
    )
  );
}
