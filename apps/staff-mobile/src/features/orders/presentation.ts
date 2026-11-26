import { StyleSheet } from 'react-native';
import { recordRow } from '../../shell/recordPresentation';

export const money = (value: number | string) =>
  Number(value).toLocaleString('en-US', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });
export const orderStyles = StyleSheet.create({ card: recordRow });
