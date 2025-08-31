import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  HostListener,
  OnDestroy,
  ViewChild,
  inject,
  signal,
  computed,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { Store } from '@ngrx/store';
import { toSignal } from '@angular/core/rxjs-interop';
import { selectBullets, selectPlayers } from '../store/room.selectors';
import { selectUser } from '../../auth/store/auth.selectors';
import { roomActions } from '../store/room.actions';
import { SignalRService } from '../../../core/services/signalr.service';
import { BulletHitReportDto, PowerUpDto } from '../../../core/models/game.models';
import { Subscription } from 'rxjs';
import { effect } from '@angular/core';

@Component({
  standalone: true,
  selector: 'app-room-canvas',
  imports: [CommonModule],
  templateUrl: './room-canvas.component.html',
  styleUrls: ['./room-canvas.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RoomCanvasComponent implements AfterViewInit, OnDestroy {
  private store = inject(Store);

  @ViewChild('wrap', { static: true }) wrapRef!: ElementRef<HTMLDivElement>;
  @ViewChild('gameCanvas', { static: true }) canvasRef!: ElementRef<HTMLCanvasElement>;

  players = toSignal(this.store.select(selectPlayers), { initialValue: [] });
  bullets = toSignal(this.store.select(selectBullets), { initialValue: [] });
  me      = toSignal(this.store.select(selectUser), { initialValue: null });
  powerUps: PowerUpDto[] = [];

  /**
   * Determine if the current player still has lives left. Returns true if there is no user (before joining)
   * or if their corresponding player state in the room indicates they are alive.
   */
  private isCurrentPlayerAlive(): boolean {
    const me   = this.me();
    const roster = this.players();
    if (!me) return true;
    const id   = me.id;
    const uname = me.username?.toLowerCase() ?? '';
    const p = roster.find(pl => pl.playerId === id || (pl.username?.toLowerCase() ?? '') === uname);
    return p ? p.isAlive : true;
  }

  /**
   * Returns true if the current player has an active bullet in flight that has not yet been
   * despawned (i.e. marked inactive or timed out).  As long as a shooter has any active bullet,
   * they cannot spawn another one.  This implements a single-bullet cooldown until a hit or timeout occurs.
   */
  private hasActiveBullet(): boolean {
    const me = this.me();
    if (!me) return false;
    const shooterId = me.id;
    const list = this.bullets();
    for (const b of list) {
      if (b.shooterId !== shooterId) continue;
      if (!b.isActive) continue;
      // Bullets that have been reported as hits are considered inactive client‑side
      if (this.reportedBullets.has(b.bulletId)) continue;
      return true;
    }
    return false;
  }

  private ctx: CanvasRenderingContext2D | null = null;
  private running = false;
  private lastTs = 0;
  private pressed = new Set<string>();

  // Canvas logical dimensions (world coordinates remain fixed at this size)
  private readonly WORLD_WIDTH = 800;
  private readonly WORLD_HEIGHT = 480;

  private px = signal(300);
  private py = signal(200);
  private rot = signal(0);

  // Keep track of bullets that have already been reported as hits
  private reportedBullets = new Set<string>();

  private signalR = inject(SignalRService);

  /**
   * The map used for rendering obstacles. It is populated from the server
   * and updated when cells are destroyed.
   */
  private map = { width: 0, height: 0, cells: [] as number[][] };
  private subs: Subscription[] = [];

  constructor() {
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
        this.map = { width, height, cells: grid };
      })
    );

    this.subs.push(
      this.signalR.cellDestroyed$.subscribe((cell: any) => {
        if (this.map.cells[cell.y]) {
          this.map.cells[cell.y][cell.x] = 0;
        }
      })
    );

    this.subs.push(this.signalR.powerUpState$.subscribe(list => {
      this.powerUps = list || [];
    }));
    this.subs.push(this.signalR.powerUpSpawned$.subscribe(p => {
      this.powerUps.push(p);
    }));
    this.subs.push(this.signalR.powerUpRemoved$.subscribe(id => {
      this.powerUps = this.powerUps.filter(p => p.powerUpId !== id);
    }));
  }

  /**
   * Indicates whether the initial spawn position for the current player has been applied.  When
   * a player joins a room, the server broadcasts a playerMoved message with the assigned spawn
   * coordinates.  This effect watches for the player state in the store and, if the local
   * position has not yet been updated from the server, synchronises the local px/py/rot signals
   * with the values provided by the server.  This prevents the client from starting at the
   * canvas centre while other clients have correct spawn positions.
   */
  private _initialSpawnSet = false;

  private _syncSpawnEffect = effect(() => {
    const me = this.me();
    if (!me || this._initialSpawnSet) return;
    const myId   = me.id;
    const myName = me.username?.toLowerCase() ?? '';
    const roster = this.players();
    const p = roster.find((pl: { playerId: any; username: string; }) => pl.playerId === myId || (pl.username?.toLowerCase() ?? '') === myName);
    if (!p) return;
    // If the server has provided a spawn position different from the current local position,
    // update the local signals once.  The rotation may also be set to align with the spawn.
    if (this.px() !== p.x || this.py() !== p.y) {
      this.px.set(p.x);
      this.py.set(p.y);
      this.rot.set(p.rotation || 0);
      this._initialSpawnSet = true;
    }
  });

  private _stopInputEffect = effect(() => {
    if (!this.isCurrentPlayerAlive()) {
      this.pressed.clear();
    }
  });

  ngAfterViewInit(): void {
    const canvas = this.canvasRef.nativeElement;
    this.ctx = canvas.getContext('2d');
    this.resizeCanvas(true);
    this.running = true;
    this.signalR.getMap().catch(() => {});
    this.signalR.getPowerUps().catch(() => {});
    requestAnimationFrame(this.loop);
  }

  ngOnDestroy(): void {
    this.running = false;
    this.subs.forEach(s => s.unsubscribe());
  }

  @HostListener('window:resize') onWindowResize() {
    this.resizeCanvas();
  }

  private resizeCanvas(initial = false) {
    const wrap = this.wrapRef.nativeElement;
    const canvas = this.canvasRef.nativeElement;

    // Calculate display size while keeping the logical canvas size fixed.
    let w = wrap.clientWidth || wrap.getBoundingClientRect().width || this.WORLD_WIDTH;
    w = Math.max(320, Math.floor(w));
    const h = Math.min(520, Math.round(w * (this.WORLD_HEIGHT / this.WORLD_WIDTH)));

    // Only set the canvas width/height once to keep world coordinates stable.
    if (initial) {
      canvas.width = this.WORLD_WIDTH;
      canvas.height = this.WORLD_HEIGHT;
    }

    // Scale the canvas via CSS so the drawing surface remains consistent
    canvas.style.width = `${w}px`;
    canvas.style.height = `${h}px`;
  }

  @HostListener('window:keydown', ['$event'])
  onDown(e: KeyboardEvent) {
    // Ignore input when the player has no lives left
    if (!this.isCurrentPlayerAlive()) return;
    this.pressed.add(e.key.toLowerCase());
    if (e.code === 'Space') {
      // Only spawn a bullet if the player has no active bullets in flight
      if (!this.hasActiveBullet()) {
        this.store.dispatch(
          roomActions.spawnBullet({ x: this.px(), y: this.py(), dir: this.rot(), speed: 500 })
        );
      }
      e.preventDefault();
    }
  }

  @HostListener('window:keyup', ['$event'])
  onUp(e: KeyboardEvent) {
    // Ignore releases if the player is dead
    if (!this.isCurrentPlayerAlive()) return;
    this.pressed.delete(e.key.toLowerCase());
  }

  private loop = (ts: number) => {
    if (!this.running || !this.ctx) return;
    const dt = (ts - (this.lastTs || ts)) / 1000;
    this.lastTs = ts;

    const me = this.me();
    const roster = this.players();
    const mePlayer = me ? roster.find(p => p.playerId === me.id || (p.username?.toLowerCase() ?? '') === (me.username?.toLowerCase() ?? '')) : null;
    const speed = mePlayer?.speed ?? 200;
    let dx = 0, dy = 0;
    if (this.pressed.has('w') || this.pressed.has('arrowup')) dy -= 1;
    if (this.pressed.has('s') || this.pressed.has('arrowdown')) dy += 1;
    if (this.pressed.has('a') || this.pressed.has('arrowleft')) dx -= 1;
    if (this.pressed.has('d') || this.pressed.has('arrowright')) dx += 1;
    if (dx !== 0 || dy !== 0) {
      // Only update movement and send position if the current player is alive
      if (this.isCurrentPlayerAlive()) {
        // Normalize direction vector
        const len = Math.hypot(dx, dy) || 1;
        const ndx = dx / len;
        const ndy = dy / len;

        // Proposed new position based on movement delta
        const c    = this.canvasRef.nativeElement;
        const gridW = this.map.width;
        const gridH = this.map.height;
        const cellW = c.width  / gridW;
        const cellH = c.height / gridH;
        const radius = 12; // approximate tank radius in pixels for collision detection
        let newX = this.px() + ndx * speed * dt;
        let newY = this.py() + ndy * speed * dt;

        // Clamp within canvas bounds taking the radius into account
        newX = Math.min(Math.max(newX, radius), c.width  - radius);
        newY = Math.min(Math.max(newY, radius), c.height - radius);

        // Determine the grid cells occupied by the tank at the proposed position
        const leftIdx   = Math.floor((newX - radius) / cellW);
        const rightIdx  = Math.floor((newX + radius) / cellW);
        const topIdx    = Math.floor((newY - radius) / cellH);
        const bottomIdx = Math.floor((newY + radius) / cellH);

        // Helper to determine if a given cell index is colliding with a solid block
        const isBlocked = (row: number, col: number): boolean => {
          if (row < 0 || row >= gridH || col < 0 || col >= gridW) return true;
          const val = this.map.cells[row][col];
          // Only cell value 0 is passable; any other value blocks movement
          return val !== 0;
        };

        // Check collisions at the four corners of the bounding box
        let collided = false;
        // top-left
        if (isBlocked(topIdx, leftIdx)) collided = true;
        // top-right
        if (isBlocked(topIdx, rightIdx)) collided = true;
        // bottom-left
        if (isBlocked(bottomIdx, leftIdx)) collided = true;
        // bottom-right
        if (isBlocked(bottomIdx, rightIdx)) collided = true;

        // Update rotation regardless of collision
        this.rot.set(Math.atan2(ndy, ndx));

        // If no collision, commit the new position
        if (!collided) {
          this.px.set(newX);
          this.py.set(newY);
        }

        // Dispatch updated position (even if unchanged) so other clients receive orientation changes
        const meId = this.me()?.id ?? 'me';
        this.store.dispatch(roomActions.updatePosition({
          dto: {
            playerId: meId,
            x: this.px(),
            y: this.py(),
            rotation: this.rot(),
            timestamp: Date.now(),
          },
        }));
      }
    }

    const ctx = this.ctx!;
    const c = this.canvasRef.nativeElement;
    ctx.clearRect(0, 0, c.width, c.height);

    // Background
    ctx.fillStyle = '#0b1e16';
    ctx.fillRect(0, 0, c.width, c.height);

    // Grid lines
    ctx.strokeStyle = 'rgba(34, 211, 238, 0.06)';
    ctx.lineWidth = 1;
    for (let x = 0; x < c.width; x += 40) {
      ctx.beginPath();
      ctx.moveTo(x, 0);
      ctx.lineTo(x, c.height);
      ctx.stroke();
    }
    for (let y = 0; y < c.height; y += 40) {
      ctx.beginPath();
      ctx.moveTo(0, y);
      ctx.lineTo(c.width, y);
      ctx.stroke();
    }

    // Map drawing occurs once above; duplicate removed.

    // Draw map obstacles. Compute cell dimensions based on canvas size and
    // map dimensions so that the grid scales with the canvas. Each cell
    // corresponds to a block of size (c.width / width) × (c.height / height).
    const gridW2 = this.map.width;
    const gridH2 = this.map.height;
    const cellW2 = c.width / gridW2;
    const cellH2 = c.height / gridH2;
    for (let row = 0; row < gridH2; row++) {
      const line = this.map.cells[row];
      for (let col = 0; col < gridW2; col++) {
        const val = line[col];
        // Skip empty cells for performance
        if (val === 0) continue;
        // Choose colours based on cell type. Values are chosen from the
        // tailwind colour palette for consistency with the rest of the UI.
        let fill: string;
        switch (val) {
          case 1: // destructible brick
            fill = '#b45309'; // amber‑600
            break;
          case 2: // indestructible wall
            fill = '#4b5563'; // stone‑600
            break;
          case 3: // special base (treated like destructible for now)
            fill = '#dc2626'; // red‑600
            break;
          default:
            fill = '#000000';
            break;
        }
        ctx.fillStyle = fill;
        ctx.fillRect(col * cellW2, row * cellH2, cellW2, cellH2);
        // Slightly darker outline for definition
        ctx.strokeStyle = 'rgba(0,0,0,0.3)';
        ctx.lineWidth = 1;
        ctx.strokeRect(col * cellW2, row * cellH2, cellW2, cellH2);
      }
    }

    // Draw power-ups
    this.powerUps.forEach(p => {
      ctx.fillStyle = p.type === 'shield' ? '#fde047' : '#4ade80';
      ctx.beginPath();
      ctx.arc(p.x, p.y, 10, 0, Math.PI * 2);
      ctx.fill();
    });

    // Bullets in bright cyan. Each bullet's position is derived from its
    // spawn position, direction and speed, along with the time since spawn.
    ctx.fillStyle = '#22d3ee';
    const now = Date.now();
    this.bullets().forEach(b => {
      if (!b.isActive) return;
      // Skip bullets already reported as hits
      if (this.reportedBullets.has(b.bulletId)) return;

      const elapsed = (now - b.spawnTimestamp) / 1000;
      const deltaX  = Math.cos(b.directionRadians) * b.speed * elapsed;
      const deltaY  = Math.sin(b.directionRadians) * b.speed * elapsed;
      const bx      = b.x + deltaX;
      const by      = b.y + deltaY;

      // If bullet goes out of bounds, mark it as reported to allow shooter to fire again.
      if (bx < 0 || by < 0 || bx > c.width || by > c.height) {
        this.reportedBullets.add(b.bulletId);
        return;
      }

      // Collision detection with map blocks. Convert bullet coordinates into grid indices.
      // If the bullet hits a block, mark the bullet as despawned, inform the server and
      // optionally destroy the block if it is destructible.
      const gx = Math.floor(bx / cellW2);
      const gy = Math.floor(by / cellH2);
      if (gx >= 0 && gy >= 0 && gx < gridW2 && gy < gridH2) {
        const cell = this.map.cells[gy][gx];
        if (cell === 1 || cell === 3) {
          if (!this.reportedBullets.has(b.bulletId)) {
            this.reportedBullets.add(b.bulletId);
            this.signalR.destroyCell(gx, gy).catch(() => {});
            this.signalR.reportBulletCollision(b.bulletId).catch(() => {});
          }
          return;
        } else if (cell === 2) {
          if (!this.reportedBullets.has(b.bulletId)) {
            this.reportedBullets.add(b.bulletId);
            this.signalR.reportBulletCollision(b.bulletId).catch(() => {});
          }
          return;
        }
      }

      // Draw the bullet
      ctx.beginPath();
      ctx.arc(bx, by, 3, 0, Math.PI * 2);
      ctx.fill();

      // Check for collisions with players (except the shooter)
      const BULLET_RADIUS = 3;
      const PLAYER_RADIUS = 12;
      this.players().forEach(p => {
        if (!p.isAlive) return;
        if (p.playerId === b.shooterId) return;
        const dist = Math.hypot(bx - p.x, by - p.y);
        if (dist < BULLET_RADIUS + PLAYER_RADIUS) {
          // Report hit once per bullet
          this.reportedBullets.add(b.bulletId);
          const hit: BulletHitReportDto = {
            bulletId: b.bulletId,
            targetPlayerId: p.playerId,
            hitX: bx,
            hitY: by,
            timestamp: now,
          };
          this.signalR.reportHit(hit);
        }
      });
    });

    // Check power-up collisions
    for (const p of [...this.powerUps]) {
      const dist = Math.hypot(this.px() - p.x, this.py() - p.y);
      if (dist < 20) {
        this.signalR.collectPowerUp(p.powerUpId).catch(() => {});
        this.powerUps = this.powerUps.filter(x => x.powerUpId !== p.powerUpId);
      }
    }

    const myId   = this.me()?.id ?? null;
    const myName = this.me()?.username?.toLowerCase() ?? null;

    // Players
    this.players().forEach(p => {
      const isMe = (p.playerId === myId) || (!!myName && p.username?.toLowerCase() === myName);

      // Animated edge when power-ups are active
      if (p.hasShield || p.speed > 200) {
        const pulse = (Math.sin(ts / 200) + 1) / 2; // 0..1 pulsing value
        const baseRadius = 16 + pulse * 2;
        ctx.lineWidth = 2 + pulse;
        if (p.hasShield) {
          ctx.strokeStyle = '#fde047';
          ctx.beginPath();
          ctx.arc(p.x, p.y, baseRadius, 0, Math.PI * 2);
          ctx.stroke();
        }
        if (p.speed > 200) {
          ctx.strokeStyle = '#4ade80';
          ctx.beginPath();
          ctx.arc(p.x, p.y, baseRadius + (p.hasShield ? 4 : 0), 0, Math.PI * 2);
          ctx.stroke();
        }
      }

      ctx.save();
      ctx.translate(p.x, p.y);
      ctx.rotate(p.rotation || 0);

      // Player body
      ctx.fillStyle = isMe ? '#86e5f7' : '#22d3ee';
      ctx.fillRect(-12, -8, 24, 16);
      ctx.fillStyle = isMe ? '#0ea5e9' : '#0284c7';
      ctx.fillRect(0, -2, 16, 4);

      ctx.restore();

      // Player name label
      const label = isMe ? 'TÚ' : (p.username ?? 'Tank');
      const bgW   = Math.max(24, label.length * 7);
      ctx.fillStyle = '#0b1e16';
      ctx.fillRect(p.x - 18, p.y - 22, bgW, 12);
      ctx.fillStyle = '#a7f3d0';
      ctx.font = '10px monospace';
      ctx.fillText(label, p.x - 16, p.y - 12);
    });

    requestAnimationFrame(this.loop);
  };
}
