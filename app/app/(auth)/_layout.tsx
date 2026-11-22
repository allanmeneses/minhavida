import { Redirect, Stack } from 'expo-router';
import { useAuth } from '@/context/AuthContext';
import { useTheme } from '@/context/ThemeContext';
import { LoadingScreen } from '@/components/LoadingScreen';

export default function AuthLayout() {
  const { loading, user } = useAuth();
  const { colors } = useTheme();
  if (loading) {
    return <LoadingScreen />;
  }
  if (user) {
    return <Redirect href="/(app)/home" />;
  }

  return (
    <Stack
      screenOptions={{
        headerShown: false,
        contentStyle: { backgroundColor: colors.bg },
      }}
    />
  );
}
