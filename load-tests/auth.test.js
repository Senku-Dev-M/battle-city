import http from 'k6/http';
import { check } from 'k6';

// Simulates 100 concurrent authentications.
// Run with: k6 run --out influxdb=http://localhost:8086/k6 auth.test.js
export const options = {
  vus: 100,
  duration: '1m',
};

const BASE_URL = __ENV.API_BASE_URL || 'http://localhost:3000';

export default function () {
  const payload = JSON.stringify({
    username: 'player',
    password: 'secret',
  });
  const params = { headers: { 'Content-Type': 'application/json' } };
  const res = http.post(`${BASE_URL}/api/auth/login`, payload, params);
  check(res, { 'status was 200': (r) => r.status === 200 });
}
