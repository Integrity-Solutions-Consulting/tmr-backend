-- ============================================================================
-- SCRIPT: 20-Inv_tmr_db_inserts_cargos_administrativo.sql
-- Propósito: Agrega cargos al departamento Administrativo (código ADM del catálogo
-- DEP). Los cargos son datos: el backend y el frontend los leen de la tabla, así
-- que aparecen en los listados sin cambios de código.
--   Líder de Nómina
--   Mensajero
--   Gerente General
--   Directora de Estrategia y Transformación
-- Requiere: 02-Inv_tmr_catalogos actualizado.v3.sql
-- Se puede ejecutar más de una vez: no duplica un cargo que ya exista en el
-- departamento (sin distinguir mayúsculas), aunque se haya creado desde Configuración.
-- ============================================================================

BEGIN;

DO $$
DECLARE
    v_id_departamento INT;
    v_filas INT;
BEGIN
    SELECT d.Id
    INTO v_id_departamento
    FROM administracion.tbl_administracion_catalogo_detalle d
    JOIN administracion.tbl_administracion_catalogo c ON c.Id = d.IdCatalogo
    WHERE c.Codigo = 'DEP'
      AND c.TipoCatalogo = 'ADM'
      AND d.CodigoValor = 'ADM';

    IF v_id_departamento IS NULL THEN
        RAISE EXCEPTION 'No existe el departamento Administrativo (ADM) en el catálogo DEP. Revise el catálogo antes de continuar.';
    END IF;

    INSERT INTO administracion.tbl_administracion_cargo
        (IdDepartamento, NombreCargo, Activo, UsuarioCreacion, IpCreacion)
    SELECT v_id_departamento, x.NombreCargo, TRUE, 'SYSTEM', '127.0.0.1'
    FROM (VALUES
        ('Líder de Nómina'),
        ('Mensajero'),
        ('Gerente General'),
        ('Directora de Estrategia y Transformación')
    ) AS x(NombreCargo)
    WHERE NOT EXISTS (
        SELECT 1
        FROM administracion.tbl_administracion_cargo c
        WHERE c.IdDepartamento = v_id_departamento
          AND lower(c.NombreCargo) = lower(x.NombreCargo)
    );

    GET DIAGNOSTICS v_filas = ROW_COUNT;
    RAISE NOTICE 'Cargos agregados a Administrativo: % filas insertadas', v_filas;
END $$;

COMMIT;
