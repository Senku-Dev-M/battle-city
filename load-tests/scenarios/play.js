const signalR = require('@microsoft/signalr');
const { Point } = require('@influxdata/influxdb-client');

async function play(signalrUrl, token, roomCode, username, writeApi) {
  const connection = new signalR.HubConnectionBuilder()
    .withUrl(`${signalrUrl}?access_token=${token}`)
    .configureLogging(signalR.LogLevel.None)
    .build();

  const metrics = { username, connect: 0, join: 0, chat: 0, moves: [], total: 0 };

  const connectStart = Date.now();
  await connection.start();
  metrics.connect = Date.now() - connectStart;

  const joinStart = Date.now();
  await connection.invoke('JoinRoom', roomCode, username, null);
  metrics.join = Date.now() - joinStart;

  const chatStart = Date.now();
  await connection.invoke('SendChat', `Hello from ${username}`);
  metrics.chat = Date.now() - chatStart;

  for (let i = 0; i < 5; i++) {
    const moveStart = Date.now();
    await connection.invoke('UpdatePosition', {
      userId: username,
      x: i + 1,
      y: i + 1,
      rotation: (i * 90) % 360,
      timestamp: Date.now()
    });
    const moveLatency = Date.now() - moveStart;
    metrics.moves.push(moveLatency);
    console.log(`${username} -> Move ${i + 1} latency: ${moveLatency} ms`);

    if (writeApi) {
      const movePoint = new Point('signalr_move')
        .tag('client', username)
        .intField('move', i + 1)
        .intField('latency', moveLatency);
      writeApi.writePoint(movePoint);
    }
  }

  metrics.total = metrics.connect + metrics.join + metrics.chat + metrics.moves.reduce((a, b) => a + b, 0);
  console.log(`${username} metrics:`, metrics);

  if (writeApi) {
    const point = new Point('signalr')
      .tag('client', username)
      .intField('connect', metrics.connect)
      .intField('join', metrics.join)
      .intField('chat', metrics.chat)
      .intField('total', metrics.total);
    writeApi.writePoint(point);
  }

  await connection.stop();
  return metrics;
}

module.exports = { play };
