import { Alert } from 'react-native';
import { router } from 'expo-router';

import { useSession } from '../session/context';
import type { StaffSession } from '../session/session';
import {
  ShellButton,
  ShellHeading,
  ShellPage,
  ShellStatus,
  ShellText,
} from './controls';
import { permittedModules } from './permissions';

export function StaffHome({
  user: verifiedUser,
}: { user?: StaffSession } = {}) {
  const { client, state } = useSession();
  if (state.status === 'restoring')
    return (
      <ShellPage>
        <ShellStatus>Restoring session…</ShellStatus>
      </ShellPage>
    );
  const user = verifiedUser ?? state.user;
  if (!user)
    return (
      <ShellPage>
        <ShellStatus>Session ended. Sign in again.</ShellStatus>
      </ShellPage>
    );
  const modules = permittedModules(user.permissions);
  return (
    <ShellPage>
      <ShellHeading>Staff home</ShellHeading>
      <ShellText>{user.fullName}</ShellText>
      <ShellText>Choose your workspace.</ShellText>
      {!modules.length && (
        <ShellText>
          No mobile modules are available for your permissions.
        </ShellText>
      )}
      {modules.map((module) => (
        <ShellButton
          key={module.id}
          label={`Open ${module.title}`}
          onPress={() =>
            router.push({
              pathname: '/modules/[module]',
              params: { module: module.id },
            })
          }
        />
      ))}
      <ShellButton
        label="Sign out"
        variant="secondary"
        onPress={() => {
          void client
            ?.logout()
            .catch(() =>
              Alert.alert(
                'Unable to remove the saved session. Please try again.',
              ),
            );
        }}
      />
    </ShellPage>
  );
}
