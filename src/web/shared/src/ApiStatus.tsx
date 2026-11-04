import type { ApiHealthState } from './useApiHealth';

export interface ApiStatusProps {
  state: ApiHealthState;
  labels: Record<ApiHealthState, string>;
}

export function ApiStatus({ state, labels }: ApiStatusProps) {
  return (
    <p role="status" data-state={state}>
      {labels[state]}
    </p>
  );
}
