-- ============================================================================
-- SINCRONIZACIÓN DE FECHAS DE AFILIACIÓN IESS (BASE DE VACACIONES) DESDE HC 2026
-- Prerrequisito: 25-Inv_tmr_db_vacaciones_fecha_afiliacion.sql
-- * Inserta un ajuste FECHA_AFILIACION por colaborador (tbl_time_report_ajuste_vacaciones).
-- * NO modifica administracion.tbl_administracion_empleado.fechaingreso ni el historial de contratos.
-- * Resuelve id_empleado por CÉDULA normalizada (no depende de IDs de la BD de migración).
-- * Idempotente: omite colaboradores que ya tienen un ajuste FECHA_AFILIACION activo
--   (una corrección manual de Talento Humano prevalece).
-- * Afiliación = la menor fecha válida entre AFILIACIÓN ISC y AFILIACIÓN RPS.
-- Generado: 2026-11-24 11:50 | Registros: 67
--
-- OMITIDOS (revisar con Talento Humano y corregir desde Saldos -> Ajustar ->
-- "Corregir fecha de afiliación IESS"):
--   * 0952498376 Álvarez Llamuca Aaron David: sin fecha de afiliación en HC (asociación MIXTO)
--   * 0958636326 Crespo Conforme Jorge Luis: afiliación 01/04/2024 anterior al ingreso general 01/04/2025 (posible error de digitación)
--   * 0923843536 Dorado Quinde Gerald Enzzo: afiliación 06/04/2026 anterior al ingreso general 13/04/2026 (posible error de digitación)
--   * 0953638871 Gamboa Barzola Byron Joel: sin fecha de afiliación en HC (asociación AFILIACIÓN)
--   * 0802814798 Marin Muñoz Luís Alberto: sin fecha de afiliación en HC (asociación AFILIACIÓN)
--   * 1250137161 Nagua Campi Jamil Andrés: sin fecha de afiliación en HC (asociación AFILIACIÓN)
--   * 0951553049 Ramirez Rios Edinson Javier: sin fecha de afiliación en HC (asociación AFILIACIÓN)
--   * 0940231277 Romero Alvarado Eduardo de Jesus: afiliación 03/02/2024 anterior al ingreso general 03/02/2025 (posible error de digitación)
--   * 0950584045 Vera Castro Tyrone Elam: sin fecha de afiliación en HC (asociación AFILIACIÓN)
-- ============================================================================

BEGIN;

DO $$
DECLARE
    v_id_empleado INT;
    v_insertados  INT := 0;
    v_existentes  INT := 0;
    v_sin_match   INT := 0;
    rec           RECORD;
BEGIN
    FOR rec IN
        SELECT * FROM (VALUES
            ('0912986619', 'Aguila Camacho Juan Carlos', DATE '2011-08-01', '01/08/2011', 'ISC 01/08/2011 / RPS 01/09/2015'),
            ('0911822955', 'Aguila Camacho Julio César', DATE '2013-05-01', '01/05/2013', 'RPS'),
            ('0924134851', 'Aguila Meza Carlos Andres', DATE '2022-01-01', '01/01/2022', 'RPS'),
            ('0930361415', 'Aguila Meza Daniela Nicole', DATE '2022-01-01', '01/01/2022', 'ISC'),
            ('0925188448', 'Aguila Plaza Xavier Alejandro', DATE '2021-06-01', '01/06/2021', 'RPS'),
            ('1206970285', 'Aguirre Lugo Akira Mahilyn', DATE '2025-09-01', '11/03/2025', 'ISC'),
            ('0919389114', 'Aguirre portilla Galo Hernan', DATE '2026-04-01', '01/04/2026', 'ISC'),
            ('0922361845', 'Agurto Coello Leonardo Francisco', DATE '2026-04-01', '03/02/2025', 'ISC'),
            ('0957259021', 'Alarcon Montero Daniel Isaac', DATE '2025-01-01', '01/06/2024', 'ISC'),
            ('0954423554', 'Alvear Santillán Erick Javier', DATE '2026-03-02', '02/03/2026', 'ISC'),
            ('0950088575', 'Anastacio Burgos Lucy Viviana', DATE '2024-07-08', '08/07/2024', 'ISC'),
            ('0926799040', 'Andrade Arreaga Byron Michael', DATE '2026-05-01', '25/03/2026', 'ISC'),
            ('0926204793', 'Arboleda Luna Jorge Alejandro', DATE '2025-10-20', '01/06/2025', 'ISC'),
            ('0954576278', 'Basurto Morales Jostin Josue', DATE '2026-10-01', '23/07/2025', 'ISC'),
            ('0952574903', 'Beltran Tovar Dylan Alejandro', DATE '2025-09-01', '11/03/2025', 'ISC'),
            ('0923985840', 'Caisaguano Sanchez Daniel Francisco', DATE '2024-09-02', '02/09/2024', 'ISC'),
            ('0917705089', 'Calvache Piguave Jean Xavier', DATE '2013-11-13', '13/11/2013', 'ISC'),
            ('0923007234', 'Carrasco Chuchuca Juan José', DATE '2026-05-01', '01/05/2026', 'ISC'),
            ('0952916559', 'Cedeño Cruz Diego Alberto', DATE '2026-10-01', '05/05/2025', 'ISC'),
            ('0950962365', 'Cordova Pincay Cristhian Bryan', DATE '2023-09-11', '11/09/2023', 'ISC'),
            ('0919702340', 'Cumbicos Arevalo Tanya Elizabeth', DATE '2024-10-01', '01/04/2022', 'ISC'),
            ('0953238789', 'Delgado Olea Giampaolo', DATE '2025-06-02', '13/05/2024', 'ISC'),
            ('0951626993', 'Demera Rocafuerte Erick Joel', DATE '2025-06-02', '03/06/2024', 'ISC'),
            ('0958735417', 'Falcones Franco Edwin Saul', DATE '2026-09-01', '23/03/2026', 'ISC'),
            ('0925806523', 'Farro Terán Luis Arcángel', DATE '2026-08-24', '24/08/2026', 'ISC'),
            ('0911123636', 'Garcia Zambrano Maribel Alexandra', DATE '2015-01-22', '22/01/2015', 'ISC'),
            ('0927074153', 'Gavela Vera Samuel Isaac', DATE '2026-06-01', '21/07/2025', 'ISC'),
            ('1804879961', 'Guangasi Paste Dario Javier', DATE '2026-09-16', '16/09/2026', 'ISC'),
            ('1205625161', 'Guillén Chevez Víctor Gabriel', DATE '2026-10-01', '01/07/2026', 'ISC'),
            ('0958671802', 'Gutiérrez León Dennis Orlando', DATE '2026-08-03', '03/08/2026', 'ISC'),
            ('0966827875', 'Guzmán Martinez Jesus Augusto', DATE '2026-04-01', '01/02/2024', 'ISC'),
            ('1751741206', 'Hereira Diaz Samuel David', DATE '2025-06-11', '11/06/2025', 'ISC'),
            ('0927219337', 'Herrera Banchon Glenda Narcisa', DATE '2024-09-02', '02/09/2024', 'ISC'),
            ('0955050919', 'Lindao Flores Dustyn Leonardo', DATE '2025-04-01', '24/04/2024', 'ISC'),
            ('0918281106', 'Lopez Espinoza Diego Emilio', DATE '2025-07-22', '22/07/2025', 'ISC'),
            ('0941336042', 'Malo Flores Jordy Ronny', DATE '2026-08-12', '12/08/2026', 'ISC'),
            ('0944369198', 'Maruri Rodríguez Josselyn Emily', DATE '2026-05-01', '01/04/2026', 'ISC'),
            ('0750617268', 'Mateo Orellana Daniel Alfredo', DATE '2026-05-05', '05/05/2026', 'ISC'),
            ('0914366869', 'Meza Mieles Yadira Veronica', DATE '2025-08-15', '01/06/2025', 'ISC'),
            ('0921591012', 'Moran Villafuerte Gabriela Teresa', DATE '2014-10-06', '06/10/2014', 'ISC'),
            ('1250020110', 'Morla Molineros Alexis Dennisse', DATE '2026-05-01', '19/05/2025', 'ISC'),
            ('0958671802', 'Naranjo Burgos Erlyn Stuard', DATE '2026-08-03', '03/08/2026', 'ISC'),
            ('0107205874', 'Ojeda Ojeda Cristian Fabricio', DATE '2026-09-21', '21/09/2026', 'ISC'),
            ('0958147258', 'Orrala León Andrea Angelina', DATE '2025-10-01', '05/05/2025', 'ISC'),
            ('0955719638', 'Pachay Ruiz Samuel Antonio', DATE '2026-02-24', '24/02/2026', 'ISC'),
            ('0929632230', 'Pico Roman Brayan Eduardo', DATE '2024-01-15', '15/01/2024', 'ISC'),
            ('0957941123', 'Pincay Baque Gilda Jamileth', DATE '2026-05-01', '16/10/2025', 'ISC'),
            ('0921541728', 'Ramirez Castro Hector David', DATE '2021-10-04', '04/10/2021', 'ISC'),
            ('0921108718', 'Ramirez Morán Marlon Tomas', DATE '2023-01-03', '01/10/2022', 'ISC'),
            ('0929583243', 'Reyes Montalvan Yandri David', DATE '2026-02-09', '09/02/2026', 'ISC'),
            ('0914805452', 'Rivera Pazos Ivonne Alexandra', DATE '2022-07-11', '11/07/2022', 'ISC'),
            ('1311544686', 'Roca Montesdeoca Karen Elisa', DATE '2024-02-09', '09/02/2024', 'ISC'),
            ('0943114660', 'Salazar Abad Byron Alexander', DATE '2024-08-01', '14/03/2024', 'ISC'),
            ('0925968570', 'Sánchez Córdova Luis Fernando', DATE '2026-05-01', '10/04/2026', 'ISC'),
            ('0954121646', 'Sandoval Quimi Jean Carlo', DATE '2024-11-05', '05/11/2024', 'ISC'),
            ('0954438594', 'Santos Camargo Laura Odalys', DATE '2026-07-13', '13/07/2026', 'ISC'),
            ('0927174698', 'Solorzano Zavala Carlos Eduardo', DATE '2025-08-20', '20/08/2025', 'ISC'),
            ('2450656307', 'Suárez Andrade Giuliana Nicole', DATE '2026-07-01', '26/01/2026', 'ISC'),
            ('0930566666', 'Vélez Gómez José Antonio', DATE '2020-01-02', '02/01/2020', 'ISC'),
            ('0909977860', 'Vera Maruri Carlos David', DATE '2024-04-15', '15/04/2024', 'ISC'),
            ('2450342312', 'Villao Gonzalez Jordan Israel', DATE '2026-07-01', '10/09/2024', 'ISC'),
            ('0918839739', 'Villegas Avilés Pablo Roberto', DATE '2012-07-02', '02/07/2012', 'ISC'),
            ('0931961908', 'Vera Medina Cristopher Alexander', DATE '2026-10-07', '07/10/2026', 'ISC'),
            ('0957235435', 'Lavayen Garcia Angie Nicole', DATE '2026-10-07', '07/10/2026', 'ISC'),
            ('1316530177', 'Andrade Cevallos Lider Ignacio', DATE '2026-10-15', '15/10/2026', 'ISC'),
            ('1718602608', 'Muñoz Burgos Pedro Anibal', DATE '2026-10-22', '22/10/2026', 'ISC'),
            ('0953754660', 'Rosado Alcívar José Julián', DATE '2026-11-09', '09/11/2026', 'ISC')
        ) AS t(cedula, nombre_hc, fecha_afiliacion, ingreso_general, fuente)
    LOOP
        v_id_empleado := NULL;
        SELECT e.id INTO v_id_empleado
        FROM administracion.tbl_administracion_empleado e
        JOIN administracion.tbl_administracion_persona p ON p.id = e.idpersona
        WHERE LTRIM(REGEXP_REPLACE(COALESCE(p.numeroidentificacion, ''), '[^0-9]', '', 'g'), '0') = LTRIM(rec.cedula, '0')
          AND e.activo = TRUE
          AND e.codigoempleado NOT LIKE 'EMP-%'
        ORDER BY (e.fechaterminacion IS NULL) DESC, e.id ASC
        LIMIT 1;

        IF v_id_empleado IS NULL THEN
            v_sin_match := v_sin_match + 1;
            RAISE NOTICE 'SIN MATCH EN BD -> Cédula: %, Nombre HC: %', rec.cedula, rec.nombre_hc;
            CONTINUE;
        END IF;

        IF EXISTS (
            SELECT 1 FROM time_report.tbl_time_report_ajuste_vacaciones a
            WHERE a.id_empleado = v_id_empleado AND a.tipo_ajuste = 'FECHA_AFILIACION' AND a.activo = TRUE
        ) THEN
            v_existentes := v_existentes + 1;
            CONTINUE;
        END IF;

        INSERT INTO time_report.tbl_time_report_ajuste_vacaciones
            (id_empleado, tipo_ajuste, dias_ajuste, permite_vacaciones, fecha_afiliacion_base, motivo,
             activo, usuario_creacion, fecha_creacion, ip_creacion)
        VALUES
            (v_id_empleado, 'FECHA_AFILIACION', 0, NULL, rec.fecha_afiliacion,
             'Fecha de afiliación IESS según HC 2026 (' || rec.fuente || '; ingreso general ' || COALESCE(rec.ingreso_general, 'N/A') || ')',
             TRUE, 'MIGRACION_HC2026', NOW(), '127.0.0.1');
        v_insertados := v_insertados + 1;
    END LOOP;

    RAISE NOTICE 'Fechas de afiliación -> insertadas: %, ya existentes: %, sin match: %', v_insertados, v_existentes, v_sin_match;
END $$;

COMMIT;
