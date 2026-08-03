# ADR 0003: Outbox para SQS Standard

Status: aceito

O worker lê lotes com `FOR UPDATE SKIP LOCKED`, publica no SQS e marca cada evento na mesma transação de seleção. Uma falha após o envio e antes do commit pode repetir a mensagem; consumidores devem deduplicar pelo atributo `event_id`.
