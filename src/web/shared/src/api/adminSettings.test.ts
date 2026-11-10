import { afterEach, describe, expect, it, vi } from 'vitest';
import { getAdminStoreSettings, getStoreMoneySettings, updateStoreSettings } from './adminSettings';
import { ApiError } from './client';

const DTO = {
  storeName: 'Принтко 3Д',
  defaultCurrency: 'EUR',
  defaultLocale: 'bg',
  contactEmail: 'shop@example.com',
  contactPhone: '',
  logoKey: null,
  primaryColor: '#2f5d8a',
  isDemo: true,
  version: '12',
};

const INPUT = {
  storeName: 'Принтко 3Д',
  defaultCurrency: 'EUR',
  defaultLocale: 'bg',
  contactEmail: 'shop@example.com',
  contactPhone: '',
  primaryColor: '#2f5d8a',
  isDemo: true,
};

function stubFetch(response: Response) {
  const fetchMock = vi.fn<(url: string, init?: RequestInit) => Promise<Response>>(() =>
    Promise.resolve(response),
  );
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

describe('admin store settings API', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('reads the money settings from the public address', async () => {
    const fetchMock = stubFetch(Response.json(DTO));

    const money = await getStoreMoneySettings();

    expect(fetchMock.mock.calls[0]?.[0]).toBe('/api/v1/store-settings');
    expect(money).toEqual({ currency: 'EUR', locale: 'bg' });
  });

  it('reads the settings from the admin address and turns the version into a number', async () => {
    const fetchMock = stubFetch(Response.json(DTO));

    const settings = await getAdminStoreSettings();

    expect(fetchMock.mock.calls[0]?.[0]).toBe('/api/v1/admin/store-settings');
    expect(settings).toEqual({ ...DTO, version: 12 });
  });

  it('saves with PUT, the forgery header and the version the form was loaded with', async () => {
    const fetchMock = stubFetch(Response.json({ ...DTO, version: '13' }));

    const saved = await updateStoreSettings(INPUT, 12);

    const [url, init] = fetchMock.mock.calls[0] ?? [];
    expect(url).toBe('/api/v1/admin/store-settings');
    expect(init?.method).toBe('PUT');
    expect(init?.headers).toMatchObject({ 'X-Requested-With': 'XMLHttpRequest' });
    expect(JSON.parse(typeof init?.body === 'string' ? init.body : 'null')).toEqual({
      ...INPUT,
      version: 12,
    });
    expect(saved.version).toBe(13);
  });

  it('names the rejected fields of a 400 answer', async () => {
    stubFetch(
      Response.json(
        { code: 'validation.failed', errors: { contactEmail: ['x'], primaryColor: ['y'] } },
        { status: 400 },
      ),
    );

    const error = await updateStoreSettings(INPUT, 12).catch((reason: unknown) => reason);

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({
      status: 400,
      code: 'validation.failed',
      fields: ['contactEmail', 'primaryColor'],
    });
  });

  it('keeps the code of a version conflict', async () => {
    stubFetch(Response.json({ code: 'store_settings.version_conflict' }, { status: 409 }));

    const error = await updateStoreSettings(INPUT, 12).catch((reason: unknown) => reason);

    expect(error).toMatchObject({
      status: 409,
      code: 'store_settings.version_conflict',
      fields: [],
    });
  });

  it('refuses an answer without a version', async () => {
    stubFetch(Response.json({ ...DTO, version: undefined }));

    await expect(getAdminStoreSettings()).rejects.toThrow();
  });
});
