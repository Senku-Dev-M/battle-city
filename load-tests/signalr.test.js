const { InfluxDB, Point } = require('@influxdata/influxdb-client');
const signalR = require('@microsoft/signalr');

// Simulates 20 players sending real-time moves via SignalR
// Metrics are written to InfluxDB.
const SIGNALR_URL = process.env.SIGNALR_URL || 'http://localhost:5000/gamehub';
const INFLUX_URL = process.env.INFLUX_URL || 'http://localhost:8086';
const INFLUX_TOKEN = process.env.INFLUX_TOKEN || 'battletanks-token';
const INFLUX_ORG = process.env.INFLUX_ORG || 'battletanks';
const INFLUX_BUCKET = process.env.INFLUX_BUCKET || 'k6';

const influx = new InfluxDB({ url: INFLUX_URL, token: INFLUX_TOKEN });
const writeApi = influx.getWriteApi(INFLUX_ORG, INFLUX_BUCKET);

async function runClient(id) {
  const connection = new signalR.HubConnectionBuilder()
    .withUrl(SIGNALR_URL)
    .configureLogging(signalR.LogLevel.None)
    .build();

  await connection.start();
  const start = Date.now();
  await connection.invoke('Move', { x: id, y: id });
  const latency = Date.now() - start;
  const point = new Point('signalr').intField('latency', latency).tag('client', String(id));
  writeApi.writePoint(point);
  await connection.stop();
}

async function main() {
  const clients = [];
  for (let i = 0; i < 20; i++) {
    clients.push(runClient(i + 1));
  }
  await Promise.all(clients);
  await writeApi.close();
}

main().catch((err) => {
  console.error(err);
  writeApi.close().catch(() => {});
});
