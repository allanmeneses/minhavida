import { useState } from 'react';
import { Text, View } from 'react-native';
import { Button } from '@/components/ui/Button';
import { useTheme } from '@/context/ThemeContext';
import { isLast, nextIndex, TOUR_SCREENS } from '@/lib/tour';
import { screenStyles } from '@/lib/screenStyles';

type Props = {
  onSkip: () => void;
  onFinish: () => void;
};

export function TourGuide({ onSkip, onFinish }: Props) {
  const { colors } = useTheme();
  const styles = screenStyles(colors);
  const [index, setIndex] = useState(0);
  const screen = TOUR_SCREENS[index];

  return (
    <View style={styles.page}>
      <Text style={styles.kicker}>Primeiro acesso</Text>
      <Text style={styles.title}>{screen.title}</Text>
      <Text style={styles.lead}>{screen.body}</Text>
      <Text style={styles.lead}>
        {index + 1} de {TOUR_SCREENS.length}
      </Text>
      <Button
        label={isLast(index) ? 'Concluir tour' : 'Avancar'}
        onPress={() => {
          if (isLast(index)) {
            onFinish();
            return;
          }
          setIndex((current) => nextIndex(current));
        }}
      />
      <Button label="Pular tour" variant="ghost" onPress={onSkip} />
    </View>
  );
}
