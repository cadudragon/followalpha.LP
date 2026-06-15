# FSD_FollowAlphaLP_v1.1 — Documento de Especificação Funcional

| | |
|---|---|
| Sistema | FollowAlpha.LP |
| Versão | 1.1 (aprovada pelo principal em 2026-06-12, com modificações de alinhamento) |
| Data | 2026-06-12 |
| Autor | Arquiteto/Analista (Claude), para validação do principal (Carlos) |
| Documentos relacionados | `LP-KNOWLEDGE.md` (domínio), `docs/ARCHITECTURE.md` (técnico), `docs/IMPLEMENTATION-PLAN.md` (fases) |

Hierarquia em caso de conflito: regras de domínio/pesquisa (`LP-KNOWLEDGE.md`) > este FSD > conveniência de implementação. Mudanças neste FSD geram nova versão (v1.1, v2.0...), nunca edição silenciosa.

---

## 1. Visão Geral e Escopo

### 1.1 O que o sistema é

O FollowAlpha.LP é uma **ferramenta de apoio à decisão para provisão de liquidez concentrada** (Uniswap v3 e forks, Arbitrum e Base no dia 1). Ela transforma decisões de LP que hoje são tomadas "no olho" em decisões tomadas com números, e mantém um registro auditável de cada decisão recomendada.

O usuário é um operador de LP (hoje: um único usuário, o principal). A pergunta central que o sistema responde, em quatro formas, por prioridade de produto:

1. **"Qual pool/range faz sentido considerar agora, e por quê?"** (Range Advisor: regime, pools, IV vs RV, sobrevivência de bandas, fee APR, IL)
2. **"O histórico sustenta esta largura de range / pool / fee tier?"** (Replay descritivo, sem otimização)
3. **"Esta estratégia de canal sobrevive, incluindo os breakouts?"** (Simulador de canal)
4. **"Meu histórico de LP teve edge?"** (Auditoria/calibração posterior)

### 1.2 O que o sistema NÃO é (fora de escopo, por decisão)

- **Não executa transações.** Nunca. Sem chaves privadas, sem assinatura, sem wallet-connect no servidor. A ferramenta recomenda; o humano executa na sua própria carteira.
- **Não prevê direção de preço.** Bull/bear não existe como output do sistema (família falseada em pesquisa anterior). Só regime de volatilidade.
- **Não promete retorno.** Expectativa congelada da pesquisa: famílias de 10-30% a.a. sobre capital alocado em LP, com gestão de risco por protocolo.
- **Não é multi-usuário hoje.** Desenhado para não impedir SaaS futuro, mas sem login/billing/tenancy real nesta versão.
- **Não toca o capital "cofre"** (carteira passiva do principal, documentada no projeto FollowAlpha.Lean).

### 1.3 Critério de sucesso do produto

1. O Range Advisor ajuda o principal a comparar ativos/pools/ranges com evidência que ele não obteria de forma confiável olhando apenas a UI do DEX: regime, IV vs RV, volume/TVL, liquidez concorrente, sobrevivência histórica de bandas, fee APR honesta e IL esperado.
2. Toda abertura de pool do principal passa a ser precedida de um veredito registrado.
3. O relatório de auditoria, quando executado, confronta o histórico do principal contra alternativas honestas e calibra o sistema.
4. Após ~6 meses de uso, o decision log permite julgar se os vereditos da ferramenta tiveram edge (a ferramenta audita a si mesma).

---

## 2. Conceitos e vocabulário funcional

- **Posição**: uma range de liquidez [Pa, Pb] num pool, com liquidez L, aberta numa data.
- **Intenção** (obrigatória na criação; reclassificável apenas com trilha completa — RN-01):
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

### UC-01 — Auditar carteira (Módulo 0, calibração posterior)

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

1. Usuário chega com ativo e pool pré-selecionados vindos do funil (UC-02 → pools do ativo) — ou seleciona um pool diretamente por endereço —, informa capital opcional e **declara a intenção** (obrigatória).
2. Sistema pode sugerir **range candidates** por uma grade determinística pré-declarada (ex.: larguras fixas e placements coerentes com a intenção), ranqueadas por evidência: IV vs RV, sobrevivência da banda, fee APR, IL esperado, liquidez concorrente e compatibilidade com a intenção. Isto não é otimização retrospectiva.
3. Usuário escolhe uma candidata ou define manualmente a range [Pa, Pb].
4. Sistema computa e exibe:
   - IV do pool (quanto de vol o pool está pagando) vs previsão de vol realizada — o sinal caro/barato;
   - fee APR esperada na range (volume recente, liquidez concorrente na faixa, fee tier);
   - distribuição empírica de tempo-até-sair da range no regime atual (mediana, quartis);
   - IL esperado nos cenários de saída (por cima / por baixo), traduzido pela intenção;
   - **expectancy líquida e o veredito: OPEN / DON'T OPEN**.
5. Sistema grava o veredito + todos os inputs + hash no decision log (sempre, mesmo se o usuário não abrir a posição).
6. Usuário decide e executa fora do sistema, se quiser.

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

### UC-07 — Alertas (adicionado pelo principal, 2026-06-12)

1. Usuário define regras de alerta: preço aproximando-se da borda de uma range aberta (distância configurável), mudança de regime de um ativo da watchlist, IV de um pool da watchlist cruzando um limiar definido.
2. O Collector avalia as regras a cada ciclo e dispara notificações pelo canal configurado (mecanismo a definir na implementação: Telegram, e-mail ou push).
3. Alertas **informam**; nunca executam nada (RN-05) e nunca recomendam direção (RN-07/RN-13).

### UC-08 — Monitor pós-OPEN (adicionado pelo principal, 2026-06-12)

1. Para cada posição aberta, o sistema acompanha: fees acumuladas vs IL acumulado (a corrida que decide o resultado), distância do preço às bordas da range, e os inputs do veredito original (IV, regime, sobrevivência) recalculados com dados atuais.
2. Quando o quadro atual diverge materialmente do quadro do veredito (ex.: regime mudou, IV caiu abaixo da vol prevista), a posição é sinalizada: **"as premissas do OPEN mudaram"** — com o quê mudou, lado a lado.
3. A sinalização é informativa — fechar/manter é decisão do usuário; se tomada, pode ser registrada como anotação datada no decision log (RN-03).

### UC-09 — Validação histórica / Replay (adicionado 2026-06-14; promovido em 2026-06-15)

Propósito: **calibrar e validar os inputs** que alimentam o verdict, com replay determinístico LP-native sobre dados históricos. NÃO é uma tela de "minha regra ganha" — é análise descritiva de mecanismo. Pela decisão de produto de 2026-06-15, o replay descritivo é parte do primeiro valor do Range Advisor: antes de auditar o histórico pessoal, o sistema deve ajudar a responder se uma largura de range, pool e fee tier fazem sentido historicamente.

Escopo permitido (descritivo — categoria A):
1. **Sobrevivência de range**: distribuição empírica de tempo-dentro de bandas de largura W por regime ("±10% em ETH neste regime durou mediana N dias, quartis A/B").
2. **Relação IV vs RV**: quando o pool pagou vol cara vs RV, qual foi o resultado realizado/simulado posterior (estatística descritiva, não otimização).
3. **Reconciliação fee APR**: estimada vs realizada/simulada, com sensibilidade a janela de volume (7d/30d), largura de range e fee tier.
4. **Channel simulator (UC-04)**: série completa incluindo breakouts.

Fora de escopo da v1 (categoria B — avaliação de edge do verdict): "OPEN teria batido DON'T OPEN no histórico?" como prova de que a regra ganha **não entra na v1**. Medir edge do verdict exige pré-registro + walk-forward + out-of-sample (lição dos Programas 1-2 do projeto-mãe); a forma sancionada de responder isso é o decision log se auto-auditando em **dados novos** (forward-tracking), não backtest in-sample. O replay nunca ajusta limiares do verdict contra resultados históricos (RN-14).

---

## 4. Regras de Negócio

| # | Regra |
|---|---|
| RN-01 | Intenção é obrigatória na criação. Reclassificação posterior é permitida **somente com trilha completa**: a intenção original permanece registrada (append-only), a reclassificação carrega data e justificativa, o P&L passa a ser exibido contra os benchmarks de **ambas** as intenções, e a posição é sinalizada como reclassificada em todos os relatórios. (Decisão do principal, 2026-06-12.) |
| RN-02 | Nenhum veredito sem inputs completos. Dados insuficientes → recusa explicada, nunca estimativa silenciosa. |
| RN-03 | Decision log é **append-only**: vereditos nunca são editados ou apagados; cada entrada carrega hash do conteúdo. É permitido **adicionar anotações datadas** a uma entrada (ex.: "não abri porque X"); anotações também são append-only e nunca alteram o registro original. (Decisão do principal, 2026-06-12.) |
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
| RN-14 | Todo cálculo que alimenta o verdict deve ser validável por replay histórico determinístico ou fixture; sem dados suficientes, o sistema sinaliza a limitação em vez de inferir edge. **Cláusula anti-overfitting (decisão do analista, 2026-06-14):** replay serve para calibrar/validar distribuições de input e reconciliar estimativas contra resultados realizados — **nunca** para ajustar limiares do verdict contra resultados históricos. Sem otimização automática, sem busca de parâmetros, sem genetic search. Avaliação de edge do verdict, se um dia feita, segue **análise cega (blind analysis)**: desenho do estudo congelado antes de tocar resultados, julgamento só em out-of-sample/dados novos, walk-forward — nunca in-sample (ver LP-KNOWLEDGE §6.1). |

---

## 5. Requisitos de Interface (UI/UX)

Interface alvo: web (Next.js) consumindo a API; antes do GO de valor, API/CLI cobrem as Fases 1-4 em modo headless. Princípio geral de UX: **números antes de rótulos, inputs sempre visíveis, nada de "confie em mim"**.

### Tela 1 — Dashboard & Monitor pós-OPEN (UC-08)
- Posições abertas: valor, **fees acumuladas vs IL acumulado**, distância do preço às bordas da range, e o status das premissas do veredito original (mantidas / mudaram — com o quê mudou).
- Regimes dos ativos da watchlist, saúde do coletor, últimos vereditos, alertas recentes.

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
Carteiras, watchlist, intents pendentes de declaração, **regras de alerta e canal de notificação (UC-07)**, status de coleta. Sem campos de chave privada — por desenho, não por esquecimento.

### Tela 9 — Validação histórica / Replay (UC-09)
Headless/API-first antes do value gate; tela web somente após GO. Saídas: curvas de sobrevivência de banda por regime, relação IV-vs-RV (descritiva), reconciliação fee APR estimada-vs-realizada, sensibilidade 7d/30d. Exibe explicitamente "calibração de inputs, não prova de edge" e sinaliza quando faltam dados (RN-14). Nunca mostra "OPEN ganhou X% no histórico" como prova de edge.

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
| Principal / Product Owner | Carlos | **aprovado em 2026-06-12 (v1.1)** |
| Arquiteto / Analista | Claude | autor, 2026-06-12 |

Decisões de alinhamento que produziram a v1.1 (todas do principal, 2026-06-12):

1. Fluxo primário **ativo-primeiro** (funil Momento → Pools → Range/intent → Verdict); UC-02 reescrito como porta de entrada; telas renumeradas.
2. Vocabulário de domínio em inglês (range, intents ACCUMULATE/DISTRIBUTE/HARVEST, verdict OPEN/DON'T OPEN).
3. RN-01 relaxada: reclassificação de intenção permitida com trilha completa e duplo benchmark.
4. RN-03 estendida: anotações datadas append-only sobre vereditos.
5. UC-07 (Alertas) e UC-08 (Monitor pós-OPEN) adicionados; Tela 1 vira Dashboard & Monitor.
6. Atualização de prioridade (2026-06-15): o primeiro critério de valor passa a ser Range Advisor + replay descritivo para aconselhar pools/ranges antes do LP-Audit. LP-Audit permanece no escopo, mas como calibração e auditoria posterior.
