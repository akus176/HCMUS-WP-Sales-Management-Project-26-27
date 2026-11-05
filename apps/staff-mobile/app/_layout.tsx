import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { Text } from 'react-native';

import { SessionProvider, useSession } from '../src/session/context';

export default function RootLayout() {
  return (
    <SessionProvider>
      <SessionRoutes />
    </SessionProvider>
  );
}

export function SessionRoutes() {
  const { state } = useSession();
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
        </Stack.Protected>
      </Stack>
    </>
  );
}
