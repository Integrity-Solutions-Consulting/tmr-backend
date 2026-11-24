-- ============================================================================
-- SCRIPT DE VERIFICACIÓN PREVIA (SOLO LECTURA) PARA PRODUCCIÓN
-- Propósito: Validar que TODAS las cédulas de los colaboradores con permisos
--            históricos existen y tienen ficha activa en Producción antes de
--            ejecutar CARGAR_HISTORIAL_PERMISOS_PRODUCCION.sql.
-- Generado: 2026-11-24 09:38 | Colaboradores: 33 | Registros: 219
-- ============================================================================

WITH origen (cedula, nombre_hc, regimen_hc, cant_registros, total_dias_excel) AS (
    VALUES
        ('0912986619', 'Aguila Camacho Juan Carlos', 'AFILIACIÓN', 1, 9::numeric),
        ('1206970285', 'Aguirre Lugo Akira Mahilyn', 'MIXTO', 1, 7::numeric),
        ('0919389114', 'Aguirre portilla Galo Hernan', 'AFILIACIÓN', 1, 5::numeric),
        ('0957259021', 'Alarcon Montero Daniel Isaac', 'AFILIACIÓN', 2, 14::numeric),
        ('0952498376', 'Álvarez Llamuca Aaron David', 'MIXTO', 1, 2::numeric),
        ('0950088575', 'Anastacio Burgos Lucy Viviana', 'AFILIACIÓN', 4, 25::numeric),
        ('0952574903', 'Beltran Tovar Dylan Alejandro', 'MIXTO', 1, 7::numeric),
        ('0923985840', 'Caisaguano Sanchez Daniel Francisco', 'AFILIACIÓN', 2, 16::numeric),
        ('0917705089', 'Calvache Piguave Jean Xavier', 'AFILIACIÓN', 15, 183::numeric),
        ('0924307051', 'Cárdenas Ortiz Steven Omar', 'FACTURA', 2, 8::numeric),
        ('0950962365', 'Cordova Pincay Cristhian Bryan', 'AFILIACIÓN', 5, 46::numeric),
        ('0927852897', 'Cruz Intriago Luis Gino', 'FACTURA', 1, 6::numeric),
        ('0919702340', 'Cumbicos Arevalo Tanya Elizabeth', 'MIXTO', 7, 17::numeric),
        ('0911123636', 'Garcia Zambrano Maribel Alexandra', 'AFILIACIÓN', 46, 232::numeric),
        ('0927219337', 'Herrera Banchon Glenda Narcisa', 'AFILIACIÓN', 2, 8::numeric),
        ('0957235435', 'Lavayen Garcia Angie Nicole', 'AFILIACIÓN', 1, 4::numeric),
        ('0918281106', 'Lopez Espinoza Diego Emilio', 'AFILIACIÓN', 4, 15::numeric),
        ('0802814798', 'Marin Muñoz Luís Alberto', 'AFILIACIÓN', 1, 5::numeric),
        ('0921591012', 'Moran Villafuerte Gabriela Teresa', 'AFILIACIÓN', 24, 167::numeric),
        ('0958147258', 'Orrala León Andrea Angelina', 'AFILIACIÓN', 1, 1::numeric),
        ('25349563', 'Pereira Sivira Jesus Gabriel', 'FACTURA', 1, 2::numeric),
        ('0929632230', 'Pico Roman Brayan Eduardo', 'AFILIACIÓN', 3, 31::numeric),
        ('0921541728', 'Ramirez Castro Hector David', 'AFILIACIÓN', 4, 45::numeric),
        ('0921108718', 'Ramirez Morán Marlon Tomas', 'AFILIACIÓN', 6, 36::numeric),
        ('0927500645', 'Rendon Cajape Luiggi Ricardo', 'FACTURA', 1, 7::numeric),
        ('0929583243', 'Reyes Montalvan Yandri David', 'FACTURA', 1, 2::numeric),
        ('0914805452', 'Rivera Pazos Ivonne Alexandra', 'AFILIACIÓN', 20, 47::numeric),
        ('1311544686', 'Roca Montesdeoca Karen Elisa', 'AFILIACIÓN', 1, 7::numeric),
        ('0940231277', 'Romero Alvarado Eduardo de Jesus', 'AFILIACIÓN', 3, 16::numeric),
        ('0943114660', 'Salazar Abad Byron Alexander', 'AFILIACIÓN', 4, 10::numeric),
        ('0930566666', 'Vélez Gómez José Antonio', 'AFILIACIÓN', 6, 30::numeric),
        ('2450342312', 'Villao Gonzalez Jordan Israel', 'FACTURA', 1, 6::numeric),
        ('0918839739', 'Villegas Avilés Pablo Roberto', 'AFILIACIÓN', 46, 197::numeric)
),
empleados_bd AS (
    SELECT DISTINCT ON (LTRIM(REGEXP_REPLACE(COALESCE(p.numeroidentificacion, ''), '[^0-9]', '', 'g'), '0'))
        LTRIM(REGEXP_REPLACE(COALESCE(p.numeroidentificacion, ''), '[^0-9]', '', 'g'), '0') AS cedula_key,
        p.numeroidentificacion AS cedula_bd,
        e.id AS id_empleado,
        e.codigoempleado,
        TRIM(p.apellidos || ' ' || p.nombres) AS nombre_bd,
        e.activo AS empleado_activo,
        e.fechaterminacion,
        tc.codigovalor AS cod_contrato,
        emp.codigovalor AS cod_empresa,
        e.fechaingreso
    FROM administracion.tbl_administracion_empleado e
    JOIN administracion.tbl_administracion_persona p ON p.id = e.idpersona
    LEFT JOIN administracion.tbl_administracion_catalogo_detalle tc ON tc.id = e.idtipocontrato
    LEFT JOIN administracion.tbl_administracion_catalogo_detalle emp ON emp.id = e.idempresacatalogo
    WHERE e.codigoempleado NOT LIKE 'EMP-%'
    ORDER BY LTRIM(REGEXP_REPLACE(COALESCE(p.numeroidentificacion, ''), '[^0-9]', '', 'g'), '0'), e.activo DESC, (e.fechaterminacion IS NULL) DESC, e.id ASC
)
SELECT
    o.cedula,
    o.nombre_hc,
    o.regimen_hc,
    o.cant_registros,
    o.total_dias_excel,
    b.id_empleado,
    b.codigoempleado,
    b.nombre_bd,
    b.empleado_activo,
    b.cod_contrato,
    b.cod_empresa,
    b.fechaingreso,
    CASE
        WHEN b.id_empleado IS NULL THEN 'SIN MATCH EN PRODUCCIÓN'
        WHEN b.empleado_activo = FALSE THEN 'INACTIVO EN BD (OMITIDO)'
        WHEN b.fechaterminacion IS NOT NULL THEN 'TIENE FECHA TERMINACIÓN'
        ELSE 'OK'
    END AS estado_match
FROM origen o
LEFT JOIN empleados_bd b ON b.cedula_key = LTRIM(o.cedula, '0')
ORDER BY estado_match DESC, o.nombre_hc ASC;
