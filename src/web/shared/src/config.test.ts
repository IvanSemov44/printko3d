import { afterEach, describe, expect, it, vi } from 'vitest';
import { loadRuntimeConfig } from './config';

function stubConfig(body: unknown, status = 200) {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(Response.json(body, { status })));
}

describe('loadRuntimeConfig', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('reads the API address without trailing slashes', async () => {
    stubConfig({ apiUrl: 'http://api.test//' });

    await expect(loadRuntimeConfig()).resolves.toEqual({
      apiUrl: 'http://api.test',
      imageBaseUrl: null,
      siteUrl: null,
    });
  });

  it('reads the image base address without trailing slashes', async () => {
    stubConfig({ apiUrl: 'http://api.test', imageBaseUrl: 'http://img.test/files/' });

    await expect(loadRuntimeConfig()).resolves.toMatchObject({
      imageBaseUrl: 'http://img.test/files',
    });
  });

  it.each([[''], ['   '], [42], [null]])('treats %j as no image base address', async (value) => {
    stubConfig({ apiUrl: 'http://api.test', imageBaseUrl: value });

    await expect(loadRuntimeConfig()).resolves.toMatchObject({ imageBaseUrl: null });
  });

  it('rejects a config without an API address', async () => {
    stubConfig({ imageBaseUrl: 'http://img.test' });

    await expect(loadRuntimeConfig()).rejects.toThrow('invalid');
  });

  it('rejects when the file cannot be loaded', async () => {
    stubConfig({}, 500);

    await expect(loadRuntimeConfig()).rejects.toThrow('500');
  });
});

describe('loadRuntimeConfig: shop address', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('reads the public address of the shop without trailing slashes', async () => {
    stubConfig({ apiUrl: 'http://api.test', siteUrl: 'https://shop.test/' });

    await expect(loadRuntimeConfig()).resolves.toMatchObject({ siteUrl: 'https://shop.test' });
  });

  it.each([[undefined], [''], ['  '], [7]])('treats %j as no shop address', async (value) => {
    stubConfig({ apiUrl: 'http://api.test', siteUrl: value });

    await expect(loadRuntimeConfig()).resolves.toMatchObject({ siteUrl: null });
  });
});
