import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  vus: 100,
  duration: '60s',
};

const baseUrl = __ENV.API_URL || 'http://localhost:5000';

export function setup() {
  for (let i = 1; i <= options.vus; i++) {
    const registerPayload = JSON.stringify({
      username: `user${i}`,
      email: `user${i}@test.com`,
      password: 'P@ssw0rd!'
    });
    http.post(`${baseUrl}/api/v1/Auth/register`, registerPayload, { headers: { 'Content-Type': 'application/json' } });
  }
}

export default function () {
  const loginPayload = JSON.stringify({
    usernameOrEmail: `user${__VU}@test.com`,
    password: 'P@ssw0rd!'
  });

  const params = { headers: { 'Content-Type': 'application/json' } };
  const loginRes = http.post(`${baseUrl}/api/v1/Auth/login`, loginPayload, params);
  check(loginRes, { 'login status 200': r => r.status === 200 });

  // hit rooms listing as a critical endpoint
  const roomsRes = http.get(`${baseUrl}/api/v1/Rooms`);
  check(roomsRes, { 'rooms status 200': r => r.status === 200 || r.status === 204 });

  sleep(1);
}
