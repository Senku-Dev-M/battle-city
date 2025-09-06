const fetch = require('node-fetch');

async function createRoom(apiUrl, token, name, maxPlayers = 4, isPublic = true, creatorName = null) {
  const res = await fetch(`${apiUrl}/api/v1/Rooms`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${token}`
    },
    body: JSON.stringify({ name, maxPlayers, isPublic, creatorName })
  });

  const data = await res.json();
  if (!res.ok) {
    throw new Error('Failed to create room: ' + JSON.stringify(data));
  }

  return data; // Expected to contain roomId and roomCode
}

module.exports = { createRoom };
