import { StyleSheet } from 'react-native';
import { recordRow } from '../../shell/recordPresentation';

export const money = (value: number | string) => Number(value).toFixed(2);
export const orderStyles = StyleSheet.create({ card: recordRow });
