import { ScrollViewStyleReset } from 'expo-router/html';
import type { ReactNode } from 'react';

export default function Root({ children }: { children: ReactNode }) {
  return (
    <html lang="pt-BR">
      <head>
        <meta charSet="utf-8" />
        <meta httpEquiv="X-UA-Compatible" content="IE=edge" />
        <meta name="viewport" content="width=device-width, initial-scale=1, shrink-to-fit=no" />
        <title>Círculos</title>
        <ScrollViewStyleReset />
        <style dangerouslySetInnerHTML={{ __html: webCss }} />
      </head>
      <body>{children}</body>
    </html>
  );
}

const webCss = `
html, body, #root {
  height: 100%;
}
html {
  color-scheme: light dark;
}
input[type='file'] {
  clip: rect(0 0 0 0);
  clip-path: inset(50%);
  height: 1px;
  width: 1px;
  overflow: hidden;
  position: absolute;
  left: -10000px;
  opacity: 0;
  white-space: nowrap;
}
input[type='file']::-webkit-file-upload-button,
input[type='file']::file-selector-button {
  display: none;
}
html, body, #root, p, span, div, h1, h2, h3, button, label, a {
  overflow-wrap: break-word;
  word-break: normal;
}
body {
  background-color: #F4EFE8;
  font-family: "Segoe UI", "Helvetica Neue", Arial, sans-serif;
}
input:focus, textarea:focus {
  border-color: #1F6F66 !important;
  box-shadow: 0 0 0 3px rgba(31, 111, 102, 0.15);
}
@media (max-width: 767px) {
  input, textarea, button {
    max-width: 100%;
  }
}
`;
