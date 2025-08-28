import { Injectable, inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { roomsActions } from './rooms.actions';
import { RoomService } from '../../../core/services/room.service';
import { catchError, map, of, switchMap, tap } from 'rxjs';
import { RoomStateDto } from '../../../core/models/room.models';

@Injectable()
export class RoomsEffects {
  private actions$ = inject(Actions);
  private rooms = inject(RoomService);

  // Normalize server payload to RoomStateDto[]
  private normalize(payload: any): RoomStateDto[] {
    if (Array.isArray(payload)) return payload;
    if (Array.isArray(payload?.items)) return payload.items;
    if (Array.isArray(payload?.rooms)) return payload.rooms;
    if (Array.isArray(payload?.data))  return payload.data;
    return [];
  }

  // Load rooms effect
  loadRooms$ = createEffect(() =>
    this.actions$.pipe(
      ofType(roomsActions.loadRooms),
      switchMap(() =>
        this.rooms.getRooms().pipe(
          tap((res) => console.log('[Rooms] GET /rooms response:', res)),
          map((res) => roomsActions.loadRoomsSuccess({ rooms: this.normalize(res) })),
          catchError((err) =>
            of(roomsActions.loadRoomsFailure({ error: err?.error?.message ?? 'No se pudo cargar salas' }))
          )
        )
      )
    )
  );

  // Create room effect
  createRoom$ = createEffect(() =>
    this.actions$.pipe(
      ofType(roomsActions.createRoom),
      switchMap(({ dto }) =>
        this.rooms.createRoom(dto).pipe(
          tap((res) => console.log('[Rooms] POST /rooms response:', res)),
          map((room) => roomsActions.createRoomSuccess({ room })),
          catchError((err) =>
            of(roomsActions.createRoomFailure({ error: err?.error?.message ?? 'No se pudo crear la sala' }))
          )
        )
      )
    )
  );
}
