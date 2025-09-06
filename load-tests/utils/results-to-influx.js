const { InfluxDB, Point } = require('@influxdata/influxdb-client');
const fs = require('fs');
const path = require('path');

const client = new InfluxDB({
  url: 'http://localhost:8086',
  token: 'LgvCKeRz_F76MtplroS43YMcvHpq75y3WawHOPyjTHzJWcSRhvN1VY3lSDpMHHGi7_wZF48NJMSnO8sEncvOHw=='
});

const writeApi = client.getWriteApi('battletanks', 'k6');

// Cargar el archivo results.json exportado de Artillery
const resultsPath = path.join(__dirname, 'results.json');
const results = JSON.parse(fs.readFileSync(resultsPath, 'utf8'));

// Recorremos las métricas intermedias
results.intermediate.forEach((entry, idx) => {
  const http = entry.summaries?.['http.response_time'];
  const sessions = entry.summaries?.['vusers.session_length'];

  if (http) {
    const point = new Point('artillery')
      .tag('batch', String(idx))
      .intField('http_requests', entry.counters['http.requests'] || 0)
      .floatField('http_response_time_avg', http.mean || 0)
      .floatField('http_response_time_p95', http.p95 || 0)
      .floatField('http_response_time_p99', http.p99 || 0);

    if (sessions) {
      point.floatField('session_duration_avg', sessions.mean || 0)
           .floatField('session_duration_p95', sessions.p95 || 0);
    }

    writeApi.writePoint(point);
  }
});

writeApi
  .close()
  .then(() => console.log("✅ Resultados importados a InfluxDB"))
  .catch((err) => console.error("❌ Error importando a InfluxDB:", err));
