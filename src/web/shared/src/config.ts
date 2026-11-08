export interface RuntimeConfig {
  apiUrl: string;
  /** Where product images are served from; `null` when the environment has no image storage yet. */
  imageBaseUrl: string | null;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}

function trimTrailingSlashes(address: string): string {
  return address.replace(/\/+$/, '');
}

/** Reads /config.json, written at container start so one build serves every environment. */
export async function loadRuntimeConfig(signal?: AbortSignal): Promise<RuntimeConfig> {
  const response = await fetch('/config.json', { signal });
  if (!response.ok) {
    throw new Error(`Failed to load runtime config: ${String(response.status)}`);
  }
  const body: unknown = await response.json();
  if (!isRecord(body) || typeof body.apiUrl !== 'string') {
    throw new Error('Runtime config is invalid');
  }
  const imageBaseUrl =
    typeof body.imageBaseUrl === 'string' && body.imageBaseUrl.trim() !== ''
      ? trimTrailingSlashes(body.imageBaseUrl.trim())
      : null;
  return { apiUrl: trimTrailingSlashes(body.apiUrl), imageBaseUrl };
}
