import { Stack } from 'expo-router';
import { useFonts } from 'expo-font';
import { StatusBar } from 'expo-status-bar';
import { Text } from 'react-native';

import { ConfigurationGate } from '../src/config/ConfigurationGate';
import { ConnectivityBanner } from '../src/connectivity/ConnectivityBanner';
import { ConnectivityProvider } from '../src/connectivity/connectivity';
import { MobileNotifications } from '../src/notifications/MobileNotifications';
import { OperationalUpdates } from '../src/realtime/OperationalUpdates';
import { SessionProvider, useSession } from '../src/session/context';

export default function RootLayout() {
  return (
    <ConfigurationGate>
      <SessionProvider>
        <SessionRoutes />
      </SessionProvider>
    </ConfigurationGate>
  );
}

export function SessionRoutes() {
  const { state } = useSession();
  return (
    <ConnectivityProvider>
      <OperationalUpdates />
      <SessionNavigation state={state} />
    </ConnectivityProvider>
  );
}

function SessionNavigation({
  state,
}: {
  state: ReturnType<typeof useSession>['state'];
}) {
  const [fontsLoaded, fontError] = useFonts({
    Kalam: require('../assets/fonts/Kalam-Bold.ttf'),
    PatrickHand: require('../assets/fonts/PatrickHand-Regular.ttf'),
  });
  if (!fontsLoaded && !fontError)
    return (
      <>
        <StatusBar style="dark" />
        <ConnectivityBanner />
        <Text accessibilityRole="text">Loading interface…</Text>
      </>
    );
  if (state.status === 'restoring')
    return (
      <>
        <StatusBar style="dark" />
        <ConnectivityBanner />
        <Text accessibilityRole="alert">Restoring session…</Text>
      </>
    );
  return (
    <>
      <StatusBar style="dark" />
      <ConnectivityBanner />
      <Stack screenOptions={{ headerShown: false }}>
        <Stack.Protected guard={!state.user}>
          <Stack.Screen name="index" />
          <Stack.Screen name="login" />
        </Stack.Protected>
        <Stack.Protected guard={!!state.user}>
          <Stack.Screen name="session" />
          <Stack.Screen name="modules/[module]" />
          <Stack.Screen name="modules/delivery/[orderId]" />
          <Stack.Screen name="modules/approvals/[discountId]" />
          <Stack.Screen name="modules/approvals/operations" />
          <Stack.Screen name="notifications/[kind]/[recordId]" />
        </Stack.Protected>
      </Stack>
      <MobileNotifications />
    </>
  );
}
