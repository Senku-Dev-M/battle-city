const signalR = require('@microsoft/signalr');
const { Point } = require('@influxdata/influxdb-client');

async function play(signalrUrl, token, roomCode, username, writeApi) {
  const connection = new signalR.HubConnectionBuilder()
    .withUrl(`${signalrUrl}?access_token=${token}`)
    .configureLogging(signalR.LogLevel.None)
    .build();

  const start = Date.now();
  await connection.start();

  await connection.invoke('JoinRoom', roomCode, username, null);
  await connection.invoke('SendChat', `Hello from ${username}`);
  await connection.invoke('UpdatePosition', {
    userId: username,
    x: 1,
    y: 1,
    rotation: 0,
    timestamp: Date.now()
  });

  const latency = Date.now() - start;
  console.log(`${username} -> Latency: ${latency} ms`);

  if (writeApi) {
    const point = new Point('signalr')
      .tag('client', username)
      .intField('latency', latency);
    writeApi.writePoint(point);
  }

  await connection.stop();
  return latency;
}

module.exports = { play };
