import { ScrollView, Text } from 'react-native';
import { useRouter, type Href } from 'expo-router';
import { Button } from '@/components/ui/Button';
import { useTheme } from '@/context/ThemeContext';
import { screenStyles } from '@/lib/screenStyles';

export default function PrivacyScreen() {
  const { colors } = useTheme();
  const styles = screenStyles(colors);
  const router = useRouter();
  return (
    <ScrollView contentContainerStyle={styles.page}>
      <Text style={styles.kicker}>Circulos</Text>
      <Text style={styles.title}>Politica de privacidade</Text>
      <Text style={styles.lead}>Ultima atualizacao: 22 de novembro de 2026.</Text>
      <Text style={styles.lead}>
        Controlador: ALLAN WANDERSON MENESES DE SOUSA. Esta politica segue a Lei Geral de Protecao de
        Dados (Lei 13.709/2018) e descreve so o que o Circulos realmente trata hoje.
      </Text>
      <Text style={styles.section}>1. Quais dados</Text>
      <Text style={styles.lead}>
        Conta: e-mail, senha com hash, nome. Perfil: nome de exibicao, data de nascimento, telefone,
        e-mail secundario, foto, se o perfil e publico. Documentos e fotos enviados por voce. Areas de
        vida e entradas (texto, numero, foto, data, local). Circulos, membros, convites, pedidos de
        entrada, publicacoes, log e listas (itens, quem adicionou e quem marcou). Lembretes, atalhos e historico de busca. E-mails de recuperacao de
        senha, convite e exclusao de conta (hoje gravados num outbox local, sem SMTP de producao).
      </Text>
      <Text style={styles.section}>2. Para que</Text>
      <Text style={styles.lead}>
        Executar o servico que voce pediu: autenticar, guardar a vida pessoal, compartilhar no circulo,
        lembrar datas e atalhos, buscar o que voce registrou. Base legal: execucao do contrato (art. 7,
        V) e consentimento geral no cadastro, revogavel pela exclusao da conta (perguntas 61 a 66 da
        ligacao, alinhadas a LGPD).
      </Text>
      <Text style={styles.section}>3. Compartilhamento</Text>
      <Text style={styles.lead}>
        Membros do circulo veem o que o circulo permite (nome, descricao, entradas, log). Circulos
        marcados para busca podem aparecer para outras contas pelo nome e descricao. Nao vendemos dados
        a terceiros. Nao ha anuncios. Nao ha painel de transparencia do que foi coletado (pergunta 69).
      </Text>
      <Text style={styles.section}>4. Seguranca</Text>
      <Text style={styles.lead}>
        Documentos e fotos ficam cifrados em AES-256-GCM no servidor, com chave do app, nao escolhida
        pela pessoa. Foto de entrada e de perfil e comprimida no padrao usado pelo app e substitui o
        original. Download so autenticado. O token JWT fica no aparelho (armazenamento seguro no nativo,
        localStorage na web). Se o app encerrar, nao ha exportacao do material cifrado em formato que
        voce mesmo abra (escolha de proposito).
      </Text>
      <Text style={styles.section}>5. Seus direitos</Text>
      <Text style={styles.lead}>
        Voce pode corrigir o perfil e as entradas. Pode exportar o diario em texto corrido. Pode apagar
        foto, documento, atalho, entrada e a conta. A exclusao da conta pede senha e confirmacao por
        e-mail, apaga o que e so seu e mantem rastro anonimo no circulo (quem apagou some, o fato de
        que existiu um evento pode permanecer). Circulos que voce criou passam a um administrador ou
        sucessor; se nao houver, o circulo trava sem dono.
      </Text>
      <Text style={styles.section}>6. Retencao e busca</Text>
      <Text style={styles.lead}>
        Os dados ficam enquanto a conta existir. O historico de busca e gravado na sua conta para
        sincronizar entre aparelhos e sugerir termos frequentes. O log do circulo e visivel aos membros,
        com data e hora no fuso do aparelho.
      </Text>
      <Text style={styles.section}>7. Contato</Text>
      <Text style={styles.lead}>
        Para exercer direitos de titular, escreva ao responsavel ALLAN WANDERSON MENESES DE SOUSA pelo
        e-mail da conta de cadastro, usando o fluxo de exclusao no app quando quiser apagar a conta.
        Nao ha CNPJ neste documento.
      </Text>
      <Button label="Termos de uso" variant="ghost" onPress={() => router.push('/termos' as Href)} />
      <Button label="Voltar" variant="ghost" onPress={() => router.back()} />
    </ScrollView>
  );
}
