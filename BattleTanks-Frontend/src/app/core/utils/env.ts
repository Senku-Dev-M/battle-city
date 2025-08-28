export const env = {
  API_BASE_URL: (import.meta as any)?.env?.VITE_API_BASE_URL ?? 'http://localhost:5284/api/v1',
  HUB_URL: (import.meta as any)?.env?.VITE_HUB_URL ?? 'http://localhost:5284/game-hub',
  // WebSocket URL for connecting to the MQTT broker. EMQX exposes a WebSocket listener on port 8083.
  MQTT_URL: (import.meta as any)?.env?.VITE_MQTT_URL ?? 'ws://localhost:8083/mqtt',
};
