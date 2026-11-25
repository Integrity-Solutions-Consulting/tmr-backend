-- ============================================================================
-- SCRIPT DE CARGA HISTÓRICA DE PERMISOS / VACACIONES (COMPATIBLE CON PRODUCCIÓN)
-- Prerrequisito: Haber ejecutado 24-Inv_tmr_db_vacaciones_historial_edicion.sql
-- Características:
--   * Resuelve id_empleado dinámicamente por CÉDULA normalizada a 10 dígitos
--     (NO depende de IDs de la base de migración).
--   * 100% idempotente: no duplica registros si se ejecuta más de una vez.
--   * Genera códigos HIST-YYYY-NNN solo para casos verificados y sin conflictos.
--     Los casos pendientes permanecen en la fuente para revisión de RR. HH.
-- Generado: 2026-11-24 09:38 | Colaboradores: 33 | Registros: 219
-- ============================================================================

BEGIN;

DO $$
DECLARE
    v_id_tipo_vac    INT;
    v_id_estado_apr  INT;
    v_id_empleado    INT;
    v_codigo         VARCHAR(30);
    v_next_seq       INT;
    v_insertados     INT := 0;
    v_existentes     INT := 0;
    v_sin_match      INT := 0;
    v_pendientes     INT := 0;
    v_coincidencias  INT;
    rec              RECORD;
BEGIN
    SELECT id INTO v_id_tipo_vac FROM time_report.tbl_time_report_tipo_permiso WHERE codigo = 'VACACIONES' LIMIT 1;
    SELECT id INTO v_id_estado_apr FROM time_report.tbl_time_report_estado_solicitud WHERE codigo = 'APROBADA' LIMIT 1;

    IF v_id_tipo_vac IS NULL OR v_id_estado_apr IS NULL THEN
        RAISE EXCEPTION 'Catálogos del módulo de permisos no inicializados (ejecute scripts 23 y 24 primero).';
    END IF;

    FOR rec IN
        WITH origen AS (SELECT * FROM (VALUES
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2013-07-27', DATE '2013-07-27', DATE '2013-07-29', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2013-09-10', DATE '2013-09-10', DATE '2013-09-11', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2013-10-25', DATE '2013-10-25', DATE '2013-10-28', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2013-12-19', DATE '2013-12-19', DATE '2013-12-20', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2014-03-17', DATE '2014-03-23', DATE '2014-03-24', 6::numeric, 'PERMISO VACACIONES', TRUE, FALSE, FALSE, 'Días registrados (6) difieren del rango calendario (7d)'),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2014-04-04', DATE '2014-04-04', DATE '2014-04-07', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2014-04-21', DATE '2014-04-25', DATE '2014-04-28', 5::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2014-05-05', DATE '2014-05-05', DATE '2014-05-06', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2014-08-25', DATE '2014-08-25', DATE '2014-08-26', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2014-10-06', DATE '2014-10-07', DATE '2014-10-08', 2::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0917705089', 'Calvache Piguave Jean Xavier', DATE '2015-01-16', DATE '2015-01-30', DATE '2015-02-02', 15::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2015-02-09', DATE '2015-02-09', DATE '2015-02-10', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2015-04-09', DATE '2015-04-09', DATE '2015-04-10', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2015-04-16', DATE '2015-04-16', DATE '2015-04-17', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2015-06-11', DATE '2015-06-12', DATE '2015-06-15', 2::numeric, 'PERMISO VACACIONES', TRUE, FALSE, FALSE, 'Año inicio corregido (11/06/2025 -> año 2015)'),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2015-07-23', DATE '2015-07-31', DATE '2015-08-03', 9::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0917705089', 'Calvache Piguave Jean Xavier', DATE '2015-09-04', DATE '2015-09-04', DATE '2015-09-07', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2015-09-17', DATE '2015-09-17', DATE '2015-09-18', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2015-10-28', DATE '2015-10-28', DATE '2015-10-29', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, FALSE, 'Año fin corregido (28/10/2014 -> año 2015)'),
            ('0921591012', 'Moran Villafuerte Gabriela Teresa', DATE '2015-11-18', DATE '2015-11-18', DATE '2015-11-19', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2015-12-11', DATE '2015-12-11', DATE '2015-12-14', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2015-12-15', DATE '2015-12-15', DATE '2015-12-16', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2016-01-01', DATE '2016-12-27', DATE '2016-12-28', 6::numeric, 'VACAIONCES ANTICIPADAS DE 2016 AL 2017', TRUE, TRUE, FALSE, 'Complementado desde hoja VACACIONES HISTÓRICO | Fechas invertidas corregidas desde ''27-12-2016 HASTA 01-01-2016'' | Días registrados (6) difieren del rango calendario (362d)'),
            ('0917705089', 'Calvache Piguave Jean Xavier', DATE '2016-01-18', DATE '2016-02-14', DATE '2016-02-15', 28::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2016-02-05', DATE '2016-02-05', DATE '2016-02-08', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, FALSE, 'Fecha normalizada desde ''05-02-20016'''),
            ('0921591012', 'Moran Villafuerte Gabriela Teresa', DATE '2016-02-10', DATE '2016-02-23', DATE '2016-02-24', 13::numeric, 'PERMISO VACACIONES', TRUE, FALSE, FALSE, 'Días registrados (13) difieren del rango calendario (14d)'),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2016-02-15', DATE '2016-02-28', DATE '2016-02-29', 13::numeric, 'PERMISO VACACIONES', TRUE, FALSE, FALSE, 'Días registrados (13) difieren del rango calendario (14d)'),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2016-02-29', DATE '2016-03-12', DATE '2016-03-14', 13::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0917705089', 'Calvache Piguave Jean Xavier', DATE '2016-06-23', DATE '2016-06-24', DATE '2016-06-27', 2::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2016-07-29', DATE '2016-07-29', DATE '2016-08-01', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2016-07-29', DATE '2016-07-29', DATE '2016-08-01', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2016-10-03', DATE '2016-10-03', DATE '2016-10-04', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0921591012', 'Moran Villafuerte Gabriela Teresa', DATE '2016-10-20', DATE '2016-11-03', DATE '2016-11-04', 17::numeric, 'PERMISO VACACIONES', TRUE, FALSE, FALSE, 'Días registrados (17) difieren del rango calendario (15d)'),
            ('0917705089', 'Calvache Piguave Jean Xavier', DATE '2016-12-19', DATE '2017-01-02', DATE '2017-01-03', 15::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2016-12-27', DATE '2017-01-01', DATE '2017-01-02', 6::numeric, 'PERMISO VACACIONES', TRUE, TRUE, FALSE, 'Nota Excel: CASO ESPECIAL, PABLITO YA AGOTO SUS VACACIONES DEL PERIODO 2016-2016 POR LO QUE ESTA ADELANTANDO LAS DEL 2016-2017'),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2016-12-30', DATE '2016-12-30', DATE '2017-01-02', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0921591012', 'Moran Villafuerte Gabriela Teresa', DATE '2017-02-24', DATE '2017-02-24', DATE '2017-02-27', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2017-03-17', DATE '2017-03-23', DATE '2017-03-24', 7::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0921591012', 'Moran Villafuerte Gabriela Teresa', DATE '2017-03-22', DATE '2017-03-22', DATE '2017-03-23', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2017-04-04', DATE '2017-04-04', DATE '2017-04-05', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2017-04-10', DATE '2017-04-19', DATE '2017-04-20', 10::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0921591012', 'Moran Villafuerte Gabriela Teresa', DATE '2017-07-28', DATE '2017-07-28', DATE '2017-07-31', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2017-09-05', DATE '2017-09-05', DATE '2017-09-06', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2017-09-12', DATE '2017-09-12', DATE '2017-09-13', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2017-09-18', DATE '2017-09-22', DATE '2017-09-25', 5::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0921591012', 'Moran Villafuerte Gabriela Teresa', DATE '2017-09-18', DATE '2017-09-24', DATE '2017-09-25', 7::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0921591012', 'Moran Villafuerte Gabriela Teresa', DATE '2017-10-28', DATE '2017-10-28', DATE '2017-10-30', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2017-11-17', DATE '2017-11-17', DATE '2017-11-20', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2017-12-10', DATE '2017-12-10', DATE '2017-12-11', 10::numeric, '*Enfermedad de el hijo', TRUE, FALSE, FALSE, 'Complementado desde hoja VACACIONES HISTÓRICO | Días registrados (10) difieren del rango calendario (1d)'),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2017-12-11', DATE '2017-12-11', DATE '2017-12-12', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0921591012', 'Moran Villafuerte Gabriela Teresa', DATE '2018-01-08', DATE '2018-01-13', DATE '2018-01-15', 6::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2018-03-22', DATE '2018-04-06', DATE '2018-04-09', 15::numeric, 'PERMISO VACACIONES', TRUE, FALSE, FALSE, 'Días registrados (15) difieren del rango calendario (16d)'),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2018-04-17', DATE '2018-04-17', DATE '2018-04-18', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0917705089', 'Calvache Piguave Jean Xavier', DATE '2018-06-29', DATE '2018-06-29', DATE '2018-07-02', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0921591012', 'Moran Villafuerte Gabriela Teresa', DATE '2018-07-23', DATE '2018-07-29', DATE '2018-07-30', 7::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2018-08-06', DATE '2018-08-21', DATE '2018-08-22', 16::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2018-08-08', DATE '2018-08-08', DATE '2018-08-09', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0917705089', 'Calvache Piguave Jean Xavier', DATE '2018-08-16', DATE '2018-08-30', DATE '2018-08-31', 15::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0921591012', 'Moran Villafuerte Gabriela Teresa', DATE '2018-10-24', DATE '2018-10-28', DATE '2018-10-29', 5::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0921591012', 'Moran Villafuerte Gabriela Teresa', DATE '2018-10-29', DATE '2018-10-29', DATE '2018-10-30', 2::numeric, 'PERMISO VACACIONES', TRUE, FALSE, FALSE, 'Días registrados (2) difieren del rango calendario (1d)'),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2018-11-23', DATE '2018-11-23', DATE '2018-11-26', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2018-12-10', DATE '2018-12-10', DATE '2018-12-11', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2019-01-18', DATE '2019-01-18', DATE '2019-01-21', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0921591012', 'Moran Villafuerte Gabriela Teresa', DATE '2019-01-21', DATE '2019-01-27', DATE '2019-01-28', 7::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2019-02-20', DATE '2019-03-03', DATE '2019-03-04', 12::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2019-03-18', DATE '2019-03-27', DATE '2019-03-28', 10::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2019-06-10', DATE '2019-06-10', DATE '2019-06-11', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2019-06-18', DATE '2019-06-18', DATE '2019-06-19', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0921591012', 'Moran Villafuerte Gabriela Teresa', DATE '2019-07-29', DATE '2019-07-31', DATE '2019-08-01', 3::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0921591012', 'Moran Villafuerte Gabriela Teresa', DATE '2019-09-09', DATE '2019-09-10', DATE '2019-09-11', 2::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2019-09-16', DATE '2019-09-22', DATE '2019-09-23', 7::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2019-09-26', DATE '2019-09-26', DATE '2019-09-27', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0921591012', 'Moran Villafuerte Gabriela Teresa', DATE '2019-10-25', DATE '2019-10-25', DATE '2019-10-28', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0921591012', 'Moran Villafuerte Gabriela Teresa', DATE '2019-11-22', DATE '2019-11-25', DATE '2019-11-26', 2::numeric, 'PERMISO VACACIONES', TRUE, FALSE, FALSE, 'Días registrados (2) difieren del rango calendario (4d)'),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2019-12-17', DATE '2019-12-17', DATE '2019-12-18', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0917705089', 'Calvache Piguave Jean Xavier', DATE '2020-01-10', DATE '2020-01-24', DATE '2020-01-27', 16::numeric, 'PERMISO VACACIONES', TRUE, FALSE, FALSE, 'Días registrados (16) difieren del rango calendario (15d)'),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2020-01-22', DATE '2020-01-22', DATE '2020-01-23', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0921591012', 'Moran Villafuerte Gabriela Teresa', DATE '2020-01-28', DATE '2020-01-31', DATE '2020-02-03', 3::numeric, 'PERMISO VACACIONES', TRUE, FALSE, FALSE, 'Días registrados (3) difieren del rango calendario (4d)'),
            ('0917705089', 'Calvache Piguave Jean Xavier', DATE '2020-02-04', DATE '2020-02-04', DATE '2020-02-05', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0917705089', 'Calvache Piguave Jean Xavier', DATE '2020-02-05', DATE '2020-02-05', DATE '2020-02-06', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2020-02-26', DATE '2020-03-10', DATE '2020-03-11', 14::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2020-03-09', DATE '2020-03-26', DATE '2020-03-27', 18::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0917705089', 'Calvache Piguave Jean Xavier', DATE '2020-11-16', DATE '2020-12-02', DATE '2020-12-03', 17::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0921591012', 'Moran Villafuerte Gabriela Teresa', DATE '2020-11-16', DATE '2020-11-30', DATE '2020-12-01', 15::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0917705089', 'Calvache Piguave Jean Xavier', DATE '2021-01-26', DATE '2021-02-12', DATE '2021-02-15', 18::numeric, 'PERMISO VACACIONES', TRUE, FALSE, FALSE, 'Fecha normalizada desde ''26-01-20-2021'''),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2021-01-28', DATE '2021-01-28', DATE '2021-01-29', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2021-02-08', DATE '2021-02-21', DATE '2021-02-22', 14::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2021-05-03', DATE '2021-05-12', DATE '2021-05-13', 10::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2021-05-17', DATE '2021-05-21', DATE '2021-05-24', 5::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2021-05-27', DATE '2021-05-27', DATE '2021-05-28', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2021-06-25', DATE '2021-06-25', DATE '2021-06-28', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2021-07-01', DATE '2021-07-01', DATE '2021-07-02', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0921591012', 'Moran Villafuerte Gabriela Teresa', DATE '2021-07-07', DATE '2021-07-18', DATE '2021-07-19', 12::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2021-07-30', DATE '2021-08-04', DATE '2021-08-05', 6::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0921591012', 'Moran Villafuerte Gabriela Teresa', DATE '2021-08-30', DATE '2021-09-12', DATE '2021-09-13', 13::numeric, 'PERMISO VACACIONES', TRUE, FALSE, FALSE, 'Días registrados (13) difieren del rango calendario (14d)'),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2022-01-18', DATE '2022-01-30', DATE '2022-01-31', 13::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2022-03-10', DATE '2022-03-10', DATE '2022-03-11', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2022-03-18', DATE '2022-03-18', DATE '2022-03-21', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2022-03-24', DATE '2022-03-24', DATE '2022-03-25', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2022-04-18', DATE '2022-05-02', DATE '2022-05-03', 15::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2022-06-21', DATE '2022-06-21', DATE '2022-06-22', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2022-06-24', DATE '2022-06-24', DATE '2022-06-27', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0930566666', 'Vélez Gómez José Antonio', DATE '2022-09-07', DATE '2022-09-07', DATE '2022-09-08', 1::numeric, 'EXAMEN MEDICO', TRUE, FALSE, TRUE, NULL),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2022-10-18', DATE '2022-10-18', DATE '2022-10-19', 1::numeric, 'EMERGENCIA FAMILIAR', TRUE, FALSE, TRUE, NULL),
            ('0921541728', 'Ramirez Castro Hector David', DATE '2022-11-07', DATE '2022-11-13', DATE '2022-11-14', 7::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2022-11-10', DATE '2022-11-10', DATE '2022-11-11', 1::numeric, 'TRAMITES PERSONALES', TRUE, FALSE, TRUE, NULL),
            ('0917705089', 'Calvache Piguave Jean Xavier', DATE '2022-11-21', DATE '2022-12-05', DATE '2022-12-06', 16::numeric, 'PERMISO VACACIONES', TRUE, FALSE, FALSE, 'Días registrados (16) difieren del rango calendario (15d)'),
            ('0921541728', 'Ramirez Castro Hector David', DATE '2022-12-19', DATE '2022-12-26', DATE '2022-12-27', 8::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0930566666', 'Vélez Gómez José Antonio', DATE '2022-12-19', DATE '2022-12-27', DATE '2022-12-28', 8::numeric, 'PERMISO VACACIONES', TRUE, FALSE, FALSE, 'Días registrados (8) difieren del rango calendario (9d)'),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2022-12-20', DATE '2023-01-01', DATE '2023-01-02', 6::numeric, '*Vacaciones 2022-2023', TRUE, FALSE, FALSE, 'Complementado desde hoja VACACIONES HISTÓRICO | Días registrados (6) difieren del rango calendario (13d)'),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2022-12-26', DATE '2023-01-01', DATE '2023-01-02', 6::numeric, 'PERMISO VACACIONES', TRUE, FALSE, FALSE, 'Días registrados (6) difieren del rango calendario (7d)'),
            ('0921108718', 'Ramirez Morán Marlon Tomas', DATE '2023-01-30', DATE '2023-01-30', DATE '2023-01-31', 1::numeric, 'ACOMPAÑO A SU ESPOSA AL MEDICO', TRUE, FALSE, TRUE, NULL),
            ('0930566666', 'Vélez Gómez José Antonio', DATE '2023-02-13', DATE '2023-02-13', DATE '2023-02-14', 1::numeric, 'TRAMITES PERSONALES', TRUE, FALSE, TRUE, NULL),
            ('0914805452', 'Rivera Pazos Ivonne Alexandra', DATE '2023-03-22', DATE '2023-03-22', DATE '2023-03-23', 1::numeric, '*Incorporacion de el hijo', TRUE, FALSE, FALSE, 'Complementado desde hoja VACACIONES HISTÓRICO'),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2023-04-20', DATE '2023-04-25', DATE '2023-04-26', 1::numeric, 'URGENCIA MEDICA', TRUE, FALSE, FALSE, 'Fechas invertidas corregidas (25/04/2023..20/04/2023) | Días registrados (1) difieren del rango calendario (6d)'),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2023-04-24', DATE '2023-04-24', DATE '2023-04-25', 1::numeric, 'CIRUGIA DEL HIJO', TRUE, FALSE, TRUE, NULL),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2023-05-08', DATE '2023-05-08', DATE '2023-05-09', 1::numeric, 'TRAMITES PERSONALES', TRUE, FALSE, TRUE, NULL),
            ('0921108718', 'Ramirez Morán Marlon Tomas', DATE '2023-05-22', DATE '2023-05-22', DATE '2023-05-23', 1::numeric, 'PROCEDIMIENTO MEDICO DELICADO DE LA ESPOSA', TRUE, FALSE, TRUE, NULL),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2023-06-12', DATE '2023-06-26', DATE '2023-06-27', 13::numeric, 'perido de vacaciones', TRUE, FALSE, FALSE, 'Complementado desde hoja VACACIONES HISTÓRICO | Días registrados (13) difieren del rango calendario (15d)'),
            ('0914805452', 'Rivera Pazos Ivonne Alexandra', DATE '2023-07-12', DATE '2023-07-12', DATE '2023-07-13', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0914805452', 'Rivera Pazos Ivonne Alexandra', DATE '2023-07-21', DATE '2023-07-25', DATE '2023-07-26', 5::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0917705089', 'Calvache Piguave Jean Xavier', DATE '2023-11-06', DATE '2023-11-20', DATE '2023-11-21', 15::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0921108718', 'Ramirez Morán Marlon Tomas', DATE '2024-02-14', DATE '2024-02-20', DATE '2024-02-21', 7::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2024-03-26', DATE '2024-03-26', DATE '2024-03-27', 1::numeric, 'TRAMITES PERSONALES', TRUE, FALSE, TRUE, NULL),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2024-04-15', DATE '2024-05-01', DATE '2024-05-02', 17::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2024-05-15', DATE '2024-06-01', DATE '2024-06-03', 17::numeric, '*Vacaciones correspondiente al periodo 2023-2024', TRUE, FALSE, FALSE, 'Complementado desde hoja VACACIONES HISTÓRICO | Días registrados (17) difieren del rango calendario (18d)'),
            ('0940231277', 'Romero Alvarado Eduardo de Jesus', DATE '2024-06-13', DATE '2024-06-16', DATE '2024-06-17', 4::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0914805452', 'Rivera Pazos Ivonne Alexandra', DATE '2024-07-15', DATE '2024-07-21', DATE '2024-07-22', 7::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0921541728', 'Ramirez Castro Hector David', DATE '2024-07-15', DATE '2024-07-29', DATE '2024-07-30', 15::numeric, 'PERMISO VACACIONES', TRUE, FALSE, FALSE, 'Rango original 15/07/2024..30/08/2024 (47d) ajustado a 15d (29/07/2024) — VERIFICAR'),
            ('0917705089', 'Calvache Piguave Jean Xavier', DATE '2024-12-09', DATE '2025-01-02', DATE '2025-01-03', 22::numeric, '*Vacaciones pendientes 2024-2025', TRUE, FALSE, FALSE, 'Complementado desde hoja VACACIONES HISTÓRICO | Días registrados (22) difieren del rango calendario (25d)'),
            ('0950088575', 'Anastacio Burgos Lucy Viviana', DATE '2025-01-06', DATE '2025-01-12', DATE '2025-01-13', 6::numeric, 'PERMISO VACACIONES', TRUE, FALSE, FALSE, 'Días registrados (6) difieren del rango calendario (7d)'),
            ('0921591012', 'Moran Villafuerte Gabriela Teresa', DATE '2025-01-15', DATE '2025-01-31', DATE '2025-02-03', 17::numeric, 'PERMISO VACACIONES', TRUE, FALSE, FALSE, 'Fecha normalizada desde ''15-01-20225'''),
            ('0930566666', 'Vélez Gómez José Antonio', DATE '2025-02-03', DATE '2025-02-09', DATE '2025-02-10', 7::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0921108718', 'Ramirez Morán Marlon Tomas', DATE '2025-02-07', DATE '2025-02-16', DATE '2025-02-17', 10::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0914805452', 'Rivera Pazos Ivonne Alexandra', DATE '2025-02-10', DATE '2025-02-16', DATE '2025-02-17', 7::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0921108718', 'Ramirez Morán Marlon Tomas', DATE '2025-02-19', DATE '2025-02-21', DATE '2025-02-24', 3::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0950088575', 'Anastacio Burgos Lucy Viviana', DATE '2025-03-06', DATE '2025-03-09', DATE '2025-03-10', 4::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0929632230', 'Pico Roman Brayan Eduardo', DATE '2025-03-20', DATE '2025-04-03', DATE '2025-04-04', 15::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0914805452', 'Rivera Pazos Ivonne Alexandra', DATE '2025-03-22', DATE '2025-03-22', DATE '2025-03-24', 1::numeric, 'INCORPORACION DEL HIJO', TRUE, FALSE, FALSE, 'Columna AÑO Excel (2023) difiere de la fecha (22/03/2025) — VERIFICAR'),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2025-03-24', DATE '2025-04-07', DATE '2025-04-08', 15::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0950962365', 'Cordova Pincay Cristhian Bryan', DATE '2025-04-14', DATE '2025-04-22', DATE '2025-04-23', 9::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2025-04-25', DATE '2025-04-25', DATE '2025-04-28', 1::numeric, 'TRAMITES PERSONALES', TRUE, FALSE, TRUE, NULL),
            ('0930566666', 'Vélez Gómez José Antonio', DATE '2025-05-10', DATE '2026-05-04', DATE '2026-05-05', 7::numeric, 'PERMISO DE VACACIONES', TRUE, FALSE, FALSE, 'Complementado desde hoja VACACIONES HISTÓRICO | Fechas invertidas corregidas desde ''04/05/2026 hasta 10/05/2025'' | Días registrados (7) difieren del rango calendario (360d)'),
            ('0912986619', 'Aguila Camacho Juan Carlos', DATE '2025-06-16', DATE '2025-06-24', DATE '2025-06-25', 9::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0921591012', 'Moran Villafuerte Gabriela Teresa', DATE '2025-06-16', DATE '2025-06-30', DATE '2025-07-01', 15::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2025-07-09', DATE '2025-07-09', DATE '2025-07-10', 1::numeric, 'TRAMITES PERSONALES', TRUE, FALSE, TRUE, NULL),
            ('0914805452', 'Rivera Pazos Ivonne Alexandra', DATE '2025-07-10', DATE '2025-07-10', DATE '2025-07-11', 1::numeric, 'EXAMENES MEDICOS', TRUE, FALSE, TRUE, NULL),
            ('0914805452', 'Rivera Pazos Ivonne Alexandra', DATE '2025-07-15', DATE '2025-07-20', DATE '2025-07-21', 6::numeric, 'PERMISO VACACIONES', TRUE, FALSE, FALSE, 'Mes fin corregido (20/05/2025 -> 20/07/2025)'),
            ('0919702340', 'Cumbicos Arevalo Tanya Elizabeth', DATE '2025-07-18', DATE '2025-07-18', DATE '2025-07-21', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0950962365', 'Cordova Pincay Cristhian Bryan', DATE '2025-07-21', DATE '2025-08-03', DATE '2025-08-04', 14::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0914805452', 'Rivera Pazos Ivonne Alexandra', DATE '2025-07-23', DATE '2025-07-23', DATE '2025-07-24', 1::numeric, 'TRAMITES PERSONALES', TRUE, FALSE, TRUE, NULL),
            ('0957259021', 'Alarcon Montero Daniel Isaac', DATE '2025-08-18', DATE '2025-08-24', DATE '2025-08-25', 7::numeric, 'VACACIONES ANTICIPADAS', TRUE, TRUE, TRUE, NULL),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2025-08-20', DATE '2025-08-25', DATE '2025-08-26', 6::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2025-08-26', DATE '2025-08-26', DATE '2025-08-27', 1::numeric, 'JURAMENTO DE BANDERA DEL HIJO', TRUE, FALSE, TRUE, NULL),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2025-08-27', DATE '2025-08-27', DATE '2025-08-28', 1::numeric, 'TRAMITES PERSONALES', TRUE, FALSE, TRUE, NULL),
            ('0919702340', 'Cumbicos Arevalo Tanya Elizabeth', DATE '2025-09-05', DATE '2025-09-08', DATE '2025-09-09', 4::numeric, 'VACACIONES ANTICIPADAS', TRUE, TRUE, TRUE, NULL),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2025-09-26', DATE '2025-09-26', DATE '2025-09-29', 1::numeric, '*Juramento de la bandera de el hijo', TRUE, FALSE, FALSE, 'Complementado desde hoja VACACIONES HISTÓRICO'),
            ('0914805452', 'Rivera Pazos Ivonne Alexandra', DATE '2025-10-03', DATE '2025-10-03', DATE '2025-10-06', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0914805452', 'Rivera Pazos Ivonne Alexandra', DATE '2025-10-10', DATE '2025-10-10', DATE '2025-10-13', 1::numeric, 'EXAMENES MEDICOS', TRUE, FALSE, TRUE, NULL),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2025-10-17', DATE '2025-10-17', DATE '2025-10-20', 1::numeric, 'CUMPLEAÑOS DEL HIJO', TRUE, FALSE, TRUE, NULL),
            ('0914805452', 'Rivera Pazos Ivonne Alexandra', DATE '2025-11-20', DATE '2025-11-20', DATE '2025-11-21', 1::numeric, 'CITA MEDICA', TRUE, FALSE, TRUE, NULL),
            ('0919702340', 'Cumbicos Arevalo Tanya Elizabeth', DATE '2025-11-27', DATE '2025-11-27', DATE '2025-11-28', 1::numeric, 'PERSONAL', TRUE, FALSE, TRUE, NULL),
            ('0919702340', 'Cumbicos Arevalo Tanya Elizabeth', DATE '2025-12-09', DATE '2025-12-09', DATE '2025-12-10', 1::numeric, 'URGENCIA MÉDICA ACOMPAÑAR A ESPOSO A PROCEDIMIENTO DE RETINA', TRUE, FALSE, FALSE, 'Nota Excel: FALTA DOCUMENTACIÓN'),
            ('2450342312', 'Villao Gonzalez Jordan Israel', DATE '2025-12-24', DATE '2025-12-30', DATE '2025-12-31', 6::numeric, 'PERMISO VACACIONES', TRUE, FALSE, FALSE, 'Días registrados (6) difieren del rango calendario (7d)'),
            ('0924307051', 'Cárdenas Ortiz Steven Omar', DATE '2025-12-26', DATE '2025-12-26', DATE '2025-12-29', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('25349563', 'Pereira Sivira Jesus Gabriel', DATE '2025-12-29', DATE '2025-12-30', DATE '2025-12-31', 2::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0918281106', 'Lopez Espinoza Diego Emilio', DATE '2026-01-05', DATE '2026-01-11', DATE '2026-01-12', 7::numeric, 'EXÁMENES MÉDICOS, ACOMPAÑAR A SU PAPÁ', TRUE, FALSE, TRUE, NULL),
            ('0950088575', 'Anastacio Burgos Lucy Viviana', DATE '2026-01-05', DATE '2026-01-11', DATE '2026-01-12', 7::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0918281106', 'Lopez Espinoza Diego Emilio', DATE '2026-01-09', DATE '2026-01-09', DATE '2026-01-12', 1::numeric, 'PERMISO DE VACACIONES', TRUE, FALSE, FALSE, 'Complementado desde hoja VACACIONES HISTÓRICO'),
            ('0923985840', 'Caisaguano Sanchez Daniel Francisco', DATE '2026-01-12', DATE '2026-01-20', DATE '2026-01-21', 9::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0914805452', 'Rivera Pazos Ivonne Alexandra', DATE '2026-01-20', DATE '2026-01-20', DATE '2026-01-21', 1::numeric, 'PERMISO CERTIFICADO MÉDICO', TRUE, FALSE, TRUE, NULL),
            ('0927852897', 'Cruz Intriago Luis Gino', DATE '2026-01-23', DATE '2026-01-28', DATE '2026-01-29', 6::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2026-01-26', DATE '2026-02-16', DATE '2026-02-17', 21::numeric, 'PERMISO VACACIONES', TRUE, FALSE, FALSE, 'Mes fin corregido (16/01/2026 -> 16/02/2026) | Días registrados (21) difieren del rango calendario (22d)'),
            ('0943114660', 'Salazar Abad Byron Alexander', DATE '2026-02-03', DATE '2026-02-03', DATE '2026-02-04', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0921108718', 'Ramirez Morán Marlon Tomas', DATE '2026-02-23', DATE '2026-03-08', DATE '2026-03-09', 14::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2026-02-24', DATE '2026-02-24', DATE '2026-02-25', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0927219337', 'Herrera Banchon Glenda Narcisa', DATE '2026-02-25', DATE '2026-02-25', DATE '2026-02-26', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2026-02-26', DATE '2026-03-16', DATE '2026-03-17', 21::numeric, 'PERMISO VACACIONES', TRUE, FALSE, FALSE, 'Días registrados (21) difieren del rango calendario (19d)'),
            ('0914805452', 'Rivera Pazos Ivonne Alexandra', DATE '2026-03-18', DATE '2026-03-18', DATE '2026-03-19', 1::numeric, 'CITA MEDICA DEL CONYUGUE', TRUE, FALSE, FALSE, 'Año fin corregido (18/03/2025 -> año 2026)'),
            ('0929632230', 'Pico Roman Brayan Eduardo', DATE '2026-03-20', DATE '2026-04-04', DATE '2026-04-06', 15::numeric, 'PERMISO VACACIONES', TRUE, FALSE, FALSE, 'Nota Excel: VERIFICAR | Formato MM/DD normalizado desde ''03/04/2026'' | Mes fin corregido (04/03/2026 -> 04/04/2026) | Días registrados (15) difieren del rango calendario (16d)'),
            ('0919389114', 'Aguirre portilla Galo Hernan', DATE '2026-04-01', DATE '2026-04-06', DATE '2026-04-07', 5::numeric, 'PERMISO VACACIONES', TRUE, FALSE, FALSE, 'Días registrados (5) difieren del rango calendario (6d)'),
            ('0914805452', 'Rivera Pazos Ivonne Alexandra', DATE '2026-04-09', DATE '2026-04-09', DATE '2026-04-10', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('1311544686', 'Roca Montesdeoca Karen Elisa', DATE '2026-04-13', DATE '2026-04-19', DATE '2026-04-20', 7::numeric, '*Vacaciones', TRUE, FALSE, FALSE, 'Complementado desde hoja VACACIONES HISTÓRICO'),
            ('0914805452', 'Rivera Pazos Ivonne Alexandra', DATE '2026-04-14', DATE '2026-04-20', DATE '2026-04-21', 6::numeric, 'PERMISO VACACIONES', TRUE, FALSE, FALSE, 'Días registrados (6) difieren del rango calendario (7d)'),
            ('0927219337', 'Herrera Banchon Glenda Narcisa', DATE '2026-04-20', DATE '2026-04-26', DATE '2026-04-27', 7::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0943114660', 'Salazar Abad Byron Alexander', DATE '2026-04-21', DATE '2026-04-21', DATE '2026-04-22', 1::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2026-04-23', DATE '2026-04-24', DATE '2026-04-27', 2::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0940231277', 'Romero Alvarado Eduardo de Jesus', DATE '2026-05-04', DATE '2026-05-10', DATE '2026-05-11', 7::numeric, 'PERMISO VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0930566666', 'Vélez Gómez José Antonio', DATE '2026-05-05', DATE '2026-05-11', DATE '2026-05-12', 6::numeric, 'PERMISO VACACIONES', TRUE, FALSE, FALSE, 'Días registrados (6) difieren del rango calendario (7d)'),
            ('0950962365', 'Cordova Pincay Cristhian Bryan', DATE '2026-05-14', DATE '2026-05-14', DATE '2026-05-15', 1::numeric, 'OPERACIÓN DE PAPÁ', TRUE, FALSE, TRUE, NULL),
            ('0950962365', 'Cordova Pincay Cristhian Bryan', DATE '2026-05-28', DATE '2026-06-07', DATE '2026-06-08', 11::numeric, 'VIAJE POR SORTEO DECAMERON', TRUE, FALSE, TRUE, NULL),
            ('0923985840', 'Caisaguano Sanchez Daniel Francisco', DATE '2026-06-08', DATE '2026-06-14', DATE '2026-06-15', 7::numeric, 'VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0950962365', 'Cordova Pincay Cristhian Bryan', DATE '2026-06-08', DATE '2026-06-28', DATE '2026-06-29', 11::numeric, 'VGozar de Vaije por sorteo del decameron', TRUE, FALSE, FALSE, 'Complementado desde hoja VACACIONES HISTÓRICO | Fechas invertidas corregidas desde ''28/06/2026 - 08/06/2026'' | Días registrados (11) difieren del rango calendario (21d)'),
            ('0914805452', 'Rivera Pazos Ivonne Alexandra', DATE '2026-06-17', DATE '2026-06-19', DATE '2026-06-22', 2::numeric, 'PERMISO POR TEMAS DE SALUD', TRUE, FALSE, FALSE, 'Nota Excel: FALTA DE DOCUMENTACIÓN | Días registrados (2) difieren del rango calendario (3d)'),
            ('0919702340', 'Cumbicos Arevalo Tanya Elizabeth', DATE '2026-06-17', DATE '2026-06-17', DATE '2026-06-18', 1::numeric, 'PERMISO POR TRÁMITE DE SUS HIJOS', TRUE, FALSE, FALSE, 'Nota Excel: FALTA DOCUMENTACIÓN'),
            ('0943114660', 'Salazar Abad Byron Alexander', DATE '2026-07-13', DATE '2026-07-19', DATE '2026-07-20', 7::numeric, 'VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0921591012', 'Moran Villafuerte Gabriela Teresa', DATE '2026-07-16', DATE '2026-07-30', DATE '2026-07-31', 15::numeric, 'VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0927500645', 'Rendon Cajape Luiggi Ricardo', DATE '2026-07-20', DATE '2026-07-26', DATE '2026-07-27', 7::numeric, 'VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0914805452', 'Rivera Pazos Ivonne Alexandra', DATE '2026-08-18', DATE '2026-08-18', DATE '2026-08-19', 1::numeric, 'DÍA LIBRE POR CRUCE DE DESCANSO OBLIGATORIO DEL VIERNES 26 DE JUNIO', TRUE, FALSE, TRUE, NULL),
            ('0918281106', 'Lopez Espinoza Diego Emilio', DATE '2026-08-20', DATE '2026-08-20', DATE '2026-08-21', 1::numeric, 'DILIGENCIAS PERSONALES', TRUE, FALSE, TRUE, NULL),
            ('0958147258', 'Orrala León Andrea Angelina', DATE '2026-08-20', DATE '2026-08-20', DATE '2026-08-21', 1::numeric, 'SUSTENTACIÓN DE PROYECTO DE TÉSIS', TRUE, FALSE, TRUE, NULL),
            ('0919702340', 'Cumbicos Arevalo Tanya Elizabeth', DATE '2026-08-27', DATE '2026-08-30', DATE '2026-08-31', 4::numeric, 'VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0914805452', 'Rivera Pazos Ivonne Alexandra', DATE '2026-09-03', DATE '2026-09-03', DATE '2026-09-04', 1::numeric, 'VACACIONES MOTIVOS PERSONALES', TRUE, FALSE, FALSE, 'Formato MM/DD normalizado desde ''09/03/2026'' | Formato MM/DD normalizado desde ''09/03/2026'''),
            ('0950088575', 'Anastacio Burgos Lucy Viviana', DATE '2026-09-07', DATE '2026-09-14', DATE '2026-09-15', 8::numeric, 'VACACIONES', TRUE, FALSE, FALSE, 'Formato MM/DD normalizado desde ''09/07/2026'''),
            ('0957259021', 'Alarcon Montero Daniel Isaac', DATE '2026-09-07', DATE '2026-09-13', DATE '2026-09-14', 7::numeric, 'VACACIONES', TRUE, FALSE, FALSE, 'Formato MM/DD normalizado desde ''09/07/2026'''),
            ('0943114660', 'Salazar Abad Byron Alexander', DATE '2026-09-08', DATE '2026-09-08', DATE '2026-09-09', 1::numeric, 'DÍA LIBRE POR CRUCE DE DESCANSO OBLIGATORIO DEL VIERNES 26 DE JUNIO', TRUE, FALSE, FALSE, 'Formato MM/DD normalizado desde ''09/08/2026'' | Formato MM/DD normalizado desde ''09/08/2026'''),
            ('0929632230', 'Pico Roman Brayan Eduardo', DATE '2026-09-14', DATE '2026-09-14', DATE '2026-09-15', 1::numeric, 'DÍA LIBRE POR CRUCE DE DESCANSO OBLIGATORIO DEL VIERNES 26 DE JUNIO', TRUE, FALSE, TRUE, NULL),
            ('0952574903', 'Beltran Tovar Dylan Alejandro', DATE '2026-09-14', DATE '2026-09-20', DATE '2026-09-21', 7::numeric, 'VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('1206970285', 'Aguirre Lugo Akira Mahilyn', DATE '2026-09-21', DATE '2026-09-27', DATE '2026-09-28', 7::numeric, 'VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0952498376', 'Álvarez Llamuca Aaron David', DATE '2026-09-28', DATE '2026-09-29', DATE '2026-09-30', 2::numeric, 'VACACIONES MOTIVOS PERSONALES', TRUE, FALSE, TRUE, NULL),
            ('0914805452', 'Rivera Pazos Ivonne Alexandra', DATE '2026-10-07', DATE '2026-10-08', DATE '2026-10-09', 1::numeric, 'VACACIONES', TRUE, FALSE, FALSE, 'Días registrados (1) difieren del rango calendario (2d)'),
            ('0802814798', 'Marin Muñoz Luís Alberto', DATE '2026-10-12', DATE '2026-10-16', DATE '2026-10-19', 5::numeric, 'VACACIONES', TRUE, FALSE, FALSE, 'Formato MM/DD normalizado desde ''10/12/2026'''),
            ('0919702340', 'Cumbicos Arevalo Tanya Elizabeth', DATE '2026-10-12', DATE '2026-10-16', DATE '2026-10-19', 5::numeric, 'VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0957235435', 'Lavayen Garcia Angie Nicole', DATE '2026-10-12', DATE '2026-10-15', DATE '2026-10-16', 4::numeric, 'VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0940231277', 'Romero Alvarado Eduardo de Jesus', DATE '2026-10-22', DATE '2026-10-26', DATE '2026-10-27', 5::numeric, 'VACACIONES', TRUE, FALSE, TRUE, NULL),
            ('0924307051', 'Cárdenas Ortiz Steven Omar', DATE '2026-11-04', DATE '2026-11-10', DATE '2026-11-11', 7::numeric, 'VACACIONES', TRUE, FALSE, FALSE, 'Formato MM/DD normalizado desde ''11/04/2026'' | Formato MM/DD normalizado desde ''11/10/2026'''),
            ('0929583243', 'Reyes Montalvan Yandri David', DATE '2026-11-05', DATE '2026-11-06', DATE '2026-11-09', 2::numeric, 'VACACIONES', TRUE, FALSE, FALSE, 'Formato MM/DD normalizado desde ''11/05/2026'' | Formato MM/DD normalizado desde ''11/06/2026'''),
            ('0921541728', 'Ramirez Castro Hector David', DATE '2026-11-10', DATE '2026-11-24', DATE '2026-11-25', 15::numeric, 'VACACIONES', TRUE, FALSE, FALSE, 'Nota Excel: AZUL??? | Formato MM/DD normalizado desde ''11/10/2026'''),
            ('0918281106', 'Lopez Espinoza Diego Emilio', DATE '2026-11-12', DATE '2026-11-17', DATE '2026-11-18', 6::numeric, 'VACACIONES', TRUE, FALSE, FALSE, 'Nota Excel: FALTA DOCUMENTACIÓN | Formato MM/DD normalizado desde ''11/12/2026''')
        ) AS t(cedula, nombre_hc, fecha_inicio, fecha_fin, fecha_regreso, dias, motivo, descuenta_vac, es_anticipada, verificado_rrhh, observacion))
        SELECT o.*, (SELECT count(*) FROM origen x WHERE x.cedula = o.cedula
            AND x.fecha_inicio <= o.fecha_fin AND o.fecha_inicio <= x.fecha_fin) AS cruces
        FROM origen o
    LOOP
        IF NOT rec.verificado_rrhh OR rec.cruces > 1 THEN
            v_pendientes := v_pendientes + 1;
            CONTINUE;
        END IF;
        -- Match por identificación normalizada sin ceros a la izquierda (soporta cédulas 9/10 dígitos y pasaportes/cédulas extranjeras)
        SELECT min(e.id), count(*) INTO v_id_empleado, v_coincidencias
        FROM administracion.tbl_administracion_empleado e
        JOIN administracion.tbl_administracion_persona p ON p.id = e.idpersona
        WHERE LTRIM(REGEXP_REPLACE(COALESCE(p.numeroidentificacion, ''), '[^0-9]', '', 'g'), '0') = LTRIM(rec.cedula, '0')
          AND e.activo = TRUE
          AND e.codigoempleado NOT LIKE 'EMP-%';

        IF v_coincidencias <> 1 THEN
            v_sin_match := v_sin_match + 1;
            RAISE NOTICE 'SIN MATCH EN BD -> Cédula: %, Nombre HC: %, Fecha: % (% d)', rec.cedula, rec.nombre_hc, rec.fecha_inicio, rec.dias;
            CONTINUE;
        END IF;

        -- Idempotencia: omitir si ya existe un registro activo del mismo empleado, fechas y días
        IF EXISTS (
            SELECT 1 FROM time_report.tbl_time_report_solicitud s
            WHERE s.id_empleado = v_id_empleado
              AND s.fecha_inicio = rec.fecha_inicio
              AND s.fecha_fin = rec.fecha_fin
              AND s.dias_solicitados = rec.dias
              AND s.activo = TRUE
        ) THEN
            v_existentes := v_existentes + 1;
            CONTINUE;
        END IF;

        IF EXISTS (
            SELECT 1 FROM time_report.tbl_time_report_solicitud s
            WHERE s.activo AND s.id_empleado = v_id_empleado
              AND s.fecha_inicio <= rec.fecha_fin AND rec.fecha_inicio <= s.fecha_fin
        ) THEN
            v_pendientes := v_pendientes + 1;
            CONTINUE;
        END IF;

        SELECT COALESCE(MAX(CAST(SUBSTRING(codigo FROM '[0-9]+$') AS INT)), 0) + 1
        INTO v_next_seq
        FROM time_report.tbl_time_report_solicitud
        WHERE codigo LIKE 'HIST-' || EXTRACT(YEAR FROM rec.fecha_inicio)::INT || '-%';

        v_codigo := 'HIST-' || EXTRACT(YEAR FROM rec.fecha_inicio)::INT || '-' || LPAD(v_next_seq::TEXT, 3, '0');

        INSERT INTO time_report.tbl_time_report_solicitud (
            codigo, id_empleado, id_tipo_permiso, id_estado, fecha_solicitud, motivo,
            fecha_inicio, fecha_fin, fecha_regreso, dias_solicitados, horario, detalle_horario,
            horas_solicitadas, impacto_timereport, descuenta_vacaciones, es_recuperable,
            es_anticipada, es_historico, verificado_rrhh, observacion_migracion,
            activo, usuario_creacion, fecha_creacion, ip_creacion
        ) VALUES (
            v_codigo, v_id_empleado, v_id_tipo_vac, v_id_estado_apr, rec.fecha_inicio, rec.motivo,
            rec.fecha_inicio, rec.fecha_fin, rec.fecha_regreso, rec.dias, 'COMPLETA', 'Días calendario',
            ROUND(rec.dias * 8, 2), 'SIN_IMPACTO', rec.descuenta_vac, FALSE,
            rec.es_anticipada, TRUE, rec.verificado_rrhh, rec.observacion,
            TRUE, 'MIGRACION_EXCEL', NOW(), '127.0.0.1'
        );

        v_insertados := v_insertados + 1;
    END LOOP;

    RAISE NOTICE 'CARGA HISTÓRICA COMPLETADA -> Insertados: %, Ya existentes (omitidos): %, Sin identidad única: %, Pendientes por revisar: %', v_insertados, v_existentes, v_sin_match, v_pendientes;
END $$;

COMMIT;
