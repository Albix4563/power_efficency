import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

function source(path) {
    return readFileSync(new URL('../' + path, import.meta.url), 'utf8');
}

const settings = source('src/VoltManager/wwwroot/js/settings.js');
const app = source('src/VoltManager/wwwroot/js/app.js');
const i18n = source('src/VoltManager/wwwroot/js/i18n.js');
const bridge = source('src/VoltManager/Bridge/HostBridge.cs');
const reorganization = source('src/VoltManager/wwwroot/js/ui-reorganization.js');
const reorganizationLayout = source('src/VoltManager/wwwroot/js/ui-reorganization.layout.js');

test('legacy Settings auto-shutdown UI stays removed while current scheduling remains wired', () => {
    assert.doesNotMatch(settings, /auto-shutdown-panel|normalizeAutoShutdownSettings|mountAutoShutdownUi|wireAutoShutdownUi/);
    assert.doesNotMatch(app, /removeLegacyAutoShutdownPanel/);
    assert.doesNotMatch(i18n, /set_pref_autoshutdown/);
    assert.match(app, /Host\.call\('schedulePowerAction'/);
    assert.match(app, /Host\.call\('getScheduledPowerAction'/);
});

test('frontend-orphaned bridge RPC cases stay removed', () => {
    for (const method of ['setActivePlan', 'setPreviewUpdates', 'refreshAppPowerProfiles']) {
        assert.doesNotMatch(bridge, new RegExp(`case "${method}"`));
    }
});

test('reorganized sidebar uses its hidden legacy route sentinel without a redundant mutation observer', () => {
    assert.match(reorganizationLayout, /<li class="hidden" aria-hidden="true"><a data-view="system"/);
    assert.doesNotMatch(reorganization, /suppressLegacySystemItem/);
});
