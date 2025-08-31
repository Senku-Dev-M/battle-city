import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  vus: 10,
  duration: '30s',
};

const BASE_URL = __ENV.API_BASE_URL || 'http://localhost:5284';

export default function () {
  const res = http.get(`${BASE_URL}/api/v1/Health`);
  check(res, { 'status was 200': (r) => r.status === 200 });
  sleep(1);
}
