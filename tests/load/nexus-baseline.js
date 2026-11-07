import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter, Rate, Trend } from 'k6/metrics';

const BASE_URL = (__ENV.BASE_URL || 'http://localhost:5000').replace(/\/$/, '');
const TENANT_ID = __ENV.TENANT_ID || '11111111-1111-1111-1111-111111111111';
// No default credential: supply the seeded demo password explicitly.
const DEFAULT_PASSWORD = __ENV.K6_PASSWORD;
if (!DEFAULT_PASSWORD) {
  throw new Error('K6_PASSWORD must be set to the seeded development password before running this test.');
}
const THINK_MIN_SECONDS = Number(__ENV.THINK_MIN_SECONDS || 1);
const THINK_MAX_SECONDS = Number(__ENV.THINK_MAX_SECONDS || 3);

// The seed contains these independent demo identities. Each VU receives its own
// login and access token; refresh-token rotation is intentionally not exercised.
const DEMO_EMAILS = [
  'admin@nexus.com',
  'executive@nexus.com',
  'portfolio.manager@nexus.com',
  'project.manager@nexus.com',
  'team.member@nexus.com',
  'pmo.admin@nexus.com',
  'finance.controller@nexus.com',
  'resource.manager@nexus.com',
];

export const options = {
  vus: Number(__ENV.VUS || 10),
  duration: __ENV.DURATION || '2m',
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)'],
  thresholds: {
    failed_requests: ['rate<0.01'],
    normal_api_duration: ['p(95)<500'],
    http_5xx: ['count==0'],
    login_failure_rate: ['rate<0.01'],
  },
};

const failedRequests = new Rate('failed_requests');
const normalApiDuration = new Trend('normal_api_duration', true);
const normalApiFailures = new Rate('normal_api_failures');
const http4xx = new Counter('http_4xx');
const http5xx = new Counter('http_5xx');
const timeouts = new Counter('timeouts');
const loginFailures = new Counter('login_failures');
const loginFailureRate = new Rate('login_failure_rate');
const dataSelectionFailures = new Counter('data_selection_failures');

const endpointMetrics = {
  login: createEndpointMetrics('login'),
  current_user: createEndpointMetrics('current_user'),
  projects_list: createEndpointMetrics('projects_list'),
  project_details: createEndpointMetrics('project_details'),
  planning_bootstrap: createEndpointMetrics('planning_bootstrap'),
  risks_list: createEndpointMetrics('risks_list'),
  risk_details: createEndpointMetrics('risk_details'),
  risk_actions: createEndpointMetrics('risk_actions'),
};

function createEndpointMetrics(prefix) {
  return {
    duration: new Trend(`${prefix}_duration`, true),
    requests: new Counter(`${prefix}_requests`),
    failures: new Counter(`${prefix}_failures`),
  };
}

function authHeaders(token) {
  return {
    Authorization: `Bearer ${token}`,
    Accept: 'application/json',
    'X-Tenant-Id': TENANT_ID,
  };
}

function jsonBody(response) {
  try {
    return response.json();
  } catch (_) {
    return null;
  }
}

function isSuccess(response) {
  return response && response.status >= 200 && response.status < 300;
}

function request(endpoint, method, path, body, headers) {
  const params = {
    headers: headers || { Accept: 'application/json' },
    tags: { endpoint },
    timeout: __ENV.REQUEST_TIMEOUT || '30s',
  };
  const response = method === 'POST'
    ? http.post(`${BASE_URL}${path}`, JSON.stringify(body || {}), { ...params, headers: { ...params.headers, 'Content-Type': 'application/json' } })
    : http.get(`${BASE_URL}${path}`, params);

  const metrics = endpointMetrics[endpoint];
  metrics.duration.add(response.timings.duration);
  metrics.requests.add(1);

  const success = isSuccess(response);
  failedRequests.add(!success);
  if (endpoint !== 'login') {
    normalApiDuration.add(response.timings.duration);
    normalApiFailures.add(!success);
  }
  if (!success) metrics.failures.add(1);
  if (response.status >= 400 && response.status < 500) http4xx.add(1);
  if (response.status >= 500 && response.status < 600) http5xx.add(1);
  const errorText = String(response.error || '').toLowerCase();
  if (response.status === 0 || response.error_code === 105 || errorText.includes('timeout')) timeouts.add(1);

  check(response, { [`${endpoint} returned 2xx`]: () => success });
  return response;
}

function think() {
  const min = Number.isFinite(THINK_MIN_SECONDS) ? THINK_MIN_SECONDS : 1;
  const max = Number.isFinite(THINK_MAX_SECONDS) ? Math.max(min, THINK_MAX_SECONDS) : Math.max(min, 3);
  sleep(min + Math.random() * (max - min));
}

function credentialForVu() {
  if (__ENV.K6_USER_EMAIL) return { email: __ENV.K6_USER_EMAIL, password: DEFAULT_PASSWORD };
  return { email: DEMO_EMAILS[(__VU - 1) % DEMO_EMAILS.length], password: DEFAULT_PASSWORD };
}

function firstItem(payload) {
  return payload && Array.isArray(payload.items) ? payload.items[0] : null;
}

let session;

export default function () {
  if (!session) {
    const credentials = credentialForVu();
    const login = request('login', 'POST', '/api/auth/login', credentials);
    const loginPayload = jsonBody(login);
    const token = loginPayload && (loginPayload.token || loginPayload.accessToken);
    loginFailureRate.add(!isSuccess(login) || !token);
    if (!isSuccess(login) || !token) {
      loginFailures.add(1);
      return;
    }
    session = { token };
  }

  const headers = authHeaders(session.token);

  think();
  request('current_user', 'GET', '/api/auth/me', null, headers);

  think();
  const projectsResponse = request('projects_list', 'GET', '/api/projects?page=1&pageSize=20', null, headers);
  if (!isSuccess(projectsResponse)) return;
  const project = firstItem(jsonBody(projectsResponse));
  if (!project || !project.id) {
    dataSelectionFailures.add(1);
    return;
  }
  const projectId = project.id;

  think();
  request('project_details', 'GET', `/api/projects/${encodeURIComponent(projectId)}`, null, headers);

  think();
  request('planning_bootstrap', 'GET', `/api/projects/${encodeURIComponent(projectId)}/plans/bootstrap`, null, headers);

  think();
  const risksResponse = request('risks_list', 'GET', '/api/risks?page=1&pageSize=20', null, headers);
  if (!isSuccess(risksResponse)) return;
  const risk = firstItem(jsonBody(risksResponse));
  if (!risk || !risk.id) {
    // Empty risk data is a valid data-volume condition, not a fabricated ID.
    dataSelectionFailures.add(1);
    return;
  }
  const riskId = risk.id;

  think();
  request('risk_details', 'GET', `/api/risks/${encodeURIComponent(riskId)}`, null, headers);

  think();
  request('risk_actions', 'GET', `/api/risks/${encodeURIComponent(riskId)}/actions`, null, headers);
}
