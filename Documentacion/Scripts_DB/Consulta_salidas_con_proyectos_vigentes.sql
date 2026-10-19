-- =============================================================================
-- CONSULTA (solo lectura, no modifica datos)
-- Colaboradores con fecha de salida registrada que siguen vigentes en algún proyecto:
-- la asignación actual no tiene salida, o su salida es posterior a la del colaborador,
-- o el proyecto empieza después de la salida del colaborador.
-- =============================================================================
SELECT
    e.id                                   AS id_empleado,
    e.codigoempleado                       AS codigo,
    p.nombres || ' ' || p.apellidos        AS colaborador,
    CASE WHEN e.activo THEN 'Activo' ELSE 'Inactivo' END AS estado_colaborador,
    e.fechaterminacion                     AS fecha_salida_colaborador,
    ts.valor                               AS tipo_salida,
    cs.valor                               AS causa_salida,
    e.comentariosalida                     AS motivo_salida,
    pr.codigo                              AS codigo_proyecto,
    pr.nombre                              AS proyecto,
    a.fechaasignacion                      AS entrada_proyecto,
    a.fechafinasignacion                   AS salida_proyecto,
    CASE WHEN a.estadoasignacion THEN 'Activo' ELSE 'Inactivo' END AS estado_asignacion,
    CASE
        WHEN a.fechaasignacion > e.fechaterminacion THEN 'Entra al proyecto después de su salida'
        WHEN a.fechafinasignacion IS NULL           THEN 'Proyecto sin fecha de salida'
        ELSE 'Salida del proyecto posterior a su salida'
    END                                    AS observacion
FROM administracion.tbl_administracion_empleado e
JOIN administracion.tbl_administracion_persona p
    ON p.id = e.idpersona
JOIN time_report.tbl_time_report_asignacion_proyecto a
    ON a.idempleado = e.id
   AND a.activo                            -- solo la versión vigente de la asignación
JOIN time_report.tbl_time_report_proyecto pr
    ON pr.id = a.idproyecto
LEFT JOIN administracion.tbl_administracion_catalogo_detalle ts ON ts.id = e.idtiposalida
LEFT JOIN administracion.tbl_administracion_catalogo_detalle cs ON cs.id = e.idcausasalida
WHERE e.fechaterminacion IS NOT NULL
  AND (a.fechafinasignacion IS NULL
       OR a.fechafinasignacion > e.fechaterminacion
       OR a.fechaasignacion > e.fechaterminacion)
ORDER BY colaborador, proyecto;
