import { Alert, Pressable, StyleSheet, Text } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { useSession } from '../src/session/context';

export default function SessionScreen() {
  const { client, state } = useSession();
  if (!state.user) return null;
  return (
    <SafeAreaView style={styles.page}>
      <Text accessibilityRole="header" style={styles.title}>
        Signed in
      </Text>
      <Text style={styles.text}>{state.user.fullName}</Text>
      <Text style={styles.text}>Staff tools are being prepared.</Text>
      <Pressable
        accessibilityRole="button"
        accessibilityLabel="Sign out"
        style={styles.button}
        onPress={() => {
          void client
            ?.logout()
            .catch(() =>
              Alert.alert(
                'Unable to remove the saved session. Please try again.',
              ),
            );
        }}
      >
        <Text style={styles.buttonText}>Sign out</Text>
      </Pressable>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  page: {
    flex: 1,
    justifyContent: 'center',
    backgroundColor: '#fff9ed',
    padding: 24,
    gap: 16,
  },
  title: { color: '#302820', fontSize: 32, fontWeight: '700' },
  text: { color: '#302820', fontSize: 18 },
  button: {
    minHeight: 48,
    padding: 12,
    backgroundColor: '#6d4936',
    borderRadius: 8,
    alignItems: 'center',
  },
  buttonText: { color: '#fff9ed', fontSize: 18 },
});
