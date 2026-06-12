# FSD_FollowAlphaLP_v1.0 — Documento de Especificação Funcional

| | |
|---|---|
| Sistema | FollowAlpha.LP |
| Versão | 1.0 (rascunho para alinhamento principal ↔ arquiteto) |
| Data | 2026-06-12 |
| Autor | Arquiteto/Analista (Claude), para validação do principal (Carlos) |
| Documentos relacionados | `LP-KNOWLEDGE.md` (domínio), `docs/ARCHITECTURE.md` (técnico), `docs/IMPLEMENTATION-PLAN.md` (fases) |

Hierarquia em caso de conflito: regras de domínio/pesquisa (`LP-KNOWLEDGE.md`) > este FSD > conveniência de implementação. Mudanças neste FSD geram nova versão (v1.1, v2.0...), nunca edição silenciosa.

---

## 1. Visão Geral e Escopo

### 1.1 O que o sistema é

O FollowAlpha.LP é uma **ferramenta de apoio à decisão para provisão de liquidez concentrada** (Uniswap v3 e forks, Arbitrum e Base no dia 1). Ela transforma decisões de LP que hoje são tomadas "no olho" em decisões tomadas com números, e mantém um registro auditável de cada decisão recomendada.

O usuário é um operador de LP (hoje: um único usuário, o principal). A pergunta central que o sistema responde, em quatro formas:

1. **"Meu histórico de LP teve edge?"** (Auditoria)
2. **"Este ativo está em modo amigável para LP agora?"** (Regime de volatilidade)
3. **"Vale a pena abrir ESTA range NESTE pool com ESTA intenção?"** (Veredito OPEN/DON'T OPEN)
4. **"Esta estratégia de canal sobrevive, incluindo os breakouts?"** (Simulador de canal)

### 1.2 O que o sistema NÃO é (fora de escopo, por decisão)

- **Não executa transações.** Nunca. Sem chaves privadas, sem assinatura, sem wallet-connect no servidor. A ferramenta recomenda; o humano executa na sua própria carteira.
- **Não prevê direção de preço.** Bull/bear não existe como output do sistema (família falseada em pesquisa anterior). Só regime de volatilidade.
- **Não promete retorno.** Expectativa congelada da pesquisa: famílias de 10-30% a.a. sobre capital alocado em LP, com gestão de risco por protocolo.
- **Não é multi-usuário hoje.** Desenhado para não impedir SaaS futuro, mas sem login/billing/tenancy real nesta versão.
- **Não toca o capital "cofre"** (carteira passiva do principal, documentada no projeto FollowAlpha.Lean).

### 1.3 Critério de sucesso do produto

1. O relatório de auditoria responde com números se o ano de LP do principal gerou valor vs alternativas honestas.
2. Toda abertura de pool do principal passa a ser precedida de um veredito registrado.
3. Após ~6 meses de uso, o decision log permite julgar se os vereditos da ferramenta tiveram edge (a ferramenta audita a si mesma).

---

## 2. Conceitos e vocabulário funcional

- **Posição**: uma range de liquidez [Pa, Pb] num pool, com liquidez L, aberta numa data.
- **Intenção** (obrigatória, imutável após criação):
  - `ACCUMULATE` — range single-sided abaixo do preço; equivale a ordem limite de compra escalonada que paga fees.
  - `DISTRIBUTE` — range single-sided acima do preço; ordem limite de venda que paga fees.
  - `HARVEST` — range two-sided; negócio de fees vs IL.
- **Benchmark da intenção**: a alternativa honesta contra a qual a posição é julgada (ordem limite seca / HODL / 50-50).
- **Veredito**: `OPEN` ou `DON'T OPEN`, sempre acompanhado dos inputs completos que o produziram.
- **Regime**: `RANGE` / `TRENDING` / `TRANSITION` (volatilidade, nunca direção).
- **Canal**: política de abrir/fechar/reabrir uma range entre dois níveis, com protocolo de breakout obrigatório.
- **Watchlist**: pools monitorados continuamente pelo coletor.

---

## 3. Casos de Uso e Fluxos

### UC-01 — Auditar carteira (Módulo 0)

**Ator**: principal. **Pré-condição**: carteira registrada (`config/wallets.json`); eventos on-chain sincronizados.

1. Sistema enumera todas as posições históricas e atuais da carteira (eventos mint/burn/collect) em Arbitrum e Base.
2. Usuário declara a intenção de cada posição (uma vez; arquivo/tela de intents). Posições sem intenção declarada são auditadas como `HARVEST` com flag "intenção não declarada".
3. Sistema calcula, por posição: fees coletadas (reconciliadas com eventos `collect`), IL realizado, custos de gas, resultado vs HODL, vs 50-50, vs benchmark da intenção.
4. Sistema produz relatório determinístico (JSON + markdown legível): por posição + agregado, com a resposta "teve edge? onde? por quê?".

**Pós-condição**: relatório versionado; re-execução com mesmos inputs produz saída idêntica.

### UC-02 — Explorar ativo: momento do mercado (Módulo 1) — a porta de entrada

Navegação primária do produto (funil ativo-primeiro, decisão do principal em 2026-06-12): **Momento do ativo → Pools do ativo → Range/intent → Verdict**.

1. Usuário vê a lista de tokens da watchlist (cards: regime atual + vol resumida) e clica num ativo (ex.: ETH).
2. Sistema apresenta a **Asset View**: gráfico de preço com camadas úteis para LP —
   - regime atual e histórico (`RANGE`/`TRENDING`/`TRANSITION`) com evidência numérica (percentis de vol realizada, trendiness, janelas usadas);
   - vol realizada (7/30/90d) vs **IV média paga pelos pools do ativo** — o mercado está pagando caro ou barato pela vol deste ativo?;
   - **range bands empíricas** desenhadas sobre o gráfico: faixas ±X% com a taxa-base de sobrevivência no regime atual ("±10% segurou mediana de 21 dias");
   - níveis estruturais de referência (médias longas, topos/fundos relevantes);
   - indicadores adicionais a definir, sob o critério de admissão da RN-13.
3. Sistema indica **estruturas compatíveis com o momento** (nunca direção): ex. "regime TRENDING com vol expandindo → HARVEST two-sided é hostil; se a tese é querer o ativo mais barato, ACCUMULATE single-sided abaixo é a estrutura coerente".
4. Interessado, o usuário avança para a **sessão de pools do ativo**: tabela comparativa (par, chain, fee tier, volume/TVL, IV de cada pool, liquidez concorrente por faixa, fee APR estimada).
5. Do pool escolhido, segue para o avaliador de range (UC-03) com o contexto pré-carregado.

Regra de exibição: a evidência numérica é sempre mostrada junto de qualquer rótulo — nunca só o rótulo.

### UC-03 — Avaliar uma range (Módulo 2 — o coração do produto)

**Fluxo principal**:

1. Usuário chega com ativo e pool pré-selecionados vindos do funil (UC-02 → pools do ativo) — ou seleciona um pool diretamente por endereço —, define a range [Pa, Pb] e o capital, e **declara a intenção** (obrigatória).
2. Sistema computa e exibe:
   - IV do pool (quanto de vol o pool está pagando) vs previsão de vol realizada — o sinal caro/barato;
   - fee APR esperada na range (volume recente, liquidez concorrente na faixa, fee tier);
   - distribuição empírica de tempo-até-sair da range no regime atual (mediana, quartis);
   - IL esperado nos cenários de saída (por cima / por baixo), traduzido pela intenção;
   - **expectancy líquida e o veredito: OPEN / DON'T OPEN**.
3. Sistema grava o veredito + todos os inputs + hash no decision log (sempre, mesmo se o usuário não abrir a posição).
4. Usuário decide e executa fora do sistema, se quiser.

**Fluxos alternativos**: dados insuficientes (pool sem histórico mínimo) → sistema recusa veredito e diz o que falta (nunca chuta); range incompatível com intenção (ex.: ACCUMULATE com range acima do preço) → erro de validação explicado.

**Exemplo concreto (números ilustrativos)** — ETH a $3.000, pool ETH/USDC 0,3% na Base; usuário propõe range $2.700–$3.300, $10.000, intenção HARVEST:

1. IV do pool (preço da vol que o pool paga): volume $32M/dia ÷ $2M de TVL na faixa ativa → **≈ 46% a.a.**
2. Vol realizada prevista do ETH: **≈ 38% a.a.** → vendendo vol cara (46 > 38). Se fosse o inverso, DON'T OPEN independente do APR exibido pela UI do DEX.
3. Fee APR do usuário na range (capital ÷ liquidez concorrente na faixa × volume): **≈ 31% a.a. enquanto dentro**.
4. Sobrevivência da range ±10% no regime atual (taxa-base histórica, não previsão): **mediana 21 dias; 25% de chance de sair em 7 dias**.
5. Custo esperado de saída: por baixo ≈ 1,5% do capital; por cima ≈ 1,4%.
6. **Expectancy: fees medianas +1,8% vs custo de saída −1,4% → +0,4%/ciclo → OPEN**, com a margem fina exposta na tela.

Veredito + inputs gravados no decision log (mesmo que o usuário não abra). Contraste com a decisão "no olho": o APR da UI do DEX é retrovisor, ignora IL e não condiciona ao regime — os três erros que este fluxo elimina.

A mesma range pode mudar de veredito com outra intenção: como ACCUMULATE (single-sided $2.700–$2.850, só USDC), o benchmark vira a ordem limite seca, e "virar ETH na queda" deixa de ser IL — é a ordem executada com cashback de fees. Por isso a intenção é obrigatória antes do cálculo (RN-01).

### UC-04 — Simular canal (Módulo 3)

1. Usuário define: pool, canal [A, B], capital, e o **protocolo de breakout completo** — máximo de reaberturas, nível de não-reabertura, teto de capital. Sem protocolo completo, o sistema recusa a simulação.
2. Sistema simula sobre histórico: série de eventos (aberturas, conversões, fechamentos, breakouts) + curva de P&L **incluindo breakouts**.
3. Regra de exibição: é proibido exibir "a sequência boa" isolada; a série completa é sempre o resultado oficial.

### UC-05 — Rever decisões (decision log)

1. Usuário lista vereditos passados, filtra por pool/intenção/resultado.
2. Cada entrada mostra: data, inputs completos, veredito, hash.
3. (Backlog) Relatório de forward-test: vereditos vs o que aconteceu depois — a ferramenta julgando a si mesma.

### UC-06 — Gerir watchlist e coleta

1. Usuário adiciona/remove pools da watchlist.
2. Coletor (24/7 no VPS) snapshota: estado do pool, volume diário, distribuição de liquidez por tick; sincroniza eventos das carteiras; atualiza séries de preço.
3. Tela/comando de saúde: última coleta por pool, lacunas, falhas.

---

## 4. Regras de Negócio

| # | Regra |
|---|---|
| RN-01 | Intenção é obrigatória na criação de posição/avaliação e **imutável** depois. Reclassificação retroativa não existe no sistema. |
| RN-02 | Nenhum veredito sem inputs completos. Dados insuficientes → recusa explicada, nunca estimativa silenciosa. |
| RN-03 | Decision log é **append-only**: vereditos nunca são editados ou apagados. Cada entrada carrega hash do conteúdo. |
| RN-04 | Simulação de canal exige protocolo de breakout completo; resultado oficial é sempre a série completa com breakouts. |
| RN-05 | O sistema não contém código de assinatura/execução de transações nem armazena chaves privadas. |
| RN-06 | Fees no audit são reconciliadas com eventos `collect` on-chain; divergências com o cálculo teórico são exibidas, não escondidas. |
| RN-07 | Output de regime é exclusivamente de volatilidade (RANGE/TRENDING/TRANSITION); o sistema nunca emite previsão de direção. |
| RN-08 | Todo número exibido é rastreável aos seus inputs (auditabilidade ponta a ponta). |
| RN-09 | Dados on-chain coletados são fatos imutáveis (append-only); re-ingestão é idempotente. |
| RN-10 | Matemática de liquidez validada contra o oráculo (fixtures dourados); fixtures nunca são editados à mão. |
| RN-11 | Caixa/stables rendem 0% em qualquer cálculo comparativo (yield real é upside, nunca edge modelado). |
| RN-12 | Expectativas exibidas ao usuário seguem o teto congelado da pesquisa (10-30% a.a. em LP); o sistema não exibe projeções acima disso. |
| RN-13 | Indicadores na Asset View só são admitidos se servirem à decisão de LP (volatilidade, comportamento de range, liquidez). Indicadores direcionais clássicos (ex.: RSI) podem aparecer como contexto visual, mas nunca viram sinal de compra/venda nem recomendação de direção. |

---

## 5. Requisitos de Interface (UI/UX)

Interface alvo: web (Next.js) consumindo a API; CLI cobre as Fases 1-3. Princípio geral de UX: **números antes de rótulos, inputs sempre visíveis, nada de "confie em mim"**.

### Tela 1 — Dashboard
Visão geral: posições atuais (valor, fees acumuladas, distância do preço às bordas da range), regimes dos ativos da watchlist, saúde do coletor, últimos vereditos.

### Tela 2 — Watchlist & Asset View (UC-02) — a tela primária de navegação
- Lista de tokens da watchlist (cards: regime atual + vol resumida).
- Ao clicar num ativo: gráfico de preço com as camadas LP — regime na timeline, vol realizada (7/30/90d) vs IV média dos pools do ativo, range bands empíricas (taxa-base de sobrevivência desenhada sobre o gráfico), níveis estruturais — e o painel de **estruturas compatíveis com o momento** (contexto, nunca direção — RN-13).
- CTA: "ver pools deste ativo".

### Tela 3 — Pools do ativo (UC-02, passo 4)
Tabela comparativa dos pools do ativo selecionado: par, chain, fee tier, volume/TVL, IV do pool, liquidez concorrente por faixa, fee APR estimada. Ordenável. CTA por linha: "avaliar range neste pool".

### Tela 4 — Avaliador de Range (UC-03) — onde a decisão é selada
- Chega pré-carregada com o contexto do funil (ativo, pool); aceita também entrada direta.
- Esquerda: inputs (pool, range com seletor visual sobre o gráfico de preço + distribuição de liquidez concorrente, capital, intenção).
- Direita: o veredito **OPEN / DON'T OPEN** em destaque, e logo abaixo, sempre visíveis, os números que o produziram: IV vs vol prevista, fee APR esperada, sobrevivência da range (mediana/quartis), IL por cenário, expectancy líquida.
- Rodapé: "este veredito foi registrado" + link para a entrada no decision log.

### Tela 5 — Simulador de Canal (UC-04)
Gráfico de preço com o canal desenhado e os eventos marcados (aberturas, conversões, breakouts); curva de P&L completa; formulário do protocolo de breakout com campos obrigatórios (a simulação não roda sem eles).

### Tela 6 — Auditoria (UC-01)
Tabela por posição (intenção, fees, IL, vs benchmarks, resultado líquido) + agregado + a resposta destacada: "teve edge?". Exportável (JSON/MD).

### Tela 7 — Decision Log (UC-05)
Lista filtrável; entrada expandida mostra inputs completos e hash. Imutável por construção — sem botões de editar/apagar.

### Tela 8 — Configurações
Carteiras, watchlist, intents pendentes de declaração, status de coleta. Sem campos de chave privada — por desenho, não por esquecimento.

---

## 6. Requisitos não-funcionais (resumo funcionalmente relevante)

- Determinismo: mesmas entradas → mesmas saídas, em qualquer relatório ou veredito.
- Disponibilidade do coletor: 24/7 (dado de tick não coletado é irrecuperável).
- Latência de veredito: segundos, não milissegundos (decisão humana, não HFT).
- Idioma: prosa da UI e dos documentos funcionais em português (pt-BR), mas **vocabulário de domínio sempre em inglês** — range, fees, IL, breakout, single-sided, watchlist; intents `ACCUMULATE`/`DISTRIBUTE`/`HARVEST`; verdict `OPEN`/`DON'T OPEN`; regimes `RANGE`/`TRENDING`/`TRANSITION` (preferência do principal, 2026-06-12). Código e documentos técnicos: inglês.
- Privacidade: dados ficam na infraestrutura do principal; nenhum dado enviado a terceiros além das queries públicas (The Graph, RPC).

---

## 7. Aprovação

| Papel | Nome | Status |
|---|---|---|
| Principal / Product Owner | Carlos | **pendente — este documento existe para validar o alinhamento** |
| Arquiteto / Analista | Claude | autor, 2026-06-12 |

Pontos que o principal deve confirmar ou corrigir para fechar a v1.0 (vira v1.1 com as correções):

1. A pergunta central e os 4 casos de uso cobrem o que tu querias da ferramenta?
2. RN-01 (intenção imutável) e RN-03 (log imutável) estão como tu operas ou apertados demais?
3. A Tela 3 (avaliador de range) é a tela que tu imaginavas como "ferramenta que ajuda a tomar decisões"?
4. Falta algum fluxo do teu dia a dia de LP que não está nos UC-01..06?
