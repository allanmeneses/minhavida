import { Redirect, Stack } from 'expo-router';
import { useAuth } from '@/context/AuthContext';
import { LoadingScreen } from '@/components/LoadingScreen';
import { colors } from '@/constants/theme';

export default function AuthLayout() {
  const { loading, user } = useAuth();
  if (loading) {
    return <LoadingScreen />;
  }
  if (user) {
    return <Redirect href="/(app)/profile" />;
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
