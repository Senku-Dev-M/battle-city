import { Injectable, OnDestroy, signal } from '@angular/core';
import { SignalRService } from '../../core/services/signalr.service';
import { PowerUpDto } from '../../core/models/game.models';
import { Subscription } from 'rxjs';

/**
 * Handles game-room level state that was previously managed inside the
 * RoomCanvasComponent. Subscribes to SignalR streams and exposes reactive
 * signals for map and power-up data so the component can focus purely on
 * rendering.
 */
@Injectable({ providedIn: 'root' })
export class GameRoomService implements OnDestroy {
  private subs: Subscription[] = [];

  readonly map = signal({ width: 0, height: 0, cells: [] as number[][] });
  readonly powerUps = signal<PowerUpDto[]>([]);

  constructor(private signalR: SignalRService) {
    this.subs.push(
      this.signalR.mapState$.subscribe((cells: any[]) => {
        if (!cells || cells.length === 0) return;
        const width = Math.max(...cells.map(c => c.x)) + 1;
        const height = Math.max(...cells.map(c => c.y)) + 1;
        const grid = Array.from({ length: height }, () => Array(width).fill(0));
        for (const cell of cells) {
          const val = cell.isDestroyed ? 0 : cell.type;
          grid[cell.y][cell.x] = val;
        }
        this.map.set({ width, height, cells: grid });
      })
    );

    this.subs.push(
      this.signalR.cellDestroyed$.subscribe((cell: any) => {
        const m = this.map();
        if (m.cells[cell.y]) {
          m.cells[cell.y][cell.x] = 0;
        }
      })
    );

    this.subs.push(this.signalR.powerUpState$.subscribe(list => {
      this.powerUps.set(list || []);
    }));
    this.subs.push(this.signalR.powerUpSpawned$.subscribe(p => {
      this.powerUps.update(arr => [...arr, p]);
    }));
    this.subs.push(this.signalR.powerUpRemoved$.subscribe(id => {
      this.powerUps.update(arr => arr.filter(p => p.powerUpId !== id));
    }));
  }

  loadInitialState() {
    this.signalR.getMap().catch(() => {});
    this.signalR.getPowerUps().catch(() => {});
  }

  ngOnDestroy(): void {
    this.subs.forEach(s => s.unsubscribe());
  }
}
