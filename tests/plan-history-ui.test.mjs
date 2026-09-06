import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

function source(path) {
  return readFileSync(new URL('../' + path, import.meta.url), 'utf8');
}

const html = source('src/VoltManager/wwwroot/index.html');
const power = source('src/VoltManager/wwwroot/js/power.js');
const dashboard = source('src/VoltManager/wwwroot/js/dashboard.js');
const bridge = source('src/VoltManager/wwwroot/js/bridge.js');
const i18n = source('src/VoltManager/wwwroot/js/i18n.js');
const reorgLayout = source('src/VoltManager/wwwroot/js/ui-reorganization.layout.js');
const reorgI18n = source('src/VoltManager/wwwroot/js/ui-reorganization.i18n.js');

test('plan history is reachable from the plan reason and both power navigation shells', () => {
  assert.match(html, /id="open-plan-history"/);
  assert.match(html, /data-pm="history"/);
  assert.match(html, /id="plan-history-mount"/);
  assert.match(reorgLayout, /id: 'history', icon: 'history', label: 'tab_plan_history'/);
  assert.match(reorgLayout, /id="vm-power-history"/);
  assert.match(dashboard, /activateSubview\('power-plans', 'history'\)/);
});

test('history filters, 50-row paging and problem semantics are explicit', () => {
  assert.match(power, /visibleCount: 50/);
  assert.match(power, /\['all', 'automatic', 'manual', 'external', 'problems'\]/);
  assert.match(power, /entry\.outcome === 'failed' \|\| entry\.outcome === 'unverifiable'/);
  assert.match(power, /filtered\.slice\(0, planHistoryState\.visibleCount\)/);
  assert.match(power, /planHistoryState\.visibleCount \+= 50/);
});

test('history revision handling rejects stale notifications and stale snapshots', () => {
  assert.match(power, /revision >= planHistoryState\.revision/);
  assert.match(power, /revision <= planHistoryState\.revision\) return/);
  assert.match(power, /planHistoryState\.dirty = true/);
  assert.match(power, /if \(planHistoryVisible\(\)\) loadPlanHistory\(\)/);
});

test('history renders names as text and formats localized dates down to seconds', () => {
  const historyUi = power.slice(power.indexOf('function historyDate'), power.indexOf("Host.call('getSettings')"));
  assert.match(historyUi, /second: '2-digit'/);
  assert.match(historyUi, /new Intl\.NumberFormat\(historyLocale\(\)/);
  assert.match(historyUi, /node\.textContent = String\(textValue\)/);
  assert.doesNotMatch(historyUi, /innerHTML/);
});

test('history exposes empty, filtered-empty, load-error retry and clear states', () => {
  assert.match(power, /state\.textContent = ht\('empty'\)/);
  assert.match(power, /state\.textContent = ht\('noResults'\)/);
  assert.match(power, /state\.textContent = ht\('loadError'\)/);
  assert.match(power, /Host\.call\('clearPlanHistory'\)/);
  assert.match(power, /id = 'plan-history-retry'/);
});

test('history translations cover Italian, English, Spanish and Chinese', () => {
  assert.equal((i18n.match(/"power_group_history"/g) || []).length, 4);
  assert.equal((i18n.match(/"plan_history_link"/g) || []).length, 4);
  assert.equal((reorgI18n.match(/tab_plan_history:/g) || []).length, 4);
  assert.match(power, /it: \{[\s\S]*?note: 'Ultimi 500 eventi della sessione/);
  assert.match(power, /en: \{[\s\S]*?note: 'Last 500 events from this session/);
  assert.match(power, /es: \{[\s\S]*?note: 'Últimos 500 eventos de la sesión/);
  assert.match(power, /zh: \{[\s\S]*?note: '本次会话最近 500 个事件/);
});

test('bridge event subscription supports lifecycle cleanup', () => {
  assert.match(bridge, /return \(\) => \{/);
  assert.match(bridge, /current\.splice\(index, 1\)/);
  assert.match(power, /planHistoryUnsubscribe = Host\.on\('planHistoryChanged'/);
  assert.match(power, /window\.addEventListener\('unload'/);
  assert.match(power, /planHistoryUnsubscribe\(\)/);
});
