/*
  Incremental posterior a 24-Instalar_Mis_Datos_Manual.sql.

  Evidencia de origen:
    commit ff3d082 / origin/feature-perfil
    Migration 20261119120000_AddConocimientosPerfil
    EF: ColaboradorDatosControl.ConocimientosJson -> conocimientos jsonb

  Este script no modifica datos existentes ni asigna estados de revisión.
  La parte de EstadoRevision queda deliberadamente fuera: el checkout no
  contiene GetProfileAsync/MyReviewsAsync ni ese miembro, por lo que no es
  seguro inferir tabla, tipo, nulabilidad, default o comillas.
*/

BEGIN;

DO $$
BEGIN
    IF to_regclass('administracion.tbl_colaborador_datos_control') IS NULL THEN
        RAISE EXCEPTION
            'Precondición incumplida: falta administracion.tbl_colaborador_datos_control (ejecutar primero script 24)';
    END IF;
END $$;

ALTER TABLE administracion.tbl_colaborador_datos_control
    ADD COLUMN IF NOT EXISTS conocimientos jsonb;

/* Verificación estructural exacta del DDL de EF/migración. */
DO $$
DECLARE
    v_type text;
    v_nullable text;
    v_default text;
BEGIN
    SELECT data_type, is_nullable, column_default
      INTO v_type, v_nullable, v_default
      FROM information_schema.columns
     WHERE table_schema = 'administracion'
       AND table_name = 'tbl_colaborador_datos_control'
       AND column_name = 'conocimientos';

    IF v_type IS DISTINCT FROM 'jsonb'
       OR v_nullable IS DISTINCT FROM 'YES'
       OR v_default IS NOT NULL THEN
        RAISE EXCEPTION
            'DDL incompatible para administracion.tbl_colaborador_datos_control.conocimientos: type=%, nullable=%, default=%',
            v_type, v_nullable, v_default;
    END IF;
END $$;

COMMIT;

/* Verificaciones para CloudBeaver: ejecutar también después de la segunda pasada. */
SELECT table_schema, table_name, column_name, data_type, is_nullable, column_default
  FROM information_schema.columns
 WHERE table_schema = 'administracion'
   AND table_name = 'tbl_colaborador_datos_control'
   AND column_name = 'conocimientos';

/* Debe devolver 0 filas: no se inventa ni se altera ninguna revisión. */
SELECT table_schema, table_name, column_name
  FROM information_schema.columns
 WHERE lower(column_name) = lower('EstadoRevision')
 ORDER BY table_schema, table_name, column_name;
