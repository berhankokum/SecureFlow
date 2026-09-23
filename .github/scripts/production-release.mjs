// Built-in Node APIs only. No production credentials or deployment execution here.
import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { pathToFileURL } from 'node:url';
import { resolve } from 'node:path';

export const images = Object.freeze({
  api: 'secureflow-api:phase27',
  edge: 'secureflow-edge:phase27',
  postgres: 'secureflow-postgres:17-patched-20260919',
});
const digestPattern = /^sha256:[a-f0-9]{64}$/;
const shaPattern = /^[a-f0-9]{40}$/;
const docker = (...args) => execFileSync('docker', args, { encoding: 'utf8', stdio: ['ignore', 'pipe', 'inherit'] }).trim();
const hash = (data) => createHash('sha256').update(data).digest('hex');
const inspect = (tag) => JSON.parse(docker('image', 'inspect', tag))[0];
const requireValue = (condition, message) => { if (!condition) throw new Error(message); };

export function registryRoot(repository) {
  requireValue(typeof repository === 'string' && /^[a-zA-Z0-9][a-zA-Z0-9-]*\/[a-zA-Z0-9][a-zA-Z0-9._-]*$/.test(repository), 'Invalid repository');
  return `ghcr.io/${repository.toLowerCase()}`;
}

export function validateSnapshot(snapshot, commit, composeBytes) {
  requireValue(shaPattern.test(commit) && snapshot.commit === commit, 'Commit mismatch');
  requireValue(snapshot.schema === 1 && snapshot.composeSha256 === hash(composeBytes), 'Compose mismatch');
  requireValue(Object.keys(snapshot.images).sort().join() === Object.keys(images).sort().join(), 'Image set mismatch');
  for (const [service, tag] of Object.entries(images)) {
    const image = snapshot.images[service];
    requireValue(image.tag === tag && digestPattern.test(image.id), 'Invalid image identity');
    requireValue(image.os === 'linux' && image.architecture === 'amd64', 'Only verified linux/amd64 supported');
  }
  return snapshot;
}

export function releaseOverride(refs, repository) {
  const root = registryRoot(repository);
  requireValue(Object.keys(refs).sort().join() === Object.keys(images).sort().join(), 'Incomplete release');
  return 'services:\n' + Object.keys(images).map(service => {
    const prefix = `${root}/${service}@`;
    requireValue(typeof refs[service] === 'string' && refs[service].startsWith(prefix)
      && digestPattern.test(refs[service].slice(prefix.length)), 'Invalid registry digest');
    return `  ${service}:\n    image: ${refs[service]}\n    build: !reset null\n`;
  }).join('');
}

export function requirePublishContext(env) {
  requireValue(env.GITHUB_ACTIONS === 'true' && env.GITHUB_EVENT_NAME === 'push'
    && env.GITHUB_REF === 'refs/heads/master' && env.ENABLE_GHCR_PUBLISH === 'true', 'Publication is restricted to opted-in master push CI');
  requireValue(shaPattern.test(env.GITHUB_SHA ?? ''), 'Invalid commit');
  registryRoot(env.GITHUB_REPOSITORY);
}

export function validateReleaseBundle(record, commit, compose, override, repository) {
  validateSnapshot(record, commit, compose);
  requireValue(record.repository === repository, 'Release repository mismatch');
  const expected = releaseOverride(record.refs, repository);
  requireValue(override.toString() === expected && record.overrideSha256 === hash(expected), 'Release override mismatch');
  return record;
}

function main(command, directory) {
  requireValue(['snapshot', 'verify', 'publish', 'check-release'].includes(command) && directory, 'Expected snapshot|verify|publish|check-release DIRECTORY');
  if (command === 'check-release') {
    // The operator supplies expected commit/repository from the trusted successful CI run.
    validateReleaseBundle(JSON.parse(readFileSync(resolve(directory, 'release.json'), 'utf8')),
      process.env.GITHUB_SHA ?? '', readFileSync(resolve(directory, 'compose.production.yml')),
      readFileSync(resolve(directory, 'compose.release.yml')), process.env.GITHUB_REPOSITORY);
    console.log('Release input integrity verified; this is not a signature or a deployment.');
    return;
  }
  const compose = readFileSync('compose.production.yml');
  const file = resolve(directory, 'images.json');
  const commit = process.env.GITHUB_SHA ?? '';
  requireValue(shaPattern.test(commit), 'GITHUB_SHA must be a full commit SHA');
  if (command === 'snapshot') {
    const snapshot = { schema: 1, commit, composeSha256: hash(compose), images: {} };
    for (const [service, tag] of Object.entries(images)) {
      const image = inspect(tag);
      snapshot.images[service] = { tag, id: image.Id, os: image.Os, architecture: image.Architecture };
    }
    validateSnapshot(snapshot, commit, compose);
    mkdirSync(directory, { recursive: true });
    writeFileSync(file, JSON.stringify(snapshot, null, 2) + '\n');
    return;
  }
  const snapshot = validateSnapshot(JSON.parse(readFileSync(file, 'utf8')), commit, compose);
  for (const [service, tag] of Object.entries(images))
    requireValue(inspect(tag).Id === snapshot.images[service].id, `Transferred image differs: ${service}`);
  if (command === 'verify') return;
  requirePublishContext(process.env);
  const root = registryRoot(process.env.GITHUB_REPOSITORY);
  const refs = {};
  for (const [service, tag] of Object.entries(images)) {
    const repository = `${root}/${service}`;
    const target = `${repository}:sha-${commit}`;
    docker('tag', tag, target);
    docker('push', target);
    const candidates = inspect(target).RepoDigests.filter(value => value.startsWith(`${repository}@`));
    requireValue(candidates.length === 1, 'Missing or ambiguous published digest');
    refs[service] = candidates[0];
  }
  // Write a usable release only after ALL pushes and immutable identities succeed.
  const override = releaseOverride(refs, process.env.GITHUB_REPOSITORY);
  writeFileSync(resolve(directory, 'compose.release.yml'), override);
  writeFileSync(resolve(directory, 'compose.production.yml'), compose);
  writeFileSync(resolve(directory, 'release.json'), JSON.stringify({
    ...snapshot, repository: process.env.GITHUB_REPOSITORY, refs,
    overrideSha256: hash(override), runId: process.env.GITHUB_RUN_ID,
    runAttempt: process.env.GITHUB_RUN_ATTEMPT,
  }, null, 2) + '\n');
  console.log('Release references prepared. No production deployment performed.');
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href)
  main(process.argv[2], process.argv[3]);
