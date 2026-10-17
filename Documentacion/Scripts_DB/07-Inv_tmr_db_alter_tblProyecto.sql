ALTER TABLE time_report.tbl_time_report_proyecto
    DROP COLUMN IF EXISTS idlider;
 
DROP INDEX IF EXISTS time_report.idx_tr_proyecto_lider;