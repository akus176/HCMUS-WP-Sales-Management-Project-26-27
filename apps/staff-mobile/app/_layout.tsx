import { Stack } from 'expo-router';
import { useFonts } from 'expo-font';
import { StatusBar } from 'expo-status-bar';
import { Text } from 'react-native';

import { ConfigurationGate } from '../src/config/ConfigurationGate';
import { ConnectivityProvider } from '../src/network/ConnectivityContext';
import { SessionProvider, useSession } from '../src/session/context';

export default function RootLayout() {
  return (
    <ConfigurationGate>
      <ConnectivityProvider>
        <SessionProvider>
          <SessionRoutes />
        </SessionProvider>
      </ConnectivityProvider>
    </ConfigurationGate>
  );
}

export function SessionRoutes() {
  const { state } = useSession();
  const [fontsLoaded, fontError] = useFonts({
    Kalam: require('../assets/fonts/Kalam-Bold.ttf'),
    PatrickHand: require('../assets/fonts/PatrickHand-Regular.ttf'),
  });
  if (!fontsLoaded && !fontError)
    return <Text accessibilityRole="text">Loading interface…</Text>;
  if (state.status === 'restoring')
    return <Text accessibilityRole="alert">Restoring session…</Text>;
  return (
    <>
      <StatusBar style="dark" />
      <Stack screenOptions={{ headerShown: false }}>
        <Stack.Protected guard={!state.user}>
          <Stack.Screen name="index" />
          <Stack.Screen name="login" />
        </Stack.Protected>
        <Stack.Protected guard={!!state.user}>
          <Stack.Screen name="session" />
          <Stack.Screen name="modules/[module]" />
          <Stack.Screen name="modules/approvals/[discountId]" />
        </Stack.Protected>
      </Stack>
    </>
  );
}
