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
      <Text style={styles.kicker}>Circulos</Text>
      <Text style={styles.title}>Termos de uso</Text>
      <Text style={styles.lead}>Ultima atualizacao: 22 de novembro de 2026.</Text>
      <Text style={styles.lead}>
        Responsavel: ALLAN WANDERSON MENESES DE SOUSA. Estes termos descrevem o que o aplicativo Circulos
        faz hoje. Nao ha promessa de recurso que ainda nao existe no codigo.
      </Text>
      <Text style={styles.section}>1. O servico</Text>
      <Text style={styles.lead}>
        O Circulos e um hub da vida pessoal. Voce cria uma conta com e-mail e senha, guarda um perfil solo,
        documentos, fotos, areas de vida, circulos, lembretes e atalhos. O produto funciona no aplicativo
        (Expo) e no navegador. Nao ha loja de pagamentos ligada nesta versao: o plano gratuito tem limites
        (3 circulos, 5 atalhos e 100 MB). O plano pago usa Mercado Pago em sandbox, com precos em reais
        configuraveis. Sem secret, a cobranca fica desligada.
      </Text>
      <Text style={styles.section}>2. Cadastro e acesso</Text>
      <Text style={styles.lead}>
        O cadastro e livre com e-mail, senha e nome. A autenticacao usa JWT. Recuperacao de senha e por
        e-mail. Nao ha segundo fator nesta versao. Voce e responsavel por manter a senha em segredo. Para
        excluir a conta, o app pede a senha e confirma por e-mail.
      </Text>
      <Text style={styles.section}>3. Circulos e conteudo</Text>
      <Text style={styles.lead}>
        Voce pode criar circulos e convidar pessoas. Circulos publicos aceitam entrada direta. Circulos
        privados pedem convite ou pedido aceito por um administrador. O dono pode transferir o circulo em
        vida. Historico e permissoes ficam com o circulo. Entrada apagada deixa rastro de quem apagou e
        quando. Sair de um circulo nao deixa rastro de participacao para os outros. Listas de compras,
        desejos, materiais ou outras ficam no perfil (privadas) ou no circulo (compartilhadas). O criador
        da lista escolhe o que os membros podem fazer e pode mudar isso depois.
      </Text>
      <Text style={styles.section}>4. Lembretes e atalhos</Text>
      <Text style={styles.lead}>
        Lembretes e atalhos sao da sua conta. Um atalho dispara na hora, sem confirmacao, e pode marcar um
        lembrete de circulo como feito. Som e vibracao no atalho sao um bip padrao e uma vibracao curta.
        O modo ferias silencia lembretes pelo prazo que voce escolher.
      </Text>
      <Text style={styles.section}>5. Conduta</Text>
      <Text style={styles.lead}>
        Nao use o Circulos para conteudo ilegal. O app ainda nao tem canal de denuncia interna nem equipe
        de moderacao automatica. O responsavel pode remover conta ou circulo se houver abuso relatado por
        e-mail.
      </Text>
      <Text style={styles.section}>6. Disponibilidade</Text>
      <Text style={styles.lead}>
        O servico e oferecido como esta. Nao ha pagina publica de status nem prazo de suporte. O contato
        e so por e-mail, sem prazo prometido de resposta.
      </Text>
      <Text style={styles.section}>7. Alteracoes</Text>
      <Text style={styles.lead}>
        Estes termos podem mudar quando o app mudar. A versao vigente e a publicada nesta pagina.
      </Text>
      <Button label="Politica de privacidade" variant="ghost" onPress={() => router.push('/privacidade' as Href)} />
      <Button label="Voltar" variant="ghost" onPress={() => router.back()} />
    </ScrollView>
  );
}
