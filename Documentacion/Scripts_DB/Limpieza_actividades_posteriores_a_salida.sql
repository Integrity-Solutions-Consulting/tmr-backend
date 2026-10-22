-- =============================================================================
-- LIMPIEZA MANUAL (modifica datos) — NO SE HA EJECUTADO, solo se entrega el script.
--
-- Contexto: antes de que RegistrarSalidaAsync borrara automáticamente las
-- actividades posteriores a la fecha de salida (ver ColaboradorService.cs), un
-- colaborador podía haber creado una "actividad recurrente" que generó filas en
-- time_report.tbl_time_report_actividad_diaria para muchos días hacia adelante.
-- Si después se le registró la salida, esas filas con fecha posterior quedaron
-- colgadas: el colaborador aparece con actividad en días en que ya no debía
-- tenerla. El fix en código solo aplica a salidas registradas DE AHORA EN
-- ADELANTE; este script es para limpiar los casos que ya existían ANTES del fix.
--
-- Regla (igual a la del código): colaborador con fechaterminacion registrada,
-- actividad con fechaactividad > fechaterminacion de ese colaborador.
--
-- Cómo usarlo:
--   1) Ejecutar primero el PASO 1 (solo lectura) y revisar el resultado.
--   2) Si la lista es la esperada, ejecutar el PASO 2 dentro de una transacción.
--      Revisar el número de filas afectadas (ROW COUNT) y recién ahí hacer
--      COMMIT. Si algo no coincide con lo revisado en el PASO 1, hacer ROLLBACK.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- PASO 1 — Vista previa (solo lectura, no modifica nada). Revisar antes de borrar.
-- -----------------------------------------------------------------------------
SELECT
    e.id                             AS id_empleado,
    e.codigoempleado                 AS codigo,
    p.nombres || ' ' || p.apellidos  AS colaborador,
    e.fechaterminacion               AS fecha_salida_colaborador,
    a.id                             AS id_actividad,
    a.idproyecto                     AS id_proyecto,
    a.fechaactividad                 AS fecha_actividad,
    a.cantidadhoras                  AS horas,
    a.descripcionactividad           AS descripcion,
    a.activo                         AS actividad_activa
FROM time_report.tbl_time_report_actividad_diaria a
JOIN administracion.tbl_administracion_empleado e
    ON e.id = a.idempleado
JOIN administracion.tbl_administracion_persona p
    ON p.id = e.idpersona
WHERE e.fechaterminacion IS NOT NULL
  AND a.fechaactividad > e.fechaterminacion
ORDER BY colaborador, a.fechaactividad;

-- -----------------------------------------------------------------------------
-- PASO 2 — Borrado definitivo (igual que hace el código: DELETE real, no lógico).
-- Ejecutar dentro de una transacción y revisar antes de confirmar.
-- -----------------------------------------------------------------------------
-- BEGIN;
--
-- DELETE FROM time_report.tbl_time_report_actividad_diaria a
-- WHERE a.fechaactividad > (
--     SELECT e.fechaterminacion
--     FROM administracion.tbl_administracion_empleado e
--     WHERE e.id = a.idempleado
-- )
-- AND EXISTS (
--     SELECT 1
--     FROM administracion.tbl_administracion_empleado e
--     WHERE e.id = a.idempleado
--       AND e.fechaterminacion IS NOT NULL
-- );
--
-- -- Revisar aquí cuántas filas se borraron (debe coincidir con el PASO 1).
-- -- Si coincide:
-- -- COMMIT;
-- -- Si no coincide o algo se ve mal:
-- -- ROLLBACK;
