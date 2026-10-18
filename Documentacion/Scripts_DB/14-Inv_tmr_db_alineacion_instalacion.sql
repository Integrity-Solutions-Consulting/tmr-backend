-- Ajustes necesarios para una instalación nueva con el modelo EF actual.
-- No se ejecuta durante el arranque web. Requiere scripts 01–13 (excepto 06).
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns
               WHERE table_schema = 'time_report'
                 AND table_name = 'tbl_time_report_asignacion_proyecto'
                 AND column_name = 'liderhora')
       AND NOT EXISTS (SELECT 1 FROM information_schema.columns
                       WHERE table_schema = 'time_report'
                         AND table_name = 'tbl_time_report_asignacion_proyecto'
                         AND column_name = 'liderhoras') THEN
        ALTER TABLE time_report.tbl_time_report_asignacion_proyecto
            RENAME COLUMN liderhora TO liderhoras;
    END IF;
END $$;

-- El script histórico 05 suponía IDs que no coinciden con el orden del seed 02.
-- Resolver por nombre sin reescribir las versiones históricas.
UPDATE autenticacion.tbl_autenticacion_permiso p
SET idmodulo = m.id
FROM (VALUES
    ('REQUERIMIENTOS_VIEW', 'Requerimientos'),
    ('CONFIGURACION_VIEW', 'Configuracion'),
    ('REPORTES_VIEW', 'Reportes'),
    ('TIME_REPORT_VIEW', 'Time Report')
) AS v(codigo, modulo)
JOIN autenticacion.tbl_autenticacion_modulo m ON m.nombremodulo = v.modulo
WHERE p.codigo = v.codigo AND p.idmodulo IS DISTINCT FROM m.id;
