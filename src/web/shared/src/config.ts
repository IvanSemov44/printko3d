export interface RuntimeConfig {
  apiUrl: string;
}

function isRuntimeConfig(value: unknown): value is RuntimeConfig {
  return (
    typeof value === 'object' &&
    value !== null &&
    'apiUrl' in value &&
    typeof value.apiUrl === 'string'
  );
}

/** Reads /config.json, written at container start so one build serves every environment. */
export async function loadRuntimeConfig(signal?: AbortSignal): Promise<RuntimeConfig> {
  const response = await fetch('/config.json', { signal });
  if (!response.ok) {
    throw new Error(`Failed to load runtime config: ${String(response.status)}`);
  }
  const body: unknown = await response.json();
  if (!isRuntimeConfig(body)) {
    throw new Error('Runtime config is invalid');
  }
  return { apiUrl: body.apiUrl.replace(/\/+$/, '') };
}
