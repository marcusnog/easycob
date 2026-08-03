# ADR 0001: Monólito modular

Status: aceito

O backend começa como API e worker .NET compartilhando o projeto `EasyCob.Core` e um PostgreSQL. Os módulos mantêm entidades próprias e se relacionam somente por identificadores. Serviços separados serão extraídos apenas quando escala ou propriedade justificar o custo operacional.
