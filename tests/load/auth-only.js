import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter, Rate, Trend } from 'k6/metrics';

const BASE_URL = (__ENV.BASE_URL || 'http://localhost:5000').replace(/\/$/, '');
const PASSWORD = __ENV.K6_PASSWORD || 'Demo123!';
const MODE = (__ENV.AUTH_MODE || __ENV.MODE || 'shared').toLowerCase();
const ONCE_PER_VU = (__ENV.AUTH_ONCE_PER_VU || 'true').toLowerCase() !== 'false';

const SEEDED_EMAILS = [
  'admin@nexus.com',
  'alex.morgan@nexus.com',
  'executive@nexus.com',
  'finance.controller@nexus.com',
  'liam.chen@nexus.com',
  'omar.haddad@nexus.com',
  'pmo.admin@nexus.com',
  'portfolio.manager@nexus.com',
  'priya.rao@nexus.com',
  'project.manager@nexus.com',
  'resource.manager@nexus.com',
  'sophia.novak@nexus.com',
  'team.member@nexus.com',
  'user1@nexus.com',
  'user2@nexus.com',
  'user3@nexus.com',
  'user4@nexus.com',
  'user5@nexus.com',
  'user6@nexus.com',
  'user7@nexus.com',
  'user8@nexus.com',
  'user9@nexus.com',
  ...Array.from({ length: 71 }, (_, index) => `user${index + 10}@nexus.com`),
];

export const options = {
  vus: Number(__ENV.VUS || 10),
  duration: __ENV.DURATION || '15s',
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)'],
};

const loginDuration = new Trend('login_duration', true);
const loginFailureRate = new Rate('login_failure_rate');
const loginRequests = new Counter('login_requests');
const loginFailures = new Counter('login_failures');
const login4xx = new Counter('login_4xx');
const login5xx = new Counter('login_5xx');
const timeouts = new Counter('timeouts');

function emailForVu() {
  if (__ENV.K6_USER_EMAIL) return __ENV.K6_USER_EMAIL;
  if (MODE === 'distinct') return SEEDED_EMAILS[(__VU - 1) % SEEDED_EMAILS.length];
  return 'admin@nexus.com';
}

function login() {
  const response = http.post(
    `${BASE_URL}/api/auth/login`,
    JSON.stringify({ email: emailForVu(), password: PASSWORD }),
    {
      headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
      tags: { endpoint: 'login_auth_only', auth_mode: MODE },
      timeout: __ENV.REQUEST_TIMEOUT || '30s',
    },
  );
  const success = response.status >= 200 && response.status < 300;
  loginDuration.add(response.timings.duration);
  loginRequests.add(1);
  loginFailureRate.add(!success);
  if (!success) loginFailures.add(1);
  if (response.status >= 400 && response.status < 500) login4xx.add(1);
  if (response.status >= 500 && response.status < 600) login5xx.add(1);
  if (response.status === 0 || response.error_code === 105 || String(response.error || '').toLowerCase().includes('timeout')) timeouts.add(1);
  if (!success) {
    console.warn(`login failure mode=${MODE} vu=${__VU} status=${response.status} error=${response.error || 'none'} error_code=${response.error_code || 'none'} body=${String(response.body || '').slice(0, 3500)}`);
  }
  check(response, { 'login returned 2xx': () => success });
  return success;
}

let completed;

export default function () {
  if (!ONCE_PER_VU || completed === undefined) completed = login();
  sleep(1);
}
