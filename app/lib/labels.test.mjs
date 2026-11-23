import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import {
  CIRCLE_STATUS_LABEL,
  FREQUENCY_LABEL,
  LOG_ACTION_LABEL,
  ROLE_LABEL,
  TARGET_KIND_LABEL,
  VISIBILITY_LABEL,
  displayLabel,
} from './labels.ts';

describe('rotulos exibidos', () => {
  it('traduz status, papeis, tipos e ações para português', () => {
    assert.equal(displayLabel('active', CIRCLE_STATUS_LABEL), 'Ativo');
    assert.equal(displayLabel('Active', CIRCLE_STATUS_LABEL), 'Ativo');
    assert.equal(displayLabel('editor', ROLE_LABEL), 'Editor');
    assert.equal(displayLabel('post', TARGET_KIND_LABEL), 'Publicação');
    assert.equal(displayLabel('public', VISIBILITY_LABEL), 'Público');
    assert.equal(displayLabel('yearly', FREQUENCY_LABEL), 'anual');
    assert.equal(displayLabel('created', LOG_ACTION_LABEL), 'criado');
    assert.equal(displayLabel('desconhecido', ROLE_LABEL), 'desconhecido');
    assert.equal(displayLabel('', ROLE_LABEL), '');
  });
});
