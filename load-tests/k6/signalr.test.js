const fetch = require('node-fetch');
const { InfluxDB, Point } = require('@influxdata/influxdb-client');
const signalR = require('@microsoft/signalr');

// ================================
// Configuración de API y SignalR
// ================================
const API_URL = process.env.API_URL || 'http://localhost:5284/api/v1/Auth/login';
const SIGNALR_URL = process.env.SIGNALR_URL || 'http://localhost:5284/game-hub';
const INFLUX_URL = process.env.INFLUX_URL || 'http://localhost:8086';
const INFLUX_TOKEN = process.env.INFLUX_TOKEN || 'battletanks-token';
const INFLUX_ORG = process.env.INFLUX_ORG || 'battletanks';
const INFLUX_BUCKET = process.env.INFLUX_BUCKET || 'k6';

// Sala de pruebas
const ROOM_CODE = process.env.ROOM_CODE || 'ZH7VFW';

// ================================
// Configuración de InfluxDB
// ================================
const influx = new InfluxDB({ url: INFLUX_URL, token: INFLUX_TOKEN });
const writeApi = influx.getWriteApi(INFLUX_ORG, INFLUX_BUCKET);

// ================================
// Función para obtener el JWT
// ================================
async function getJwtToken() {
  const res = await fetch(API_URL, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      usernameOrEmail: 'faker',
      password: 'password',
    }),
  });

  const setCookie = res.headers.get('set-cookie');
  if (setCookie) {
    const jwtCookie = setCookie.split(';')[0];
    const token = jwtCookie.split('=')[1];
    return token;
  }

  const data = await res.json();
  throw new Error('No token found. Response: ' + JSON.stringify(data));
}

// ================================
// Función que simula un cliente
// ================================
async function runClient(id, token) {
  const connection = new signalR.HubConnectionBuilder()
    .withUrl(`${SIGNALR_URL}?access_token=${token}`)
    .configureLogging(signalR.LogLevel.None)
    .build();

  const start = Date.now();
  await connection.start();

  await connection.invoke('JoinRoom', ROOM_CODE, `Client-${id}`, null);
  await connection.invoke('SendChat', `Hello from client ${id}`);
  await connection.invoke('UpdatePosition', {
    userId: `client-${id}`,
    x: id * 10,
    y: id * 5,
    rotation: 0,
    timestamp: Date.now()
  });

  // Medir latencia
  const latency = Date.now() - start;
  console.log(`Cliente ${id} -> Latencia: ${latency} ms`);

  // Registrar en Influx
  const point = new Point('signalr')
    .intField('latency', latency)
    .tag('client', String(id));
  writeApi.writePoint(point);

  await connection.stop();
}

// ================================
// Función principal
// ================================
async function main() {
  try {
    const token = await getJwtToken();
    console.log("JWT obtenido correctamente");

    const clients = [];
    for (let i = 0; i < 10; i++) {
      clients.push(runClient(i + 1, token));
    }
    await Promise.all(clients);

    console.log("Simulación finalizada");
  } catch (err) {
    console.error("Error en la simulación:", err);
  } finally {
    await writeApi.close();
  }
}

main();
