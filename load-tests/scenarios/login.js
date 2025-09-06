const fetch = require('node-fetch');

async function loginUser(apiUrl, usernameOrEmail, password) {
  const res = await fetch(`${apiUrl}/api/v1/Auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ usernameOrEmail, password })
  });

  if (!res.ok) {
    const data = await res.text();
    throw new Error(`Failed to login ${usernameOrEmail}: ${res.status} ${data}`);
  }

  const setCookie = res.headers.get('set-cookie');
  if (setCookie) {
    return setCookie.split(';')[0].split('=')[1];
  }

  const data = await res.json();
  throw new Error('No token found. Response: ' + JSON.stringify(data));
}

module.exports = { loginUser };
