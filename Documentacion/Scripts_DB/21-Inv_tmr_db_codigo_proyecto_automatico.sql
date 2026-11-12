-- ============================================================================
-- SCRIPT: 21-Inv_tmr_db_codigo_proyecto_automatico.sql
-- Propósito: El código de proyecto (PR-0001, PR-0002, ...) deja de escribirse a
-- mano y lo asigna el backend al crear el proyecto (ProyectosEndpoints).
--   1. Crea la secuencia time_report.seq_proyecto_codigo, que continúa desde el
--      número PR-#### más alto existente.
--   2. Crea un índice único para que la base no acepte códigos repetidos, sin
--      distinguir mayúsculas ni espacios. Incluye proyectos inactivos.
-- Si ya hay códigos repetidos el script se detiene y los lista: hay que corregirlos
-- y volver a ejecutarlo. Debe aplicarse ANTES de desplegar el backend que lo usa.
-- Se puede ejecutar más de una vez: la secuencia nunca retrocede.
-- ============================================================================

BEGIN;

CREATE SEQUENCE IF NOT EXISTS time_report.seq_proyecto_codigo;

DO $$
DECLARE
    v_duplicados TEXT;
    v_ultimo_codigo BIGINT;
    v_valor_secuencia BIGINT;
    v_siguiente_base BIGINT;
BEGIN
    SELECT string_agg(r.codigo, ', ' ORDER BY r.codigo)
    INTO v_duplicados
    FROM (
        SELECT upper(trim(p.Codigo)) AS codigo
        FROM time_report.tbl_time_report_proyecto p
        WHERE p.Codigo IS NOT NULL AND trim(p.Codigo) <> ''
        GROUP BY upper(trim(p.Codigo))
        HAVING count(*) > 1
    ) r;

    IF v_duplicados IS NOT NULL THEN
        RAISE EXCEPTION 'Hay códigos de proyecto repetidos: %. Corríjalos antes de ejecutar este script.', v_duplicados;
    END IF;

    SELECT max((regexp_match(upper(trim(p.Codigo)), '^PR-(\d+)$'))[1]::BIGINT)
    INTO v_ultimo_codigo
    FROM time_report.tbl_time_report_proyecto p
    WHERE p.Codigo IS NOT NULL;

    SELECT CASE WHEN s.is_called THEN s.last_value ELSE 0 END
    INTO v_valor_secuencia
    FROM time_report.seq_proyecto_codigo s;

    v_siguiente_base := GREATEST(COALESCE(v_ultimo_codigo, 0), v_valor_secuencia);

    IF v_siguiente_base > 0 THEN
        PERFORM setval('time_report.seq_proyecto_codigo', v_siguiente_base, TRUE);
    END IF;

    RAISE NOTICE 'Secuencia de código de proyecto lista. Último número usado: %', v_siguiente_base;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS uq_tr_proyecto_codigo
    ON time_report.tbl_time_report_proyecto (upper(trim(Codigo)))
    WHERE Codigo IS NOT NULL AND trim(Codigo) <> '';

COMMIT;
