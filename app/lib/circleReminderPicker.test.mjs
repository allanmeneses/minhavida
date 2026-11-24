import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';
import {
  canCycleCircleReminders,
  circleReminderButtonLabel,
  nextCircleReminderId,
} from './circleReminderPicker.ts';

const root = dirname(fileURLToPath(import.meta.url));
const homeSource = readFileSync(join(root, '../app/(app)/home.tsx'), 'utf8');
const tomei = { id: 'r1', title: 'Tomei', circleId: 'fam' };

describe('ciclo do lembrete no atalho', () => {
  it('clique com a lista vazia não consome o ciclo; o fetch que chega depois habilita o próximo clique', () => {
    let reminderId = null;
    let reminders = [];
    assert.equal(canCycleCircleReminders(reminders), false);
    if (canCycleCircleReminders(reminders)) {
      reminderId = nextCircleReminderId(reminders, reminderId);
    } else {
      reminderId = nextCircleReminderId(reminders, reminderId);
    }
    assert.equal(reminderId, null);
    assert.equal(circleReminderButtonLabel(reminders, reminderId), 'Sem lembrete de círculo');

    reminders = [tomei, { id: 'r0', title: 'Sem círculo', circleId: null }];
    assert.equal(canCycleCircleReminders(reminders), true);
    assert.equal(reminderId, null);

    reminderId = nextCircleReminderId(reminders, reminderId);
    assert.equal(reminderId, 'r1');
    assert.equal(circleReminderButtonLabel(reminders, reminderId), 'Lembrete: Tomei');
  });

  it('rerender só com nova identidade de onPress mantém o nome digitado e o lembrete já escolhido', () => {
    const reminders = [tomei];
    const label = 'Foi feito';
    let reminderId = nextCircleReminderId(reminders, null);
    const onPressA = () => {
      reminderId = nextCircleReminderId(reminders, reminderId);
    };
    const onPressB = () => {
      reminderId = nextCircleReminderId(reminders, reminderId);
    };
    assert.notEqual(onPressA, onPressB);
    assert.equal(label, 'Foi feito');
    assert.equal(circleReminderButtonLabel(reminders, reminderId), 'Lembrete: Tomei');
  });

  it('o botão só cicla depois dos lembretes, buscados junto com o painel', () => {
    const remindersCall = homeSource.indexOf("'/api/reminders'");
    const shortcutsCall = homeSource.indexOf("'/api/shortcuts'");
    const homeCall = homeSource.indexOf("'/api/home'");
    assert.ok(homeCall > 0);
    assert.ok(remindersCall > 0);
    assert.ok(remindersCall < shortcutsCall);
    assert.ok(homeCall < shortcutsCall);
    assert.match(homeSource, /canCycleCircleReminders\(reminders\)/);
    assert.match(homeSource, /disabled=\{!canCycleCircleReminders\(reminders\)\}/);
    assert.match(homeSource, /circleReminderButtonLabel\(reminders, reminderId\)/);
    assert.match(homeSource, /nextCircleReminderId\(reminders, reminderId\)/);
    const loadStart = homeSource.indexOf('const load = useCallback');
    const loadEnd = homeSource.indexOf('}, [token]);', loadStart);
    const loadBody = homeSource.slice(loadStart, loadEnd);
    assert.equal((loadBody.match(/setReminderId/g) ?? []).length, 0);
  });
});
