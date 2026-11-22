import { ScrollViewStyleReset } from 'expo-router/html';
import type { ReactNode } from 'react';

export default function Root({ children }: { children: ReactNode }) {
  return (
    <html lang="pt-BR">
      <head>
        <meta charSet="utf-8" />
        <meta httpEquiv="X-UA-Compatible" content="IE=edge" />
        <meta name="viewport" content="width=device-width, initial-scale=1, shrink-to-fit=no" />
        <title>Circulos</title>
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
body {
  background-color: #F4EFE8;
  font-family: "Segoe UI", "Helvetica Neue", Arial, sans-serif;
}
input:focus, textarea:focus {
  border-color: #1F6F66 !important;
  box-shadow: 0 0 0 3px rgba(31, 111, 102, 0.15);
}
`;
