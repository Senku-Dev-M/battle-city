const { registerUser } = require('./register');
const { loginUser } = require('./login');
const { createRoom } = require('./createRoom');
const { play } = require('./play');
const { InfluxDB } = require('@influxdata/influxdb-client');

async function runScenario() {
  const API_URL = process.env.API_URL || 'http://localhost:5284';
  const SIGNALR_URL = process.env.SIGNALR_URL || 'http://localhost:5284/game-hub';
  const INFLUX_URL = process.env.INFLUX_URL || 'http://localhost:8086';
  const INFLUX_TOKEN = process.env.INFLUX_TOKEN || 'htt1gQEdOGoYX1Ol-PtU9wJ4KJ1bPSLhYxZ-2gJehw4SZBjHoQr32EMXG8fyZ2sJrICDMsHnjqcOna96qkV7Zw==';
  const INFLUX_ORG = process.env.INFLUX_ORG || 'battletanks';
  const INFLUX_BUCKET = process.env.INFLUX_BUCKET || 'k6';

  const influx = new InfluxDB({ url: INFLUX_URL, token: INFLUX_TOKEN });
  const writeApi = influx.getWriteApi(INFLUX_ORG, INFLUX_BUCKET);

  const user1 = `player_${Date.now()}`;
  const user2 = `${user1}_b`;

  await registerUser(API_URL, user1, `${user1}@test.com`, 'password');
  await registerUser(API_URL, user2, `${user2}@test.com`, 'password');

  const token1 = await loginUser(API_URL, user1, 'password');
  const token2 = await loginUser(API_URL, user2, 'password');

  const room = await createRoom(API_URL, token1, 'loadtest-room', 4, true, user1);
  const roomCode = room.roomCode || room.RoomCode;

  const results = await Promise.all([
    play(SIGNALR_URL, token1, roomCode, user1, writeApi),
    play(SIGNALR_URL, token2, roomCode, user2, writeApi)
  ]);

  await writeApi.close();

  results.forEach(r => {
    const avgMove = r.moves.reduce((a, b) => a + b, 0) / r.moves.length;
    console.log(`${r.username} -> total: ${r.total} ms, avg move: ${avgMove.toFixed(2)} ms`);
  });

  const avgTotal = results.reduce((sum, r) => sum + r.total, 0) / results.length;
  const avgMoveAll = results.reduce((sum, r) => {
    return sum + r.moves.reduce((a, b) => a + b, 0) / r.moves.length;
  }, 0) / results.length;

  console.log('Summary:', {
    averageTotalLatency: Number(avgTotal.toFixed(2)),
    averageMoveLatency: Number(avgMoveAll.toFixed(2))
  });
}

runScenario().catch(err => {
  console.error('Scenario failed:', err);
  process.exit(1);
});
