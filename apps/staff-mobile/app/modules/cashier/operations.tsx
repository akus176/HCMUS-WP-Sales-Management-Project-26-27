import { router } from 'expo-router';
import {
  ShellPage,
  ShellPageHeader,
  ShellStatus,
} from '../../../src/shell/controls';

/** Retired staff-workflow links never load operational data. */
export default function RetiredOperationsScreen() {
  return (
    <ShellPage>
      <ShellPageHeader
        title="Page unavailable"
        backLabel="Back to Today"
        onBack={() => router.replace('/session')}
      />
      <ShellStatus>This page is not available in the delivery app.</ShellStatus>
    </ShellPage>
  );
}
