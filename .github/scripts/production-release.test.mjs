import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { images, registryRoot, validateSnapshot, releaseOverride, requirePublishContext, validateReleaseBundle } from './production-release.mjs';

const commit = 'a'.repeat(40);
const digest = 'sha256:' + 'b'.repeat(64);
const compose = Buffer.from('standalone production compose');
const snapshot = () => ({ schema: 1, commit, composeSha256: createHash('sha256').update(compose).digest('hex'),
  images: Object.fromEntries(Object.entries(images).map(([service, tag]) => [service, { tag, id: digest, os: 'linux', architecture: 'amd64' }])) });
const context = () => ({ GITHUB_ACTIONS: 'true', GITHUB_EVENT_NAME: 'push', GITHUB_REF: 'refs/heads/master',
  ENABLE_GHCR_PUBLISH: 'true', GITHUB_SHA: commit, GITHUB_REPOSITORY: 'Owner/SecureFlow' });

test('normalizes valid GHCR namespace', () => assert.equal(registryRoot('Owner/SecureFlow'), 'ghcr.io/owner/secureflow'));
test('rejects namespace injection', () => {
  for (const value of ['../repo', 'owner/repo/extra', 'owner/repo\n', 'x;id/repo', '', undefined])
    assert.throws(() => registryRoot(value));
});
test('accepts exact commit, compose and image identities', () => assert.equal(validateSnapshot(snapshot(), commit, compose).schema, 1));
test('rejects commit and compose substitution', () => {
  assert.throws(() => validateSnapshot(snapshot(), 'c'.repeat(40), compose));
  assert.throws(() => validateSnapshot(snapshot(), commit, Buffer.from('changed')));
});
test('rejects missing, foreign and malformed images', () => {
  for (const mutate of [s => delete s.images.api, s => { s.images.other = s.images.api; },
    s => { s.images.api.id = 'latest'; }, s => { s.images.api.tag = 'foreign'; },
    s => { s.images.api.architecture = 'arm64'; }, s => { s.schema = 2; }]) {
    const value = snapshot(); mutate(value); assert.throws(() => validateSnapshot(value, commit, compose));
  }
});
test('release removes build and uses only exact registry digests', () => {
  const refs = Object.fromEntries(Object.keys(images).map(service => [service, `ghcr.io/owner/secureflow/${service}@${digest}`]));
  const yaml = releaseOverride(refs, 'Owner/SecureFlow');
  assert.equal((yaml.match(/build: !reset null/g) ?? []).length, 3);
  assert.ok(!yaml.includes(':latest'));
  for (const invalid of ['ghcr.io/evil/api@' + digest, 'ghcr.io/owner/secureflow/api:latest', refs.api + '\n'])
    assert.throws(() => releaseOverride({ ...refs, api: invalid }, 'Owner/SecureFlow'));
  delete refs.postgres;
  assert.throws(() => releaseOverride(refs, 'Owner/SecureFlow'));
});
test('only opted-in trusted master push can publish', () => {
  assert.doesNotThrow(() => requirePublishContext(context()));
  for (const [key, value] of Object.entries({ GITHUB_ACTIONS: 'false', GITHUB_EVENT_NAME: 'pull_request',
    GITHUB_REF: 'refs/heads/feature', ENABLE_GHCR_PUBLISH: 'false', GITHUB_SHA: 'short', GITHUB_REPOSITORY: '../repo' }))
    assert.throws(() => requirePublishContext({ ...context(), [key]: value }));
});

test('deployment bundle rejects modified override, commit, repository or compose', () => {
  const repository = 'Owner/SecureFlow';
  const refs = Object.fromEntries(Object.keys(images).map(service => [service, `ghcr.io/owner/secureflow/${service}@${digest}`]));
  const override = releaseOverride(refs, repository);
  const record = { ...snapshot(), repository, refs, overrideSha256: createHash('sha256').update(override).digest('hex') };
  assert.doesNotThrow(() => validateReleaseBundle(record, commit, compose, override, repository));
  assert.throws(() => validateReleaseBundle(record, commit, compose, override + '\n', repository));
  assert.throws(() => validateReleaseBundle(record, commit, compose, override, 'Other/Repo'));
  assert.throws(() => validateReleaseBundle(record, 'c'.repeat(40), compose, override, repository));
  assert.throws(() => validateReleaseBundle(record, commit, Buffer.from('changed'), override, repository));
});

test('Compose release merge removes all builds, pins all runtimes and retains private topology', () => {
  const refs = Object.fromEntries(Object.keys(images).map(service => [service, `ghcr.io/owner/secureflow/${service}@${digest}`]));
  // config only: no daemon, registry, real secrets or deployment. Explicit env file avoids .env.
  const config = JSON.parse(execFileSync('docker', ['compose', '--env-file', 'infra/production/compose.env.example',
    '-f', 'compose.production.yml', '-f', '-', 'config', '--format', 'json'], {
    input: releaseOverride(refs, 'Owner/SecureFlow'), encoding: 'utf8',
    env: { ...process.env, PRODUCTION_HOST: 'app.example.com', PRODUCTION_SECRETS_DIR: process.cwd() + '/artifacts/nonexistent-release-test',
      PRODUCTION_BIND_ADDRESS: '127.0.0.1', PRODUCTION_HTTP_PORT: '80', PRODUCTION_HTTPS_PORT: '443',
      PRODUCTION_PROXY_SUBNET: '172.30.27.0/29', PRODUCTION_PROXY_IP: '172.30.27.2', PRODUCTION_API_IP: '172.30.27.3' },
  }));
  for (const [name, service] of Object.entries(config.services)) {
    assert.equal(service.build, undefined, `${name} still builds on deployment host`);
    assert.match(service.image, /@sha256:[a-f0-9]{64}$/);
    if (name !== 'edge') assert.equal(service.ports, undefined);
  }
  assert.equal(config.services.api.read_only, true);
  assert.equal(config.services.edge.read_only, true);
  assert.equal(config.networks.data.internal, true);
  assert.ok(config.services.api.volumes.some(volume => volume.source === 'keys'));
});
