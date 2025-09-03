import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, effect, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { toSignal } from '@angular/core/rxjs-interop';

import { roomActions } from './store/room.actions';
import {
  selectHubConnected,
  selectJoined,
  selectRoomError,
  selectPlayers,
  selectGameStarted,
  selectGameFinished,
  selectWinnerId,
} from './store/room.selectors';
import { selectUser } from './../auth/store/auth.selectors';
import { RoomCanvasComponent } from './room-canvas/room-canvas.component';
import { ChatPanelComponent } from './chat-panel/chat-panel.component';

@Component({
  standalone: true,
  selector: 'app-room',
  imports: [CommonModule, RouterLink, RoomCanvasComponent, ChatPanelComponent],
  templateUrl: './room.component.html',
  styleUrls: ['./room.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RoomComponent implements OnInit, OnDestroy {
  private route = inject(ActivatedRoute);
  private store = inject(Store);

  hubConnected = toSignal(this.store.select(selectHubConnected), { initialValue: false });
  joined       = toSignal(this.store.select(selectJoined),       { initialValue: false });
  error        = toSignal(this.store.select(selectRoomError),    { initialValue: null });
  user         = toSignal(this.store.select(selectUser),         { initialValue: null });
  gameStarted  = toSignal(this.store.select(selectGameStarted),  { initialValue: false });
  gameFinished = toSignal(this.store.select(selectGameFinished), { initialValue: false });
  winnerId     = toSignal(this.store.select(selectWinnerId),     { initialValue: null });

  /**
   * Signal containing the list of players in the current room.  Used to derive the current player's
   * state (lives and alive status).
   */
  players      = toSignal(this.store.select(selectPlayers), { initialValue: [] });

  /**
   * Derives the current player's state by matching either the user id or username (case‑insensitive)
   * against the list of players.  Returns null if the player has not yet joined the room.
   */
  myPlayer = computed(() => {
    const me = this.user();
    const roster = this.players();
    if (!me) return null;
    const myId   = me.id;
    const myName = me.username?.toLowerCase() ?? '';
    return roster.find(p => p.playerId === myId || (p.username?.toLowerCase() ?? '') === myName) ?? null;
  });

  /**
   * Returns the number of remaining lives for the current player or null if not in a room.
   */
    myLives = computed(() => {
      const p = this.myPlayer();
      return p ? p.lives : null;
    });

    myScore = computed(() => {
      const p = this.myPlayer();
      return p ? p.score : 0;
    });

    myReady = computed(() => {
      const p = this.myPlayer();
      return p ? p.isReady : false;
    });

  /**
   * Indicates whether the current player is still alive.  Defaults to true (so that UI does not show
   * an overlay before joining).
   */
    myAlive = computed(() => {
      const p = this.myPlayer();
      return p ? p.isAlive : true;
    });

  private roomCode = signal<string | null>(null);

  // 👇 Efecto creado como *campo de clase* (válido en contexto de inyección)
  private joinWhenReady = effect(() => {
    const connected = this.hubConnected();
    const alreadyJoined = this.joined();
    const code = this.roomCode();
    const username = this.user()?.username ?? 'Player';

    if (connected && !alreadyJoined && code) {
      this.store.dispatch(roomActions.joinRoom({ code, username }));
    }
  });

  ngOnInit(): void {
    const code = this.route.snapshot.paramMap.get('code');
    this.roomCode.set(code);
    this.store.dispatch(roomActions.hubConnect());
  }

  ngOnDestroy(): void {
    this.store.dispatch(roomActions.leaveRoom());
  }

  sendReady() {
    this.store.dispatch(roomActions.setReady({ ready: true }));
  }
}
