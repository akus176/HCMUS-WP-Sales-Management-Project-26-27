const toolsDir = process.env.MOBILE_SHELL_VISUAL_TOOLS || '/tmp/mobile-144-visual-tools';
const esbuild = require(toolsDir + '/node_modules/esbuild');
const path = require('path');
const fs = require('fs');
const root = path.resolve(__dirname, '../../..');
const app = root + '/apps/staff-mobile';
fs.mkdirSync(toolsDir + '/site', { recursive: true });
fs.writeFileSync(toolsDir + '/router.js', 'export const router = { push: () => {}, replace: () => {} };');
fs.writeFileSync(toolsDir + '/site/index.html', '<!doctype html><html lang="en"><head><meta name="viewport" content="width=device-width,initial-scale=1"><title>Shell visual fixture</title><style>html,body,#root{margin:0;min-height:100%;height:100%;background:#fff9ed}</style></head><body><div id="root"></div><script src="/bundle.js"></script></body></html>');
const source = `
import React from 'react';
import { createRoot } from 'react-dom/client';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { StaffHome } from '${app}/src/shell/StaffHome';
import { ShellPage, ShellHeading, ShellStatus, ShellButton } from '${app}/src/shell/controls';
const controls = new URLSearchParams(location.search).has('controls');
createRoot(document.getElementById('root')).render(<SafeAreaProvider initialMetrics={{ frame: { x: 0, y: 0, width: innerWidth, height: innerHeight }, insets: { top: 0, left: 0, right: 0, bottom: 0 } }}>
{controls ? <ShellPage><ShellHeading>Shell control states</ShellHeading><ShellStatus>Access unavailable. Your current permissions do not allow this module.</ShellStatus><ShellButton label="Preparation" selected onPress={()=>{}}/><ShellButton label="Retry access check" onPress={()=>{}}/><ShellButton label="Unavailable action" disabled onPress={()=>{}}/></ShellPage> : <StaffHome/>}
</SafeAreaProvider>);
`;
esbuild.build({stdin:{contents:source,resolveDir:app,loader:'tsx'},outfile:toolsDir+'/site/bundle.js',bundle:true,platform:'browser',format:'iife',jsx:'automatic',define:{'process.env.NODE_ENV':'"production"',__DEV__:'false'},nodePaths:[app+'/node_modules',toolsDir+'/node_modules'],alias:{'react-native':toolsDir+'/node_modules/react-native-web/dist/index.js','react':app+'/node_modules/react','react-dom':app+'/node_modules/react-dom','expo-router':toolsDir+'/router.js'},resolveExtensions:['.web.tsx','.web.ts','.web.js','.tsx','.ts','.js','.json'],
plugins: [{ name: 'fixture-session', setup(build) {
  build.onResolve({ filter: /session\/context$/ }, () => ({ path: 'fixture-session', namespace: 'fixture' }));
  build.onLoad({ filter: /.*/, namespace: 'fixture' }, () => ({ contents: `export const useSession = () => ({ client: null, state: { status: 'signedIn', user: { fullName: 'Visual fixture — multi-role staff', permissions: ['orders.fulfill', 'discounts.approve', 'payments.record', 'inventory.manage', 'delivery.update', 'users.manage'] } } });` }));
} }],
});
