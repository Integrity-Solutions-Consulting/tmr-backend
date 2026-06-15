ALTER TABLE time_report.tbl_time_report_empleado_proyecto
    RENAME TO tbl_time_report_asignacion_proyecto;
 
ALTER TABLE time_report.tbl_time_report_asignacion_proyecto
    ADD COLUMN idlider integer,
    ADD COLUMN lidercosto numeric(15,2),
    ADD COLUMN liderhora numeric(10,2);
 
ALTER TABLE time_report.tbl_time_report_asignacion_proyecto
    ADD CONSTRAINT fk_time_report_asignacion_proyecto_lider FOREIGN KEY (idlider)
        REFERENCES administracion.tbl_administracion_lider (id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION;
 
CREATE INDEX IF NOT EXISTS idx_tr_asignacion_proyecto_lider
    ON time_report.tbl_time_report_asignacion_proyecto USING btree
    (idlider ASC NULLS LAST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default;