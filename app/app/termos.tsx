import { ScrollView, Text } from 'react-native';
import { useRouter, type Href } from 'expo-router';
import { Button } from '@/components/ui/Button';
import { useTheme } from '@/context/ThemeContext';
import { screenStyles } from '@/lib/screenStyles';

export default function TermsScreen() {
  const { colors } = useTheme();
  const styles = screenStyles(colors);
  const router = useRouter();
  return (
    <ScrollView contentContainerStyle={styles.page}>
      <Text style={styles.kicker}>Círculos</Text>
      <Text style={styles.title}>Termos de uso</Text>
      <Text style={styles.lead}>Última atualização: 22 de novembro de 2026.</Text>
      <Text style={styles.lead}>
        Responsável: ALLAN WANDERSON MENESES DE SOUSA. Estes termos descrevem o que o aplicativo Círculos
        faz hoje. Não há promessa de recurso que ainda não existe no código.
      </Text>
      <Text style={styles.section}>1. O serviço</Text>
      <Text style={styles.lead}>
        O Círculos é um hub da vida pessoal. Você cria uma conta com e-mail e senha, guarda um perfil solo,
        documentos, fotos, áreas de vida, círculos, lembretes e atalhos. O produto funciona no aplicativo
        (aplicativo) e no navegador. Não há loja de pagamentos ligada nesta versão: o plano gratuito tem limites
        (3 círculos, 5 atalhos e 100 MB). O plano pago usa Mercado Pago em ambiente de teste, com preços em reais
        configuráveis. Sem chave de produção, a cobrança fica desligada.
      </Text>
      <Text style={styles.section}>2. Cadastro e acesso</Text>
      <Text style={styles.lead}>
        O cadastro é livre com e-mail, senha e nome. A autenticação usa sessão renovada. Recuperação de senha é por
        e-mail. Não há segundo fator nesta versão. Você é responsável por manter a senha em segredo. Para
        excluir a conta, o app pede a senha e confirma por e-mail.
      </Text>
      <Text style={styles.section}>3. Círculos e conteúdo</Text>
      <Text style={styles.lead}>
        Você pode criar círculos e convidar pessoas. Círculos públicos aceitam entrada direta. Círculos
        privados pedem convite ou pedido aceito por um administrador. O dono pode transferir o círculo em
        vida. Histórico e permissões ficam com o círculo. Entrada apagada deixa rastro de quem apagou e
        quando. Sair de um círculo não deixa rastro de participação para os outros. Listas de compras,
        desejos, materiais ou outras ficam no perfil (privadas) ou no círculo (compartilhadas). O criador
        da lista escolhe o que os membros podem fazer e pode mudar isso depois.
      </Text>
      <Text style={styles.section}>4. Lembretes e atalhos</Text>
      <Text style={styles.lead}>
        Lembretes e atalhos são da sua conta. Um atalho dispara na hora, sem confirmação, e pode marcar um
        lembrete de círculo como feito. Som e vibração no atalho são um bip padrão e uma vibração curta.
        O modo férias silencia lembretes pelo prazo que você escolher.
      </Text>
      <Text style={styles.section}>5. Conduta</Text>
      <Text style={styles.lead}>
        Não use o Círculos para conteúdo ilegal. O app ainda não tem canal de denúncia interna nem equipe
        de moderação automática. O responsável pode remover conta ou círculo se houver abuso relatado por
        e-mail.
      </Text>
      <Text style={styles.section}>6. Disponibilidade</Text>
      <Text style={styles.lead}>
        O serviço é oferecido como está. Não há página pública de status nem prazo de suporte. O contato
        é só por e-mail, sem prazo prometido de resposta.
      </Text>
      <Text style={styles.section}>7. Alterações</Text>
      <Text style={styles.lead}>
        Estes termos podem mudar quando o app mudar. A versão vigente é a publicada nesta página.
      </Text>
      <Button label="Política de privacidade" variant="ghost" onPress={() => router.push('/privacidade' as Href)} />
      <Button label="Voltar" variant="ghost" onPress={() => router.back()} />
    </ScrollView>
  );
}
