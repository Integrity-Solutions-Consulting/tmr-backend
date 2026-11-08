-- ============================================================================
-- SCRIPT: 19-Inv_tmr_db_renombrar_tipos_contrato.sql
-- Propósito: Renombra las etiquetas del catálogo TCT (Tipo de contrato) sembradas
-- por el script 02. Solo cambia Valor/Descripcion; los códigos (FJ, CON, MT, PAS,
-- COS) no cambian, porque el backend decide por código (ver
-- CalculoHorasPeriodo.HorasJornadaPorCodigo, que compara contra "PAS") y los
-- empleados ya asignados referencian el Id del detalle, no el texto.
--   FJ  : Fijo           -> Relación de Dependencia – Afiliado
--   CON : Por contrato   -> Prestación de Servicios Profesionales
--   MT  : Medio tiempo   -> Medio Tiempo
--   PAS : Pasantía       -> Prestación de servicios - Juniors
--   COS : Consultor      -> Consultor Externo
-- Requiere: 02-Inv_tmr_catalogos actualizado.v3.sql
-- Se puede ejecutar más de una vez.
-- ============================================================================

BEGIN;

DO $$
DECLARE
    v_filas INT;
BEGIN
    UPDATE administracion.tbl_administracion_catalogo_detalle d
    SET Valor = v.NuevoValor,
        Descripcion = v.NuevaDescripcion,
        UsuarioModificacion = 'SYSTEM',
        FechaModificacion = now(),
        IpModificacion = '127.0.0.1'
    FROM administracion.tbl_administracion_catalogo c
    CROSS JOIN (VALUES
        ('FJ',  'Relación de Dependencia – Afiliado',    'Contrato indefinido a tiempo completo'),
        ('CON', 'Prestación de Servicios Profesionales', 'Contrato a plazo fijo o por proyecto'),
        ('MT',  'Medio Tiempo',                           'Contrato a tiempo parcial'),
        ('PAS', 'Prestación de servicios - Juniors',      'Pasante o practicante universitario'),
        ('COS', 'Consultor Externo',                      'Prestación de servicios / honorarios')
    ) AS v(Codigo, NuevoValor, NuevaDescripcion)
    WHERE d.IdCatalogo = c.Id
      AND c.Codigo = 'TCT'
      AND c.TipoCatalogo = 'ADM'
      AND d.CodigoValor = v.Codigo;

    GET DIAGNOSTICS v_filas = ROW_COUNT;
    IF v_filas <> 5 THEN
        RAISE EXCEPTION 'Se esperaban 5 filas actualizadas en TCT, se actualizaron %. Revise el catálogo antes de continuar.', v_filas;
    END IF;

    RAISE NOTICE 'Catálogo TCT (Tipo de contrato) renombrado: % filas actualizadas', v_filas;
END $$;

COMMIT;
