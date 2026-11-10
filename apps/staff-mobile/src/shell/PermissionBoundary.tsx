import { useEffect, useState, type ReactNode } from 'react';
import { Alert, AppState } from 'react-native';

import { useSession } from '../session/context';
import type { StaffSession } from '../session/session';
import { ShellButton, ShellStatus } from './controls';
import { findModule, permittedActions } from './permissions';

type Check = {
  snapshot: StaffSession;
  moduleId: string;
  refreshKey: number;
  result: StaffSession | 'forbidden' | 'error';
};

export function PermissionBoundary({
  moduleId,
  requiredPermission,
  scope = 'module',
  focused = true,
  refreshKey = 0,
  children,
}: {
  moduleId?: string | string[];
  requiredPermission?: string;
  scope?: 'home' | 'module';
  focused?: boolean;
  refreshKey?: number;
  children: (user: StaffSession) => ReactNode;
}) {
  const { client, state } = useSession();
  const module = findModule(moduleId);
  const snapshot = state.user;
  const accessId = scope === 'home' ? 'home' : module?.id;
  const checkId = requiredPermission
    ? `${accessId}:${requiredPermission}`
    : accessId;
  const [check, setCheck] = useState<Check | null>(null);
  const [retry, setRetry] = useState(0);
  const [active, setActive] = useState(
    AppState.currentState !== 'background' &&
      AppState.currentState !== 'inactive',
  );
  useEffect(() => {
    const subscription = AppState.addEventListener('change', (next) => {
      setActive(next === 'active');
      setCheck(null);
      if (next === 'active') setRetry((value) => value + 1);
    });
    return () => subscription.remove();
  }, []);
  useEffect(() => {
    if (!client || !snapshot || !accessId || !checkId || !active || !focused)
      return;
    let current = true;
    const controller = new AbortController();
    void client.api
      .GET('/api/v1/auth/me', { signal: controller.signal })
      .then(({ data }) => {
        if (!current) return;
        // A malformed or cross-account response cannot grant access.
        const valid =
          data?.id === snapshot.id &&
          Array.isArray(data?.permissions) &&
          data.permissions.every(
            (permission) => typeof permission === 'string',
          );
        setCheck({
          snapshot,
          moduleId: checkId,
          refreshKey,
          result:
            valid &&
            (!requiredPermission ||
              data.permissions.includes(requiredPermission)) &&
            (scope === 'home' ||
              (module && permittedActions(module, data.permissions).length))
              ? data
              : 'forbidden',
        });
      })
      .catch(() => {
        if (current)
          setCheck({
            snapshot,
            moduleId: checkId,
            refreshKey,
            result: 'error',
          });
      });
    return () => {
      current = false;
      controller.abort();
    };
  }, [
    client,
    snapshot,
    module,
    accessId,
    checkId,
    requiredPermission,
    scope,
    focused,
    active,
    retry,
    refreshKey,
  ]);

  // Verification failure must not trap a user with saved credentials.
  const signOut =
    snapshot && client ? (
      <ShellButton
        label="Sign out"
        onPress={() => {
          void client
            .logout()
            .catch(() =>
              Alert.alert(
                'Unable to remove the saved session. Please try again.',
              ),
            );
        }}
      />
    ) : null;
  if (state.status === 'restoring')
    return <ShellStatus>Restoring session…</ShellStatus>;
  if (!snapshot)
    return <ShellStatus>Session ended. Sign in again.</ShellStatus>;
  if (!accessId)
    return (
      <>
        <ShellStatus>
          Access unavailable. This module is not available.
        </ShellStatus>
        {signOut}
      </>
    );
  if (
    !focused ||
    !active ||
    !check ||
    check.snapshot !== snapshot ||
    check.moduleId !== checkId ||
    check.refreshKey !== refreshKey
  )
    return (
      <>
        <ShellStatus>Checking access…</ShellStatus>
        {signOut}
      </>
    );
  if (check.result === 'error')
    return (
      <>
        <ShellStatus>
          Unable to verify access. Reconnect and try again.
        </ShellStatus>
        <ShellButton
          label="Retry access check"
          onPress={() => {
            setCheck(null);
            setRetry((value) => value + 1);
          }}
        />
        {signOut}
      </>
    );
  if (check.result === 'forbidden')
    return (
      <>
        <ShellStatus>
          Access unavailable. Your current permissions do not allow this module.
        </ShellStatus>
        {signOut}
      </>
    );
  return children(check.result);
}
