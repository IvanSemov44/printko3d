import { useEffect, useState } from 'react';
import { loadRuntimeConfig } from './config';
import { checkApiHealth } from './health';

export type ApiHealthState = 'checking' | 'available' | 'unavailable';

export function useApiHealth(): ApiHealthState {
  const [state, setState] = useState<ApiHealthState>('checking');

  useEffect(() => {
    const controller = new AbortController();

    async function run() {
      try {
        const config = await loadRuntimeConfig(controller.signal);
        const healthy = await checkApiHealth(config.apiUrl, controller.signal);
        setState(healthy ? 'available' : 'unavailable');
      } catch {
        if (!controller.signal.aborted) {
          setState('unavailable');
        }
      }
    }

    void run();
    return () => {
      controller.abort();
    };
  }, []);

  return state;
}
