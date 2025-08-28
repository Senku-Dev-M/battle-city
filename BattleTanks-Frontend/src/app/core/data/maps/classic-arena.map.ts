/**
 * Defines the shape of a map used by the game. A map is composed of a grid of
 * cells along with some metadata such as a name and spawn points. Each cell
 * contains a numeric code indicating the type of terrain. The following
 * conventions are used for the cell values:
 *
 * 0 → Empty, passable space.
 * 1 → Destructible block. Bullets can destroy these bricks.
 * 2 → Indestructible block. Bullets cannot destroy or pass through these.
 * 3 → Special objective (e.g. a base). For now these behave like
 *     destructible blocks.
 */
export interface GameMap {
  /** Unique identifier for the map */
  id: string;
  /** Human friendly name */
  name: string;
  /** Number of columns in the map */
  width: number;
  /** Number of rows in the map */
  height: number;
  /** Spawn points for players */
  spawnPoints: { x: number; y: number }[];
  /** 2D array of cell types. The outer array is rows (y), inner is columns (x). */
  cells: number[][];
}

/**
 * A classic Battle City‑style arena. The layout loosely follows the original
 * NES level with an outer wall of indestructible blocks, a protected base
 * (value 3) and various destructible bricks scattered throughout. The map is
 * 20×20 cells, matching a 40‑pixel grid in the canvas renderer. Spawn points
 * are placed near the corners and bottom centre for variety.
 */
export const BATTLE_CITY_MAP: GameMap = {
  id: 'battle-city-classic',
  name: 'Battle City Classic',
  width: 20,
  height: 20,
  spawnPoints: [
    { x: 1, y: 1 },
    { x: 18, y: 1 },
    { x: 9, y: 18 },
    { x: 10, y: 18 },
  ],
  cells: [
    [2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2],
    [2, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 2],
    [2, 0, 1, 0, 1, 0, 1, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0, 1, 0, 2],
    [2, 0, 1, 0, 1, 0, 1, 0, 0, 2, 2, 0, 0, 1, 0, 1, 0, 1, 0, 2],
    [2, 0, 1, 0, 1, 0, 1, 0, 0, 2, 2, 0, 0, 1, 0, 1, 0, 1, 0, 2],
    [2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2],
    [2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2],
    [2, 0, 0, 1, 1, 1, 0, 0, 1, 1, 1, 1, 0, 0, 1, 1, 1, 0, 0, 2],
    [2, 0, 0, 1, 2, 1, 0, 0, 1, 2, 2, 1, 0, 0, 1, 2, 1, 0, 0, 2],
    [2, 0, 0, 1, 2, 1, 0, 0, 1, 2, 2, 1, 0, 0, 1, 2, 1, 0, 0, 2],
    [2, 0, 0, 1, 1, 1, 0, 0, 1, 1, 1, 1, 0, 0, 1, 1, 1, 0, 0, 2],
    [2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2],
    [2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2],
    [2, 0, 1, 0, 1, 0, 1, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0, 1, 0, 2],
    [2, 0, 1, 0, 1, 0, 1, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0, 1, 0, 2],
    [2, 0, 1, 0, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 0, 1, 0, 2],
    [2, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 2],
    [2, 0, 0, 1, 1, 1, 0, 0, 0, 1, 1, 0, 0, 0, 1, 1, 1, 0, 0, 2],
    [2, 0, 0, 1, 3, 1, 0, 0, 0, 1, 1, 0, 0, 0, 1, 3, 1, 0, 0, 2],
    [2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2],
  ],
};
