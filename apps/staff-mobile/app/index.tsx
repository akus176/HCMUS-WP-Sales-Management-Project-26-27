import { StyleSheet, Text, View } from 'react-native';
import { ShellPage } from '../src/shell/controls';
import { Link } from 'expo-router';

export default function StarterScreen() {
  return (
    <ShellPage>
      <View style={styles.content}>
        <Text style={styles.eyebrow}>COFFEE SHOP</Text>
        <Text accessibilityRole="header" style={styles.title}>
          Staff mobile
        </Text>
        <Text style={styles.description}>Staff tools are being prepared.</Text>
        <Text style={styles.note}>
          No staff account or shop data is loaded.
        </Text>
        <Link href="/login" accessibilityRole="link" style={styles.description}>
          Staff sign-in
        </Link>
      </View>
    </ShellPage>
  );
}

const styles = StyleSheet.create({
  page: { flex: 1, backgroundColor: '#fff9ed' },
  content: {
    flex: 1,
    justifyContent: 'center',
    alignSelf: 'center',
    width: '100%',
    maxWidth: 600,
    padding: 24,
    gap: 16,
  },
  eyebrow: { color: '#6d4936', fontSize: 14, letterSpacing: 2 },
  title: { color: '#302820', fontSize: 36, fontWeight: '700' },
  description: {
    color: '#302820',
    fontSize: 20,
    minHeight: 48,
    paddingVertical: 8,
  },
  note: { color: '#5e5248', fontSize: 16 },
});
