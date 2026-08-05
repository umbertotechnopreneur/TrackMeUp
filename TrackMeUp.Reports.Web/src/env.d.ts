/// <reference types="vite/client" />

interface TrackMeUpWebViewBridge {
  addEventListener(type: 'message', listener: (event: MessageEvent<unknown>) => void): void
  removeEventListener(type: 'message', listener: (event: MessageEvent<unknown>) => void): void
  postMessage(message:
    | { type: 'report.ready' }
    | { type: 'report.theme.set'; theme: 'system' | 'light' | 'dark' }): void
}

interface Window {
  chrome?: {
    webview?: TrackMeUpWebViewBridge
  }
}
