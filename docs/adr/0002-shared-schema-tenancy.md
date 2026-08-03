# ADR 0002: Multi-tenancy em schema compartilhado

Status: aceito

As tabelas de negócio usam `tenant_id`, índices iniciados pelo tenant e filtros globais do EF Core. A API resolve o tenant pela claim assinada `tenant_id`; requisições não podem escolher o tenant no corpo. PostgreSQL RLS será a segunda barreira antes do primeiro ambiente compartilhado.
