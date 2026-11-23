import { Activity, useEffect, useState, type ReactNode } from 'react';
import { Alert, AppState } from 'react-native';

import { useSession } from '../session/context';
import type { StaffSession } from '../session/session';
import { ShellButton, ShellPage, ShellStatus } from './controls';
import { findModule, permittedActions } from './permissions';
import { useNativeNavigation } from './NativeNavigation';

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
  fallbackHeader,
  retain = false,
  children,
}: {
  moduleId?: string | string[];
  requiredPermission?: string;
  scope?: 'home' | 'module';
  focused?: boolean;
  refreshKey?: number;
  fallbackHeader?: ReactNode;
  retain?: boolean;
  children: (user: StaffSession) => ReactNode;
}) {
  const { client, state } = useSession();
  const verifyNavigation = useNativeNavigation()?.verify;
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
        // Publish effective navigation grants in the same update as the gate,
        // including a valid response that revokes this particular workspace.
        if (valid) verifyNavigation?.(data);
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
    verifyNavigation,
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
  const fallbackContent = (content: ReactNode) => (
    <>
      {fallbackHeader}
      {content}
    </>
  );
  const fallback = (content: ReactNode) =>
    retain && scope === 'home' ? (
      <ShellPage>{fallbackContent(content)}</ShellPage>
    ) : (
      fallbackContent(content)
    );
  if (state.status === 'restoring')
    return fallback(<ShellStatus>Restoring session…</ShellStatus>);
  if (!snapshot)
    return fallback(<ShellStatus>Session ended. Sign in again.</ShellStatus>);
  if (!accessId)
    return fallback(
      <>
        <ShellStatus>
          Access unavailable. This module is not available.
        </ShellStatus>
        {signOut}
      </>,
    );
  // Hide and suspend retained work while a new focus grant is verified. State
  // survives tab changes; a denied grant or changed identity discards the tree.
  if (
    retain &&
    check?.snapshot === snapshot &&
    check.moduleId === checkId &&
    typeof check.result === 'object'
  ) {
    const ready = focused && active && check.refreshKey === refreshKey;
    return (
      <>
        {!ready &&
          focused &&
          fallback(<ShellStatus>Checking access…</ShellStatus>)}
        <Activity mode={ready ? 'visible' : 'hidden'}>
          {children(check.result)}
        </Activity>
      </>
    );
  }
  if (
    !focused ||
    !active ||
    !check ||
    check.snapshot !== snapshot ||
    check.moduleId !== checkId ||
    check.refreshKey !== refreshKey
  )
    return fallback(
      <>
        <ShellStatus>Checking access…</ShellStatus>
        {signOut}
      </>,
    );
  if (check.result === 'error')
    return fallback(
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
      </>,
    );
  if (check.result === 'forbidden')
    return fallback(
      <>
        <ShellStatus>
          Access unavailable. Your current permissions do not allow this module.
        </ShellStatus>
        {signOut}
      </>,
    );
  return children(check.result);
}
