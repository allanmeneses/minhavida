import { Redirect } from 'expo-router';
import { useAuth } from '@/context/AuthContext';
import { LoadingScreen } from '@/components/LoadingScreen';

export default function Index() {
  const { loading, user } = useAuth();
  if (loading) {
    return <LoadingScreen label="Abrindo o Circulos..." />;
  }
  return <Redirect href={user ? '/(app)/profile' : '/(auth)/login'} />;
}
