# UX/UI Spec — Tela "Links de Afiliado — Mercado Livre" (Sub-C, Issue #185)

Escopo: layout/composição visual da nova tela do dashboard descrita no contrato funcional de
`especificacao-tecnica.md` §3.6 (Issue #182). Este documento não altera nenhuma decisão de dado/API —
só define como isso aparece na tela.

## 0. Nota sobre design system consultado

- Figma da squad (`https://www.figma.com/design/yi6YkNAy9HfHus2oiPi3G7/Diego-Mulet-s-team-library`)
  consultado via `get_figma_data`: o arquivo está no estado padrão de template do Figma ("Start
  here", "Build your own team library" — `components: {}`, `componentSets: {}`), sem nenhum
  componente customizado da squad publicado ainda. Não há tokens/componentes reais a mapear de lá.
- O `omuletachou` **não usa Tailwind** (sem `tailwind.config.*` no repo) — o dashboard Angular usa
  **Angular Material** como design system real e já validado em produção (precedente
  `dashboard/src/app/pages/facebook-manual/`, citado em `especificacao-tecnica.md`/`tasks.md`: fila
  de itens pendentes de ação manual + `MatSnackBar` de feedback). Esta spec, portanto, amarra os
  componentes a **Angular Material** (M2/M3 theme já em uso no shell do dashboard) em vez de
  Radix/`@radix-ng/primitives` — seguindo o design system efetivamente estabelecido neste projeto,
  não um genérico fora do stack real.
- Este agente não lê código-fonte (`src/`) por escopo de papel — a composição abaixo é derivada do
  contrato funcional (`especificacao-tecnica.md` §3.6) e da descrição textual do precedente
  (`facebook-manual`) presente em `tasks.md`/`especificacao-tecnica.md`, não de inspeção direta do
  HTML/SCSS existente. O Dev Angular deve conferir o HTML/SCSS real de `facebook-manual` para
  reaproveitar classes/mixins de tema já existentes (cores, espaçamento, tipografia) e manter 1:1 a
  identidade visual do restante do dashboard.

## 1. Direção estética e microcopy

Ferramenta operacional interna (não é tela voltada a cliente final) — prioridade é **clareza de
sequência e prevenção de erro de pareamento**, não decoração. Direção:

- **Tom de voz**: direto, em pt-BR, imperativo e específico — nunca "Loading..." genérico. Exemplos
  de microcopy (o Dev pode ajustar palavra a palavra, manter a intenção):
  - Título da página: **"Links de Afiliado — Mercado Livre"**
  - Subtítulo/instrução fixa (abaixo do título, sempre visível): *"Copie as URLs abaixo, cole na
    ferramenta oficial do Mercado Livre (Gerador de produtos recomendados), copie os links gerados de
    volta e cole aqui, na mesma ordem."*
  - Botão de cópia: **"Copiar URLs (N produtos)"** (não "Copy" genérico — expõe a contagem, reforça
    que o operador está copiando um lote específico)
  - Confirmação de cópia: tooltip/label temporário **"Copiado!"** por ~2s no próprio botão (padrão já
    usado em `copyCaption` do `facebook-manual`, reaproveitar a mesma técnica)
  - Placeholder da textarea: *"Cole aqui os links gerados pelo Mercado Livre, um por linha, na mesma
    ordem da lista acima"*
  - Botão de importação: **"Importar links"**
  - Estado vazio: **"Nenhum produto aguardando link de afiliado no momento."** com subtexto *"Assim
    que o coletor de Mercado Livre trouxer novos produtos, eles aparecem aqui."*
- **Numeração visual explícita** ("1." / "2." nos títulos dos dois blocos) — reforça a sequência de
  passos e serve de âncora para o pareamento por ordem, que é a regra de negócio crítica desta tela
  (heurística "combinação sistema/mundo real" — a UI usa a mesma noção de ordem/posição que a
  ferramenta externa do ML usa para gerar os links).
- Evitar qualquer copy tipo "Lorem"/genérica de template — todo texto da tela deve refletir o
  vocabulário do domínio (produto, link de afiliado, Mercado Livre), nunca placeholder de IA.

## 2. Fluxo de navegação

1. Item de navegação no shell (`shell.component.ts`, array `navItems`): rótulo **"Links ML"**, ícone
   Material `link`, rota `/mercadolivre-links` (conforme especificação técnica §3.6 — já decidido, não
   é decisão desta spec).
2. Ao entrar na rota: fetch automático de `GET /api/products?status=AwaitingAffiliateLink&pageSize=200`
   — sem clique adicional do operador.
3. Fluxo principal acontece **em uma única página**, sem stepper/wizard forçado — o operador precisa
   alternar entre esta aba e a aba da ferramenta do Mercado Livre, então a lista de produtos e o campo
   de importação ficam **ambos visíveis o tempo todo** (nunca esconder o Bloco 1 ao preencher o Bloco
   2 — heurística "reconhecimento em vez de recall": o operador não deve precisar memorizar a lista).
4. Após importação (sucesso total ou parcial): a lista recarrega automaticamente; produtos importados
   somem (deixaram de estar `AwaitingAffiliateLink`); a textarea é limpa **apenas em caso de sucesso**
   (import falho preserva o conteúdo colado — ver §4.6).
5. Não há paginação/rolagem infinita nesta tela (contrato já limita a `pageSize=200`, volume operacional
   baixo — até 80 produtos/dia no pior caso).

## 3. Composição visual (wireframe descritivo)

Layout de página única, **duas seções empilhadas verticalmente** (não lado a lado — a lista pode ter
muitas linhas e precisa de largura total para o link original ser legível):

```
┌─────────────────────────────────────────────────────────────────┐
│  Links de Afiliado — Mercado Livre                    [Links ML]│  ← header da página (título +
│  Copie as URLs, gere os links no Mercado Livre e cole de volta. │    subtítulo instrucional fixo
└─────────────────────────────────────────────────────────────────┘

┌─ Card 1 ───────────────────────────────────────────────────────┐
│  1. Produtos pendentes de link de afiliado         [Copiar URLs (8)] │  ← mat-card-header + botão
│  ────────────────────────────────────────────────────────────  │
│  # │ Produto                    │ Categoria   │ Link original   │  ← mat-table
│  1 │ Air fryer 5L Inox           │ Eletrodom.  │ .../p/MLB123 ⧉ │
│  2 │ Fone bluetooth XYZ          │ Eletrônicos │ .../p/MLB456 ⧉ │
│  …                                                              │
└──────────────────────────────────────────────────────────────────┘

┌─ Card 2 ───────────────────────────────────────────────────────┐
│  2. Colar links gerados pelo Mercado Livre                      │  ← mat-card-header
│  ────────────────────────────────────────────────────────────  │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │ [textarea, cole um link por linha, mesma ordem acima]     │  │  ← mat-form-field + textarea
│  │                                                            │  │    (cdkTextareaAutosize)
│  └──────────────────────────────────────────────────────────┘  │
│  6 de 8 links colados — faltam 2                    [Importar] │  ← contador ao vivo + CTA
│  ⓘ Após importar, você pode disparar Jobs → Processor para      │  ← nota textual (link p/ /jobs)
│    publicar imediatamente.                                      │
└──────────────────────────────────────────────────────────────────┘
```

### 3.1 Card 1 — lista de produtos pendentes

- Componente: `mat-card` com `mat-card-header` (título "1. Produtos pendentes de link de afiliado") +
  botão de ação no canto do header, **não no rodapé** (o botão de cópia é a única ação deste bloco,
  fica junto do título por proximidade).
- Corpo: `mat-table` (ou `mat-list` no breakpoint mobile — ver §5), colunas:
  1. **# (índice, 1-based)** — coluna estreita, é a âncora visual do pareamento por ordem.
  2. **Produto** (`title`) — trunca com `text-overflow: ellipsis` + `matTooltip` com o título completo.
  3. **Categoria** (`category`, já vem no `ProductListItemDto`) — `mat-chip` pequeno, contexto extra
     para o operador conferir visualmente contra o que aparece na ferramenta do ML (não é dado
     obrigatório do contrato, é reforço de "prevenção de erro"/reconhecimento — se o Dev preferir
     omitir por simplicidade, tela continua correta; recomendação, não bloqueio).
  4. **Link original** (`sourceUrl`) — fonte monoespaçada, truncada (meio da string, preservando
     início `.../p/` e o ID no fim, que é a parte que muda), com ícone `content_copy` individual por
     linha (conveniência opcional — cópia unitária, além do botão de copiar tudo).
- Botão principal do card: `mat-raised-button color="primary"` com ícone `content_copy` — rótulo
  **"Copiar URLs (N)"**, `N` = contagem de produtos carregados. Ao clicar: `navigator.clipboard
  .writeText(...)` (mesmo padrão de `copyCaption` do `facebook-manual`) com todas as `sourceUrl`,
  uma por linha, na mesma ordem da tabela. Feedback: label do botão muda para **"Copiado!"** por ~2s
  (ícone vira `check`), depois volta ao normal — feedback inline, sem depender só do snackbar (ação
  de clipboard é silenciosa por natureza do browser, então o feedback visual imediato é obrigatório
  — heurística "visibilidade do status do sistema").

### 3.2 Card 2 — importação

- `mat-card` com `mat-card-header` (título "2. Colar links gerados pelo Mercado Livre").
- `mat-form-field appearance="outline"` com `<textarea matInput cdkTextareaAutosize
  cdkAutosizeMinRows="6">`, `placeholder` conforme §1.
- **Contador ao vivo** (`mat-hint` ou linha de texto abaixo do campo, atualizado a cada `input`):
  - Se `linhasColadas.length === produtos.length` (e > 0): texto **verde/`primary`**, ícone `check_circle`
    — *"N de N links colados — pronto para importar."*
  - Se `linhasColadas.length !== produtos.length`: texto **`warn`/vermelho**, ícone `error_outline`
    — *"X de N links colados — faltam/sobram Y."* (mensagem específica, não genérica) — este é o CA de
    prevenção de erro descrito na especificação técnica (bloquear envio em caso de mismatch).
  - Se textarea vazia: sem contador, ou contador neutro "0 de N links colados".
- Botão **"Importar links"** (`mat-raised-button color="primary"`, ícone `publish` ou `upload`):
  - **Disabled** quando: lista de produtos vazia, OU textarea vazia, OU contagem de linhas ≠ contagem
    de produtos, OU importação em andamento.
  - Nunca dispara a chamada HTTP em estado de mismatch — a validação é 100% client-side antes do
    `POST` (consistente com a especificação técnica: "bloquear o envio e avisar o operador").
- Nota textual fixa abaixo do botão (`mat-card` sutil ou simples parágrafo com ícone `info` `mat-icon`
  cor `accent`/cinza): lembrete do `Jobs → Processor`, com link/`routerLink` para `/jobs` (rota já
  existente) — conveniência, não obrigatório clicar para a tela funcionar.

## 4. Estados (todos)

### 4.1 Loading — carregamento inicial da lista
- Card 1: `mat-progress-spinner` centralizado no lugar da tabela (ou 3-5 linhas de skeleton, se o
  projeto já tiver um padrão de skeleton em outra tela — usar o mesmo; senão, spinner simples é
  aceitável para esta tela de baixo volume).
- Card 2: presente mas com textarea `disabled` e botão "Importar" `disabled` até a lista carregar (não
  faz sentido colar links antes de saber quantos produtos existem).
- Texto de apoio: *"Carregando produtos pendentes…"*.

### 4.2 Empty — nenhum produto pendente
- Card 1 substituído por um estado vazio dedicado (mesmo card, corpo diferente): ícone Material
  `task_alt` (ou `inbox`) grande, cinza, centralizado + título *"Nenhum produto aguardando link de
  afiliado no momento."* + subtexto conforme §1.
- Botão "Copiar URLs" não aparece (nada para copiar).
- Card 2 fica oculto ou com um aviso substituto — não há sentido em mostrar a textarea sem produtos
  para parear. Recomendação: ocultar Card 2 inteiro neste estado (reduz ruído — heurística "design
  minimalista").

### 4.3 Default/loaded — lista com itens (estado normal, coberto em detalhe no §3)

### 4.4 Disabled — condições de botões desabilitados
- "Copiar URLs": nunca desabilitado quando há produtos (sempre pelo menos 1 item, dado que só aparece
  nesse estado).
- "Importar links": desabilitado nas 4 condições listadas em §3.2 — o botão em si permanece visível
  (não escondido) mesmo desabilitado, para o operador entender que a ação existe e por que não está
  disponível (o contador ao vivo explica o motivo).
- Textarea: desabilitada durante `loading` inicial e durante `importing` (§4.5) — habilitada nos
  demais estados.

### 4.5 Loading — importação em andamento
- Botão "Importar links": `disabled` + `mat-spinner` (diâmetro ~20px) substituindo o ícone, mantendo o
  texto ou trocando para *"Importando…"*.
- Textarea: `readonly`/`disabled` durante a chamada (evita edição concorrente com a request em voo).
- Card 1: pode permanecer interativo (copiar de novo não é destrutivo), mas não é o foco.

### 4.6 Erro
- **Erro ao carregar a lista** (`GET` falhou): Card 1 mostra estado de erro dedicado — ícone
  `error_outline`, texto *"Não foi possível carregar os produtos pendentes."* + botão **"Tentar
  novamente"** (`mat-stroked-button`) que refaz o fetch. Card 2 permanece oculto/disabled (mesmo
  princípio do estado vazio — não faz sentido sem a lista).
- **Mismatch de contagem** (validação client-side antes do POST): já coberto em §3.2 — mensagem inline
  vermelha junto ao contador, botão "Importar" desabilitado. **Não** é um snackbar (é validação
  contínua enquanto o operador digita, não uma ação pontual que falhou).
- **Erro na chamada de importação** (`POST` falhou, ex. rede/servidor): `MatSnackBar` de erro (cor
  `warn`), mensagem *"Não foi possível importar os links. Tente novamente."* com ação **"Tentar
  novamente"** no próprio snackbar. **Textarea preserva o conteúdo colado** (não limpar em caso de
  falha — heurística "controle e liberdade do usuário": o operador não deve perder o trabalho de colar
  os links por causa de uma falha de rede).

### 4.7 Sucesso
- **Importação 100% bem-sucedida** (`Skipped.length === 0`): `MatSnackBar` (cor padrão/`primary`),
  mensagem *"N produtos importados com sucesso."*, duração ~4s, sem ação adicional necessária. Lista
  recarrega (itens somem); textarea limpa; se a lista ficar vazia, transita para o estado `Empty` (§4.2).
- **Importação parcial** (`Skipped.length > 0`): `MatSnackBar` com mensagem *"N importados, M pulados."*
  e ação **"Ver detalhes"** no snackbar. Ao clicar, abre um `mat-dialog` (ou expande um
  `mat-expansion-panel` inline no Card 2, abaixo do botão — preferível ao dialog por manter contexto
  visível) listando cada item pulado: produto (título, resolvido a partir do `productId` contra a
  lista já carregada em memória) + motivo (`reason` do `AffiliateLinkImportSkip`, texto vindo do
  backend, ex. "Link vazio", "Status atual é X, esperado AwaitingAffiliateLink"). Lista recarrega
  normalmente (os importados somem, os pulados continuam em `AwaitingAffiliateLink` e permanecem na
  tabela do Card 1 para nova tentativa).

### 4.8 Readonly
- Não aplicável a esta tela no contrato atual — não há papéis/permissões diferenciadas nem modo de
  visualização somente-leitura descritos na especificação técnica. O único "somente leitura" de fato é
  o comportamento transitório da textarea durante `importing` (§4.5), já coberto ali.

## 5. Responsividade

Breakpoints seguindo o padrão Angular CDK (`Breakpoints.Handset` / `.Tablet` / `.Web`, já disponível
via `@angular/cdk/layout`, coerente com Angular Material):

- **Desktop (≥ 960px)**: layout de §3 completo — `mat-table` com as 4 colunas (#, Produto, Categoria,
  Link original), textarea com `cdkAutosizeMinRows="6"`.
- **Tablet (600–959px)**: mesma estrutura vertical; coluna **Categoria** oculta da tabela (menos
  crítica que # / Produto / Link); demais colunas mantêm truncamento com tooltip.
- **Mobile (< 600px)**: `mat-table` substituída por `mat-list` — cada produto vira um `mat-list-item`
  de duas linhas: linha 1 = `# · Título` (truncado), linha 2 = link truncado + ícone de copiar
  individual à direita (padrão responsivo recomendado do próprio Angular Material para tabelas
  estreitas). Botão "Copiar URLs" e "Importar links" ocupam largura total (`mat-raised-button` full
  width) para área de toque adequada. Contador ao vivo permanece visível, quebrando em duas linhas se
  necessário.

## 6. Heurísticas de Nielsen → critérios verificáveis (checklist para QA/Dev)

1. **Visibilidade do status do sistema** — todo carregamento (lista, cópia, importação) tem feedback
   visual explícito (spinner/label temporário/snackbar); nenhuma ação fica "muda" mais de ~300ms sem
   indicação.
2. **Correspondência sistema/mundo real** — numeração "1."/"2." nos blocos e coluna "#" na tabela usam
   a mesma noção de ordem que a ferramenta do Mercado Livre usa para gerar os links (linguagem do
   domínio, não jargão técnico de API).
3. **Controle e liberdade do usuário** — falha de importação nunca limpa a textarea; operador pode
   corrigir e reenviar sem redigitar/recolar.
4. **Consistência e padrões** — reaproveita o padrão visual já validado em `facebook-manual`
   (estrutura de card + lista + `MatSnackBar`), mesmos componentes Angular Material do resto do
   dashboard (sem introduzir biblioteca/estilo novo só para esta tela).
5. **Prevenção de erros** — contagem de linhas coladas × produtos exibidos é validada **antes** do
   `POST`, com botão desabilitado em caso de divergência (não deixa o operador descobrir o erro só
   depois de importar errado).
6. **Reconhecimento em vez de memorização** — lista de produtos permanece visível durante o
   preenchimento da textarea (nunca escondida atrás de outra etapa/tela).
7. **Estética e design minimalista** — dois cards, sem elementos decorativos supérfluos; estado vazio
   oculta o card de importação (nada a fazer, nada a mostrar).
8. **Ajuda e documentação** — subtítulo fixo da página e nota textual sobre o `Jobs → Processor`
   removem a necessidade de o operador perguntar "e agora?" depois de importar.

## 7. Referências

- Contrato funcional/de dados: `especificacao-tecnica.md` §3.6 (schema mínimo), §3.5 (DTOs/endpoint de
  importação), `tasks.md` Sub-C.
- Precedente de código (a inspecionar pelo Dev, este agente não leu `src/`):
  `dashboard/src/app/pages/facebook-manual/` (`.component.ts/.html/.scss/.spec.ts`).
- Rota/nav item já decididos na especificação técnica: `/mercadolivre-links`,
  `shell.component.ts` → `navItems`.
