export async function checkApiHealth(apiUrl: string, signal?: AbortSignal): Promise<boolean> {
  try {
    const response = await fetch(`${apiUrl}/health`, { signal });
    return response.ok;
  } catch (error) {
    if (signal?.aborted) {
      throw error;
    }
    return false;
  }
}
