const fetch = require('node-fetch');

async function registerUser(apiUrl, username, email, password) {
  const res = await fetch(`${apiUrl}/api/v1/Auth/register`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username, email, password })
  });

  if (!res.ok) {
    const data = await res.text();
    throw new Error(`Failed to register ${username}: ${res.status} ${data}`);
  }

  return res.json();
}

module.exports = { registerUser };
