# FollowAlpha.LP — Base de Conhecimento

Documento-semente do projeto, escrito em 2026-06-12. Consolida todo o conhecimento de LP (liquidez concentrada) produzido na fase de pesquisa do projeto FollowAlpha.Lean. Serve como onboarding para qualquer pessoa ou agente que trabalhe neste repositório.

---

## 1. De onde este projeto vem (contexto obrigatório)

Este projeto nasce do encerramento de dois programas de pesquisa quantitativa no repositório `FollowAlpha.Lean` (2026), que falsearam com processo limpo — ~280 backtests no engine LEAN self-hosted, walk-forward analysis, holdout preservado, zero erros de processo — a seguinte família de hipóteses:

> **Timing direcional em BTC/ETH/SPY/ouro com barras diárias — simples ou asset-native, parâmetros fixos ou otimizados por WFA — não bateu alocação passiva de risco igualado, líquida de custos.** Quanto mais inteligente o desenho, pior o resultado out-of-sample (degradação IS→OOS de até 10x).

A conclusão estratégica: capital pequeno não tem vantagem em **prever** mercados líquidos; tem vantagem estrutural em **vender serviços** com capacidade limitada — liquidez (LP) e alavancagem (carry/funding). Este projeto ataca o primeiro. O princípio:

> Previsão só paga se existir alguém mais burro do outro lado. Pedágio paga porque alguém **precisa** do serviço: o trader paga fee por liquidez no momento em que quer negociar.

O principal (Carlos) opera pools de liquidez concentrada há mais de um ano — skill operacional, dados proprietários (histórico próprio) e tamanho de capital que cabe em nichos onde market makers profissionais não competem.

## 2. A tese econômica do LP concentrado

LP concentrado (Uniswap v3 e derivados) é, matematicamente, **vender volatilidade e colher fees**: a posição lucra se as fees acumuladas superarem o custo de recomposição do inventário (impermanent loss; na formulação rigorosa, **LVR — loss-versus-rebalancing**).

Fatos fundamentais que governam tudo:

1. **A maioria dos LPs passivos perde dinheiro contra simplesmente segurar os tokens.** Documentado academicamente. O edge não é "abrir pool"; é estar na minoria que mede e gerencia.
2. **"Se um range gera taxas interessantes, eventualmente vamos sair do range"** (verdade aprendida pelo principal, promovida a lei do projeto). Toda estratégia de range é short-vol: pinga-pinga por meses, devolve em movimentos. O protocolo de saída/breakout define a sobrevivência, não o pinga-pinga.
3. **Volatilidade é prevísível; direção não.** Clustering de vol é dos fenômenos mais robustos em finanças; direção foi falseada nos programas anteriores. Toda classificação de regime neste projeto é de VOL (range vs tendência), nunca de direção.
4. **Fees esperadas vs IL esperado é a única conta que importa antes de abrir.** Quem abre pool sem essa conta está abrindo no olho — e o olho perde para a matemática.

## 3. O princípio mestre: contabilidade por intenção

A descoberta conceitual mais importante da fase de desenho. **O impermanent loss só é perda contra um benchmark que tu não querias.** Toda posição declara UMA intenção antes de abrir, e o P&L é medido contra o benchmark da intenção:

| Intenção | Estrutura | Benchmark honesto | Sucesso significa |
|---|---|---|---|
| **ACCUMULATE** | range single-sided **abaixo** do preço | ordem limite de compra escalonada (sem fees) | comprou o ativo desejado mais barato que a ordem seca, + fees |
| **DISTRIBUTE** | range single-sided **acima** do preço | ordem limite de venda escalonada (sem fees) | vendeu mais caro que a ordem seca, + fees |
| **HARVEST** | range two-sided em torno do preço | HODL dos tokens e HODL 50/50 | fees > IL no horizonte realizado |

Insight central: uma range single-sided abaixo do preço É uma **ordem limite que paga** — a ordem limite tradicional espera de graça; a range é remunerada em fees enquanto espera. Se a intenção era acumular BTC na queda, acabar cheio de BTC não é IL: é a ordem executada, com cashback.

**Regra (atualizada por decisão do principal, 2026-06-12): reclassificar a intenção é permitido somente com trilha completa.** A intenção original fica registrada para sempre (append-only), a reclassificação carrega data e justificativa, o P&L passa a ser exibido contra os benchmarks de **ambas** as intenções, e a posição é sinalizada em todos os relatórios. O autoengano ("era HARVEST mas virou ACCUMULATE porque caiu") não é morto pela proibição, mas pela memória: o sistema nunca esquece a história original — tu podes mudar de tese; o registro não muda contigo.

## 4. O rascunho original do principal e as correções aplicadas

O principal pediu uma ferramenta de decisão com 4 capacidades. Parecer do quant analyst, item a item:

1. **"Diga como está o mercado: Bear/Bull/Lateral"** → CORRIGIDO para regime de **volatilidade** (RANGE / TRENDING / TRANSITION). Direção é imprevisível (falseado); vol é tratável. Bull/bear entra apenas como contexto manual da intenção, nunca como previsão automática.
2. **"Leia gráficos e diga se fica num range por X meses"** → CORRIGIDO para **taxas-base empíricas**: ninguém prevê permanência; o que se computa é a distribuição histórica de tempo-até-sair de uma range de largura W, condicionada ao regime de vol. Saída tipo: "range de ±15% em ETH, neste regime: mediana 23 dias dentro, 25% de chance de sair em 1 semana".
3. **"Range com intenção: em baixa, acumular BTC ganhando fees em vez de comprar one-shot"** → ACEITO INTEGRALMENTE; promovido a princípio mestre (Seção 3). A melhor ideia do rascunho.
4. **"Canal: abre na base, fecha quando converte em dólar no topo, repete — máquina de dinheiro"** → ACEITO COM MANUAL DE SEGURANÇA OBRIGATÓRIO. Funciona enquanto o ativo oscila no canal; o breakout para baixo é o modo de morte (reabriu, preço atravessou o fundo, comprou a queda inteira). É short-vol declarado: o protocolo de breakout (Seção 5, Módulo 3) não é opcional.

## 5. Os quatro módulos da ferramenta

### Módulo 0 — LP-Audit (PRIMEIRO entregável; pré-requisito dos demais)

Auditoria do histórico real do principal: para cada posição já operada — fees colhidas, IL realizado, resultado vs HODL, resultado vs benchmark de intenção (reconstruída a posteriori, com honestidade), custos de gas/rebalance. Responde com números: **o LP do último ano gerou edge positivo ou negativo, onde e por quê?** Calibra todos os módulos com dados proprietários, zero contaminação. Entrada necessária: endereços de carteira / lista de posições.

### Módulo 1 — Regime de volatilidade

Por ativo: classificação RANGE / TRENDING / TRANSITION via vol realizada (percentil 30d vs histórico) e trendiness (eficiência de caminho / ADX-like). Simples, testável, alimenta o Módulo 2.

### Módulo 2 — Range Intelligence (o coração)

Para range de largura W em torno do preço, condicionada ao regime:

- distribuição empírica de tempo-até-sair (curvas de sobrevivência da range);
- fee APR esperada dentro da range (volume do pool, TVL concentrado na faixa, fee tier);
- IL esperado nos cenários de saída (por cima / por baixo);
- **veredito: OPEN / DON'T OPEN**, com expectancy líquida = fees esperadas no horizonte provável − custo esperado de saída.

Proibido: saída do tipo "fica neste range por X meses".

### Módulo 3 — Channel Machine com protocolo de breakout

O canal do principal, formalizado. Manual de segurança declarado ANTES da primeira abertura:

- máximo de reaberturas sem travessia completa do canal;
- nível de preço abaixo do qual NÃO se reabre (aceita inventário conforme intenção, ou corta — decidido antes);
- teto de capital por canal (% do capital LP total);
- métrica oficial = série completa incluindo breakouts; nunca "a sequência boa".

Backtest do canal: simulável diretamente com a matemática do AMM (curva de liquidez concentrada + série de preço + custos de gas) — não precisa de engine de mercado tipo LEAN. Walk-forward onde houver parâmetro.

## 6. Disciplina de pesquisa (herdada e inegociável)

Lições pagas com dois programas de pesquisa:

1. **Pré-registro**: métricas, benchmarks e critérios definidos ANTES de olhar resultados. Mudar critério depois de ver resultado = violação de processo.
2. **Benchmarks honestos**: toda medição contra a alternativa que tu realmente terias (HODL, ordem limite, 50/50) — nunca contra strawman.
3. **Sem resgate retrospectivo**: posição/estratégia que falha seu critério não é "ajustada até passar". Um desenho, um julgamento.
4. **Logs append-only commitados em git** (tamper evidence). Sem histórico, sem verdade.
5. **Aporte/yield externo não mascara resultado**: cada medição se sustenta sozinha.
6. **Nenhuma regra de runtime lê o próprio PnL/equity para decidir** (lição do deadlock do drawdown governor do projeto legado): controles reativos ao próprio resultado travam ou explodem; controles devem ser função do estado de mercado.
7. **Separação de papéis**: o analista desenha e julga; o implementador executa mecanicamente; o principal decide capital.
8. **A ferramenta recomenda; o principal executa.** Automação de execução on-chain é decisão futura, com gate próprio.

Base acadêmica de referência: Pardo (walk-forward como único teste válido de otimização), Carver (poucos parâmetros, sistemas simples), López de Prado (a maioria dos backtests positivos são falsos positivos; controlar olhadas na amostra).

## 6b. Fundação matemática adotada (avaliada em 2026-06-12)

Repositório local `C:\Users\carlos.bezerra\Documents\Workspace\uniswap-v3-liquidity-math` (Atis Elsts, implementação de referência da nota técnica "Liquidity Math in Uniswap v3"):

- `uniswap-v3-liquidity-math.py` — kernel da matemática de liquidez concentrada (L de amounts+range, recomposição exata delta_x/delta_y, bounds). Alimenta os Módulos 0, 2 e 3 (é o simulador do canal). Portar com testes unitários contra os casos conhecidos do próprio arquivo.
- `subgraph-liquidity-range-example.py` / `-positions-` — distribuição de liquidez por tick e posições ativas: base do cálculo de fee share (tua fatia = teu L / L total na faixa) e da análise de ranges lotados.
- `subgraph-implied-volatility-example.py` — **a joia**: `IV = 2·fee·sqrt(volume/tickTVL)·sqrt(365)` — quanto de vol o pool está pagando. Upgrade do Módulo 2: o veredito OPEN/DON'T OPEN vira comparação IV do pool vs previsão de vol realizada (vender vol caro = abre; barato = não abre, qualquer que seja o APR anunciado). Backbone teórico: LP ≈ short straddle, fees ≈ prêmio da opção.

Ressalvas registradas: endpoint do hosted service do The Graph foi descontinuado (migrar para o gateway com API key ou Dune); float por simplicidade (analytics ok; audit reconcilia com eventos `collect` on-chain); sem licença visível (uso interno ok; redistribuição → reimplementar).

## 7. Dados necessários

- **Pools**: subgraphs (Uniswap e derivados) / Dune — volume, TVL, fees por pool e por faixa de tick, histórico.
- **Preços/vol**: séries spot diárias e intraday (pipelines já dominados no projeto-mãe: Coinbase API com User-Agent, Yahoo v8; congelar e hashear o que virar input de medição).
- **Posições do principal**: endereços de carteira → eventos de mint/burn/collect das posições (entrada do Módulo 0).
- **Custos**: gas histórico da chain relevante, slippage de recomposição.

## 8. Expectativas congeladas

- Família de **10–30% a.a. sobre o capital alocado em LP**, com trabalho real e risco de cauda gerenciado por protocolo. Qualquer promessa acima disso é ilegítima.
- O capital do cofre (carteira passiva documentada em `FollowAlpha.Lean/docs/portfolio/politica-base.md`) é **separado e intocável** por este projeto. O cofre não financia experimento.
- O projeto-mãe `FollowAlpha.Lean` fica em modo manutenção: tracking ao vivo da C002 (revisão 2027-07) e trilha de auditoria dos programas encerrados.

## 9. Sequência de entrega

1. **Módulo 0 — LP-Audit** ← começa aqui; bloqueado aguardando endereços de carteira do principal.
2. Módulo 1 — regime de vol.
3. Módulo 2 — range intelligence (OPEN/DON'T OPEN).
4. Módulo 3 — channel machine.

## 10. Glossário mínimo

- **Liquidez concentrada**: LP aloca capital num intervalo de preço [A, B]; dentro dele, atua como market maker e colhe fees; fora dele, o capital vira 100% um dos dois ativos e para de render.
- **IL (impermanent loss)**: perda da posição LP vs simplesmente segurar os tokens, causada pela recomposição automática do inventário conforme o preço se move.
- **LVR (loss-versus-rebalancing)**: formulação rigorosa do custo do LP — o que se perde por negociar sempre "atrasado" contra arbitradores informados. Fees precisam superar LVR.
- **Single-sided**: range inteiramente acima ou abaixo do preço atual; funciona como ordem limite escalonada (e remunerada) de venda ou compra.
- **Fee tier**: percentual de fee do pool (ex.: 0,05%, 0,3%, 1%) cobrado dos traders e distribuído aos LPs ativos na faixa.
- **Breakout**: saída do preço do range/canal; o evento que converte lucro acumulado de short-vol em perda se não houver protocolo.
- **Toxic flow**: fluxo de traders informados (arbitragem) que negociam contra o LP exatamente quando o preço se move; a parte do volume que custa em vez de pagar.

---

*Documento de origem: discussões registradas em `FollowAlpha.Lean` (charter formal: `docs/research/program3-lp-decision-engine-charter.md`). Este arquivo é autocontido de propósito: o repositório novo não depende do antigo.*
