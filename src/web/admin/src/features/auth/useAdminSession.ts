import { ApiError, getCurrentAdmin, login, logout, type AdminUser } from '@storefront/shared';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

const SESSION_KEY = ['auth', 'me'] as const;

/** Who is signed in: the answer comes from the API on every load, never from the browser's storage. */
export function useCurrentAdmin() {
  return useQuery({
    queryKey: SESSION_KEY,
    queryFn: ({ signal }) => getCurrentAdmin(signal),
  });
}

export function useLogin() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (credentials: { email: string; password: string }) =>
      login(credentials.email, credentials.password),
    onSuccess: (user: AdminUser) => {
      queryClient.setQueryData(SESSION_KEY, user);
    },
  });
}

export function useLogout() {
  const queryClient = useQueryClient();

  function forgetSession() {
    queryClient.setQueryData(SESSION_KEY, null);
    // Whatever else was loaded for the administrator must not outlive the session.
    queryClient.removeQueries({ predicate: (query) => query.queryKey[0] !== SESSION_KEY[0] });
  }

  return useMutation({
    mutationFn: logout,
    onSuccess: forgetSession,
    onError: (error: Error) => {
      // 401 means the session was already gone, which is what the user wanted.
      if (error instanceof ApiError && error.status === 401) {
        forgetSession();
      }
    },
  });
}
