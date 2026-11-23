import { Alert } from 'react-native';
import { router } from 'expo-router';
import { useSession } from '../session/context';
import type { StaffSession } from '../session/session';
import { DeliveryHome } from '../features/delivery/DeliveryHome';
import {
  ShellButton,
  ShellFeedback,
  ShellHeading,
  ShellPage,
  ShellPageHeader,
  ShellStatus,
  ShellText,
} from './controls';
import { useNativeNavigation } from './NativeNavigation';

export function StaffHome({
  user: verifiedUser,
  embedded = false,
}: { user?: StaffSession; embedded?: boolean } = {}) {
  const { client, state } = useSession();
  const navigation = useNativeNavigation();
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
  const allowed = user.permissions.includes('delivery.update');
  const content = (
    <>
      {navigation && <ShellPageHeader title="Today" />}
      <ShellHeading
        accessibilityLabel="Staff home"
        level={navigation ? 'section' : 'page'}
      >
        {navigation
          ? `Good ${new Date().getHours() < 12 ? 'morning' : new Date().getHours() < 18 ? 'afternoon' : 'evening'}, ${user.fullName.split(' ')[0]}`
          : 'Staff home'}
      </ShellHeading>
      <ShellText>{user.fullName}</ShellText>
      {navigation ? (
        <DeliveryHome user={user} />
      ) : allowed ? (
        <ShellButton
          label="Open Deliveries"
          onPress={() => router.push('/modules/delivery')}
        />
      ) : (
        <ShellFeedback
          kind="warning"
          label="Delivery access is required. This app is for delivery staff. Contact your administrator or sign out from Account."
        />
      )}
      {!navigation && (
        <ShellButton
          label="Sign out"
          variant="text"
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
      )}
    </>
  );
  return embedded ? content : <ShellPage>{content}</ShellPage>;
}
