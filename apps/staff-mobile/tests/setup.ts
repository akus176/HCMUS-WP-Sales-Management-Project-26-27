jest.mock('@react-native-community/netinfo', () => ({
  __esModule: true,
  default: {
    fetch: jest.fn(async () => ({
      type: 'wifi',
      isConnected: true,
      isInternetReachable: true,
      details: { isConnectionExpensive: false },
    })),
    addEventListener: jest.fn(() => () => {}),
  },
}));
