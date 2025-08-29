import http from 'k6/http';
import { check, sleep } from 'k6';

// Basic REST load test.
// Run with: k6 run --out influxdb=http://localhost:8086/k6 k6.test.js
// API_BASE_URL can be overridden via environment variable.
export const options = {
  vus: 10,
  duration: '30s',
};

const BASE_URL = __ENV.API_BASE_URL || 'http://localhost:3000';

export default function () {
  const res = http.get(`${BASE_URL}/api/health`);
  check(res, { 'status was 200': (r) => r.status === 200 });
  sleep(1);
}
