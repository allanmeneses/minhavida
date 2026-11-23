import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');

describe('observabilidade', () => {
  it('OpenTelemetry liga por variável e nasce desligado', () => {
    const setup = readFileSync(resolve(root, 'backend/src/Circulos.Api/Observability/ObservabilitySetup.cs'), 'utf8');
    assert.match(setup, /OTEL_ENABLED/);
    assert.match(setup, /OTEL_EXPORTER_OTLP_ENDPOINT/);
    assert.match(setup, /WithTracing/);
    assert.match(setup, /WithMetrics/);
    const settings = readFileSync(resolve(root, 'backend/src/Circulos.Api/appsettings.json'), 'utf8');
    assert.match(settings, /"Enabled": false/);
  });
});
