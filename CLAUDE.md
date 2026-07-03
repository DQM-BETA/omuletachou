# omuletachou

Sistema de Automacao de Afiliado - omuletachou.com.br

## Stack
- Backend API: ASP.NET Core 8.0
- Jobs: Hangfire + Hangfire.PostgreSql 1.8+
- ORM: Entity Framework Core 8.0
- Banco: PostgreSQL 16
- IA: Anthropic Claude API (claude-haiku-4-5-20251001) via Anthropic.SDK
- Dashboard (admin): Angular 17+
- Site publico: Next.js 14+ (SSR + PWA)
- Containers: Docker + Docker Compose 24+
- Servidor: Oracle Cloud Free Tier, VM ARM Ampere A1, Ubuntu 22.04

## Branches
- main: producao (protegida, exige PR)
- homolog: homologacao (protegida, exige PR)
- desenv: desenvolvimento continuo
- feature/ISSUE-NNN-descricao: branches de trabalho (base: desenv)

## Convencoes
- Commit: feat(ISSUE-NNN): descricao
- Merge feature->desenv: squash
- Merge desenv->homolog e homolog->main: merge commit (NUNCA squash)

## Paths relevantes
- docs_path: documentacoes/ISSUE-NNN-titulo/
- openspec_path: openspec/changes/ISSUE-NNN-titulo/