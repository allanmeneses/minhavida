import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { AuthProvider } from '@/context/AuthContext';
import { OfflineProvider } from '@/context/OfflineContext';
import { ThemeProvider, useTheme } from '@/context/ThemeContext';
import { TourProvider } from '@/context/TourContext';

function ThemedStack() {
  const { colors, resolved } = useTheme();
  return (
    <>
      <StatusBar style={resolved === 'dark' ? 'light' : 'dark'} />
      <Stack
        screenOptions={{
          headerShown: false,
          contentStyle: { backgroundColor: colors.bg },
        }}
      />
    </>
  );
}

export default function RootLayout() {
  return (
    <ThemeProvider>
      <AuthProvider>
        <OfflineProvider>
          <TourProvider>
            <ThemedStack />
          </TourProvider>
        </OfflineProvider>
      </AuthProvider>
    </ThemeProvider>
  );
}
