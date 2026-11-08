import {test} from 'node:test';
import assert from 'node:assert/strict';
import {request} from 'node:http';
import {readFile} from 'node:fs/promises';
import vm from 'node:vm';
import {createSiteServer} from '../scripts/serve-site.mjs';

test('static website serves its complete assets and keeps private repository paths inaccessible', async context => {
  const server = await createSiteServer();
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  context.after(() => new Promise(resolve => server.close(resolve)));
  const base = `http://127.0.0.1:${server.address().port}`;
  const response = await fetch(base);
  assert.equal(response.status, 200);
  assert.match(response.headers.get('content-security-policy'), /connect-src 'none'/);
  const html = await response.text();
  assert.match(html, /Legends aren't born/);
  for (const path of [...html.matchAll(/(?:src|href)="\.\/([^"#]+)"/g)].map(match => match[1])) {
    const asset = await fetch(`${base}/${path}`);
    assert.equal(asset.status, 200, path);
    assert.ok(Number(asset.headers.get('content-length')) > 0, path);
  }
  assert.equal((await fetch(`${base}/__health`)).status, 200);
  assert.equal((await fetch(base, {method:'POST', body:'synthetic'})).status, 405);
  for (const path of ['/.git/config','/.local/secrets/credentials.secrets.json','/README.md','/unknown.html','/%2e%2e%2fREADME.md']) {
    const status = await new Promise(resolve => { request(base + path, response => { response.resume(); resolve(response.statusCode); }).end(); });
    assert.ok([403,404].includes(status), path);
  }
  const head = await fetch(`${base}/index.html`, {method:'HEAD'});
  assert.equal(head.status, 200);
  assert.equal(await head.text(), '');
});

test('browser configuration contains only public settings and no default account destination', async () => {
  const sandbox = {window:{}};
  vm.runInNewContext(await readFile(new URL('../website/site/config.js', import.meta.url), 'utf8'), sandbox);
  assert.deepEqual(Object.keys(sandbox.window.REVORA_CONFIG).sort(), ['accountUrl','repositoryUrl']);
  assert.equal(sandbox.window.REVORA_CONFIG.accountUrl, null);
  assert.match(sandbox.window.REVORA_CONFIG.repositoryUrl, /^https:\/\/github\.com\//);
  new vm.Script(await readFile(new URL('../website/site/app.js', import.meta.url), 'utf8'));
});
