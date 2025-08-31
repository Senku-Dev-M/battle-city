import http from 'k6/http';
import { check } from 'k6';

// Simulates 100 concurrent authentications.
// Run with: k6 run --out influxdb=http://admin:admin123@localhost:8086/k6 load-tests/auth.test.js
export const options = {
  vus: 100,
  duration: '1m',
};

const BASE_URL = __ENV.API_BASE_URL || 'http://localhost:5284';

export default function () {
  const payload = JSON.stringify({
    usernameOrEmail: 'faker',
    password: 'password',
  });
  const params = { headers: { 'Content-Type': 'application/json' } };
  const res = http.post(`${BASE_URL}/api/v1/Auth/login`, payload, params);
  check(res, { 'status was 200': (r) => r.status === 200 });
}
