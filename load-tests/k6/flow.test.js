import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  vus: 5,
  duration: '30s',
};

const BASE_URL = __ENV.API_BASE_URL || 'http://localhost:5284';

function randomUser() {
  const id = `${__VU}_${Date.now()}`;
  return {
    username: `k6user_${id}`,
    email: `k6user_${id}@example.com`,
    password: 'Password123!'
  };
}

export default function () {
  const user = randomUser();

  // Register user
  let res = http.post(`${BASE_URL}/api/v1/Auth/register`, JSON.stringify({
    username: user.username,
    email: user.email,
    password: user.password,
    confirmPassword: user.password,
  }), { headers: { 'Content-Type': 'application/json' } });
  check(res, { 'register succeeded': (r) => r.status === 200 });

  // Login user
  res = http.post(`${BASE_URL}/api/v1/Auth/login`, JSON.stringify({
    usernameOrEmail: user.username,
    password: user.password,
  }), { headers: { 'Content-Type': 'application/json' } });
  check(res, { 'login succeeded': (r) => r.status === 200 });

  // Create room
  const roomPayload = {
    name: `Room_${__VU}_${Date.now()}`,
    maxPlayers: 4,
    isPublic: true,
  };
  res = http.post(`${BASE_URL}/api/v1/Rooms`, JSON.stringify(roomPayload), { headers: { 'Content-Type': 'application/json' } });
  check(res, { 'room created': (r) => r.status === 201 });

  // List rooms
  res = http.get(`${BASE_URL}/api/v1/Rooms`);
  check(res, { 'rooms listed': (r) => r.status === 200 });

  sleep(1);
}
