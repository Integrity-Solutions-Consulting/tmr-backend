-- Estructura de producción. Las tres copias operativas bkp_* se conservan solo en sus bases de origen.
SET LOCAL search_path = pg_catalog;
SET LOCAL check_function_bodies = false;


CREATE SCHEMA IF NOT EXISTS administracion;


COMMENT ON SCHEMA administracion IS 'Personal, cargos, clientes, modos de trabajo y datos maestros de RRHH';


CREATE SCHEMA IF NOT EXISTS auditoria;


COMMENT ON SCHEMA auditoria IS 'Registro histórico de cambios en entidades críticas del sistema';


CREATE SCHEMA IF NOT EXISTS autenticacion;


COMMENT ON SCHEMA autenticacion IS 'Usuarios, roles, permisos, sesiones y aplicaciones del sistema';


CREATE SCHEMA IF NOT EXISTS inventario;


COMMENT ON SCHEMA inventario IS 'Equipos, activos tecnológicos, proveedores y facturas de adquisición';


CREATE SCHEMA IF NOT EXISTS inventory;


CREATE SCHEMA IF NOT EXISTS notificacion;


CREATE SCHEMA IF NOT EXISTS time_report;


COMMENT ON SCHEMA time_report IS 'Gestión de proyectos, actividades diarias, permisos y reporte de horas';


CREATE TYPE autenticacion.audit_evento_enum AS ENUM (
    'LOGIN_OK',
    'LOGIN_FAIL',
    'LOGOUT',
    'REFRESH_OK',
    'REFRESH_FAIL',
    'TOKEN_REVOKE',
    'PASSWORD_CHANGE',
    'USER_CREATE',
    'USER_BLOCK'
);


CREATE TYPE autenticacion.revocacion_razon_enum AS ENUM (
    'LOGOUT',
    'ADMIN_REVOKE',
    'PASSWORD_CHANGE',
    'SECURITY_BREACH',
    'EXPIRED',
    'SESSION_LIMIT'
);


CREATE FUNCTION auditoria.fn_auditoria_registrar_asignacion_equipo_insert() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    INSERT INTO auditoria.tbl_auditoria_asignacion_equipo(IdAsignacion,TipoCambio,ValoresNuevos,CambiadoPor)
    VALUES(NEW.Id,'INSERT',row_to_json(NEW)::JSONB,NEW.UsuarioCreacion);
    RETURN NEW;
END; $$;


CREATE FUNCTION auditoria.fn_auditoria_registrar_asignacion_equipo_update() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    INSERT INTO auditoria.tbl_auditoria_asignacion_equipo(IdAsignacion,TipoCambio,ValoresAnteriores,ValoresNuevos,CambiadoPor)
    VALUES(NEW.Id,'UPDATE',row_to_json(OLD)::JSONB,row_to_json(NEW)::JSONB,NEW.UsuarioModificacion);
    RETURN NEW;
END; $$;


CREATE FUNCTION auditoria.fn_auditoria_registrar_empleado_insert() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    INSERT INTO auditoria.tbl_auditoria_empleado(IdEmpleado,TipoCambio,ValoresNuevos,CambiadoPor)
    VALUES(NEW.Id,'INSERT',row_to_json(NEW)::JSONB,NEW.UsuarioCreacion);
    RETURN NEW;
END; $$;


CREATE FUNCTION auditoria.fn_auditoria_registrar_empleado_update() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    INSERT INTO auditoria.tbl_auditoria_empleado(IdEmpleado,TipoCambio,ValoresAnteriores,ValoresNuevos,CambiadoPor)
    VALUES(NEW.Id,'UPDATE',row_to_json(OLD)::JSONB,row_to_json(NEW)::JSONB,NEW.UsuarioModificacion);
    RETURN NEW;
END; $$;


CREATE FUNCTION auditoria.fn_auditoria_registrar_equipo_insert() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    INSERT INTO auditoria.tbl_auditoria_equipo(IdEquipo,TipoCambio,ValoresNuevos,CambiadoPor)
    VALUES(NEW.Id,'INSERT',row_to_json(NEW)::JSONB,NEW.UsuarioCreacion);
    RETURN NEW;
END; $$;


CREATE FUNCTION auditoria.fn_auditoria_registrar_equipo_update() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    INSERT INTO auditoria.tbl_auditoria_equipo(IdEquipo,TipoCambio,ValoresAnteriores,ValoresNuevos,CambiadoPor)
    VALUES(NEW.Id,'UPDATE',row_to_json(OLD)::JSONB,row_to_json(NEW)::JSONB,NEW.UsuarioModificacion);
    RETURN NEW;
END; $$;


CREATE FUNCTION auditoria.fn_auditoria_registrar_login() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    INSERT INTO auditoria.tbl_auditoria_sesion_usuario(IdUsuario,TipoCambio,DireccionIp)
    VALUES(NEW.IdUsuario,'LOGIN',NEW.IpCreacion);
    RETURN NEW;
END; $$;


CREATE FUNCTION auditoria.fn_auditoria_registrar_logout() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    -- Solo registra logout cuando se revoca la sesión (RevocadoFecha pasa de NULL a un valor)
    IF NEW.RevocadoFecha IS NOT NULL AND OLD.RevocadoFecha IS NULL THEN
        INSERT INTO auditoria.tbl_auditoria_sesion_usuario(IdUsuario,TipoCambio,DireccionIp)
        VALUES(NEW.IdUsuario,'LOGOUT',NEW.DireccionIp);
    END IF;
    RETURN NEW;
END; $$;


CREATE FUNCTION inventario.fn_inventario_obtener_equipos_inactivos() RETURNS TABLE(id integer, marca character varying, modelo character varying, numero_serie character varying, nombre_categoria character varying, nombre_estado character varying, fecha_ultima_asignacion timestamp with time zone, ultimo_asignado text)
    LANGUAGE plpgsql
    AS $$
BEGIN
    RETURN QUERY
    SELECT e.Id, e.Marca, e.Modelo, e.NumeroSerie,
           ce.Valor, ee.Valor AS NombreEstado,
           ua.FechaAsignacion,
           (p.Nombres||' '||p.Apellidos)::TEXT
    FROM inventario.tbl_inventario_equipo e
    JOIN administracion.tbl_administracion_catalogo_detalle ce ON e.IdCategoria=ce.Id
    JOIN administracion.tbl_administracion_catalogo_detalle ee ON e.IdEstado=ee.Id
    LEFT JOIN LATERAL (
        SELECT ea.IdEmpleado, ea.FechaAsignacion
        FROM inventario.tbl_inventario_asignacion_equipo ea
        WHERE ea.IdEquipo=e.Id ORDER BY ea.FechaAsignacion DESC LIMIT 1
    ) ua ON TRUE
    LEFT JOIN administracion.tbl_administracion_empleado emp ON ua.IdEmpleado=emp.Id
    LEFT JOIN administracion.tbl_administracion_persona  p   ON emp.IdPersona=p.Id
    WHERE e.Activo=FALSE ORDER BY e.FechaModificacion DESC;
END; $$;


CREATE FUNCTION inventario.fn_inventario_obtener_equipos_por_categoria() RETURNS TABLE(categoria character varying, equipos_asignados bigint)
    LANGUAGE plpgsql
    AS $$
BEGIN
    RETURN QUERY
    SELECT ce.Valor::VARCHAR,
           COUNT(DISTINCT e.Id) FILTER (WHERE ea.FechaDevolucion IS NULL AND ea.Activo=TRUE AND e.Activo=TRUE)
    FROM administracion.tbl_administracion_catalogo_detalle ce
    LEFT JOIN inventario.tbl_inventario_equipo e ON ce.Id=e.IdCategoria
    LEFT JOIN inventario.tbl_inventario_asignacion_equipo ea ON e.Id=ea.IdEquipo
    GROUP BY ce.Valor ORDER BY 2 DESC;
END; $$;


CREATE FUNCTION inventario.fn_inventario_obtener_resumen_estado_por_categoria(p_id_categoria integer) RETURNS TABLE(nombre_estado character varying, conteo_equipos bigint)
    LANGUAGE plpgsql
    AS $$
BEGIN
    RETURN QUERY
    SELECT ee.Valor::VARCHAR, COUNT(e.Id)
    FROM administracion.tbl_administracion_catalogo_detalle ee
    LEFT JOIN inventario.tbl_inventario_equipo e
           ON ee.Id=e.IdEstado AND e.Activo=TRUE AND e.IdCategoria=p_id_categoria
    GROUP BY ee.Valor ORDER BY 2 DESC;
END; $$;


CREATE FUNCTION inventario.fn_inventario_obtener_tendencia_adquisicion(p_anio integer) RETURNS TABLE(numero_mes integer, nombre_mes character varying, conteo_adquisiciones bigint, valor_total_adquisiciones numeric)
    LANGUAGE plpgsql
    AS $$
BEGIN
    RETURN QUERY
    WITH meses AS (
        SELECT 1 AS num,'Ene'::VARCHAR(10) AS nom UNION ALL SELECT 2,'Feb' UNION ALL
        SELECT 3,'Mar' UNION ALL SELECT 4,'Abr' UNION ALL SELECT 5,'May' UNION ALL
        SELECT 6,'Jun' UNION ALL SELECT 7,'Jul' UNION ALL SELECT 8,'Ago' UNION ALL
        SELECT 9,'Sep' UNION ALL SELECT 10,'Oct' UNION ALL SELECT 11,'Nov' UNION ALL SELECT 12,'Dic'
    ),
    adq AS (
        SELECT EXTRACT(MONTH FROM f.FechaFactura)::INTEGER AS mes,
               COUNT(DISTINCT f.Id)                        AS conteo,
               COALESCE(SUM(df.Total), 0)                  AS total
        FROM inventario.tbl_inventario_factura f
        JOIN inventario.tbl_inventario_detalle_factura df ON df.IdFactura = f.Id
        WHERE EXTRACT(YEAR FROM f.FechaFactura) = p_anio
        GROUP BY mes
    )
    SELECT m.num, m.nom,
           COALESCE(a.conteo,0)::BIGINT,
           COALESCE(a.total,0)::NUMERIC
    FROM meses m LEFT JOIN adq a ON m.num = a.mes
    ORDER BY m.num;
END; $$;


CREATE FUNCTION inventario.fn_inventario_obtener_totales_dashboard() RETURNS TABLE(total_equipos bigint, equipos_asignados bigint, total_clientes bigint, equipos_reparacion bigint, equipos_disponibles bigint, equipos_baja bigint)
    LANGUAGE plpgsql
    AS $$
BEGIN
    RETURN QUERY
    WITH ce AS (
        SELECT COUNT(*) FILTER (WHERE Activo = TRUE)  AS total_activo,
               COUNT(*) FILTER (WHERE Activo = FALSE) AS total_inactivo,
               COUNT(*) FILTER (
                   WHERE IdEstado = (SELECT Id FROM administracion.tbl_administracion_catalogo_detalle WHERE Valor='Disponible')
                   AND Activo = TRUE
               ) AS total_disponible
        FROM inventario.tbl_inventario_equipo
    ),
    asig AS (SELECT COUNT(*) AS total FROM inventario.tbl_inventario_asignacion_equipo ea
             JOIN inventario.tbl_inventario_equipo e ON ea.IdEquipo=e.Id
             WHERE ea.FechaDevolucion IS NULL AND e.Activo=TRUE AND ea.Activo=TRUE),
    rep  AS (SELECT COUNT(*) AS total FROM inventario.tbl_inventario_equipo
             WHERE IdEstado IN (SELECT Id FROM administracion.tbl_administracion_catalogo_detalle
                                WHERE Valor IN ('En reparacion','En revision'))
             AND Activo=TRUE),
    cli  AS (SELECT COUNT(*) AS total FROM administracion.tbl_administracion_cliente WHERE Activo=TRUE)
    SELECT ce.total_activo, asig.total, cli.total, rep.total, ce.total_disponible, ce.total_inactivo
    FROM ce, asig, rep, cli;
END; $$;


CREATE FUNCTION time_report.fn_time_report_horas_por_tipo_actividad(p_fecha date DEFAULT CURRENT_DATE) RETURNS TABLE(tipo_actividad character varying, total_horas numeric)
    LANGUAGE plpgsql
    AS $$
BEGIN
    RETURN QUERY
    SELECT ta.NombreTipo::VARCHAR, SUM(da.CantidadHoras)
    FROM time_report.tbl_time_report_actividad_diaria da
    JOIN time_report.tbl_time_report_tipo_actividad ta ON da.IdTipoActividad=ta.Id
    WHERE da.Activo=TRUE AND da.FechaActividad=p_fecha
    GROUP BY ta.NombreTipo ORDER BY ta.NombreTipo;
END; $$;


CREATE FUNCTION time_report.fn_time_report_obtener_dashboard_general() RETURNS TABLE(total_proyectos_activos bigint, total_empleados_activos bigint, total_clientes_activos bigint, horas_registradas_mes numeric, proyectos_en_curso bigint)
    LANGUAGE plpgsql
    AS $$
BEGIN
    RETURN QUERY
    SELECT
        (SELECT COUNT(*) FROM time_report.tbl_time_report_proyecto WHERE Activo=TRUE),
        (SELECT COUNT(*) FROM administracion.tbl_administracion_empleado WHERE Activo=TRUE),
        (SELECT COUNT(*) FROM administracion.tbl_administracion_cliente WHERE Activo=TRUE),
        (SELECT COALESCE(SUM(CantidadHoras),0)
         FROM time_report.tbl_time_report_actividad_diaria
         WHERE EXTRACT(MONTH FROM FechaActividad)=EXTRACT(MONTH FROM NOW())
           AND EXTRACT(YEAR  FROM FechaActividad)=EXTRACT(YEAR  FROM NOW())
           AND Activo=TRUE),
        (SELECT COUNT(*) FROM time_report.tbl_time_report_proyecto p
         JOIN administracion.tbl_administracion_catalogo_detalle ep ON p.IdEstadoProyecto=ep.Id
         WHERE ep.CodigoValor='EN_CURSO' AND p.Activo=TRUE);
END; $$;


CREATE FUNCTION time_report.fn_time_report_obtener_proyeccion_por_grupo(p_id_grupo uuid) RETURNS TABLE(id_proyeccion integer, grupo_proyeccion uuid, id_tipo_recurso integer, nombre_tipo_recurso character varying, nombre_recurso character varying, nombre_proyeccion character varying, costo_por_hora numeric, cantidad_recurso integer, distribucion_tiempo text, tiempo_total numeric, costo_recurso numeric, porcentaje_participacion numeric, tipo_periodo boolean, cantidad_periodo integer)
    LANGUAGE plpgsql
    AS $$
BEGIN
    IF p_id_grupo IS NULL THEN
        RAISE EXCEPTION 'El parámetro p_id_grupo no puede ser NULL.';
    END IF;
    RETURN QUERY
    SELECT ph.Id, ph.GrupoProyeccion, ph.IdTipoRecurso,
           c.NombreCargo::VARCHAR, ph.NombreRecurso, ph.NombreProyeccion,
           ph.CostoPorHora, ph.CantidadRecurso, ph.DistribucionTiempo,
           ph.TiempoTotal, ph.CostoRecurso, ph.PorcentajeParticipacion,
           ph.TipoPeriodo, ph.CantidadPeriodo
    FROM time_report.tbl_time_report_proyeccion_horas ph
    JOIN administracion.tbl_administracion_cargo c ON ph.IdTipoRecurso=c.Id
    WHERE ph.GrupoProyeccion=p_id_grupo ORDER BY c.NombreCargo;
END; $$;


CREATE FUNCTION time_report.fn_time_report_obtener_proyeccion_por_proyecto(p_id_proyecto integer) RETURNS TABLE(id_proyeccion integer, id_tipo_recurso integer, nombre_tipo_recurso character varying, nombre_recurso character varying, costo_por_hora numeric, cantidad_recurso integer, cantidad_periodo integer, distribucion_tiempo text, tiempo_total numeric, costo_recurso numeric, porcentaje_participacion numeric, tipo_periodo boolean, id_proyecto integer)
    LANGUAGE plpgsql
    AS $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM time_report.tbl_time_report_proyecto WHERE Id=p_id_proyecto AND Activo=TRUE) THEN
        RAISE EXCEPTION 'El proyecto % no existe o no está activo.', p_id_proyecto;
    END IF;
    RETURN QUERY
    SELECT php.Id, php.IdTipoRecurso, c.NombreCargo::VARCHAR, php.NombreRecurso,
           php.CostoPorHora, php.CantidadRecurso, php.CantidadPeriodo,
           php.DistribucionTiempo, php.TiempoTotal,
           CAST(php.TiempoTotal*php.CostoPorHora AS NUMERIC(10,2)),
           php.PorcentajeParticipacion, php.TipoPeriodo, php.IdProyecto
    FROM time_report.tbl_time_report_proyeccion_horas_proyecto php
    LEFT JOIN administracion.tbl_administracion_cargo c ON php.IdTipoRecurso=c.Id
    WHERE php.IdProyecto=p_id_proyecto AND php.Activo=TRUE
    ORDER BY php.CostoRecurso DESC, php.NombreRecurso;
END; $$;


CREATE FUNCTION time_report.fn_time_report_obtener_recursos_por_cliente() RETURNS TABLE(cliente character varying, mes_numero integer, anio integer, cantidad_recursos bigint, total_horas numeric)
    LANGUAGE plpgsql
    AS $$
BEGIN
    RETURN QUERY
    SELECT COALESCE(c.NombreComercial,c.RazonSocial)::VARCHAR,
           EXTRACT(MONTH FROM da.FechaActividad)::INTEGER,
           EXTRACT(YEAR  FROM da.FechaActividad)::INTEGER,
           COUNT(DISTINCT e.Id), SUM(da.CantidadHoras)
    FROM time_report.tbl_time_report_actividad_diaria da
    JOIN administracion.tbl_administracion_empleado  e  ON da.IdEmpleado=e.Id
    JOIN time_report.tbl_time_report_proyecto        p  ON da.IdProyecto=p.Id
    JOIN administracion.tbl_administracion_cliente   c  ON p.IdCliente=c.Id
    WHERE p.Activo=TRUE AND c.Activo=TRUE
    GROUP BY COALESCE(c.NombreComercial,c.RazonSocial),
             EXTRACT(MONTH FROM da.FechaActividad), EXTRACT(YEAR FROM da.FechaActividad)
    ORDER BY 3 DESC, 2 DESC;
END; $$;


CREATE FUNCTION time_report.fn_time_report_obtener_resumen_proyectos() RETURNS TABLE(nombre_cliente character varying, nombre_proyecto character varying, estado_proyecto character varying, lider_proyecto text, total_recursos bigint, horas_registradas numeric, presupuesto numeric)
    LANGUAGE plpgsql
    AS $$
BEGIN
    RETURN QUERY
    SELECT COALESCE(c.NombreComercial,c.RazonSocial)::VARCHAR, p.Nombre::VARCHAR,
           ep.Valor::VARCHAR,
           (pers.Nombres||' '||pers.Apellidos)::TEXT,
           COUNT(DISTINCT ep2.IdEmpleado),
           COALESCE(SUM(da.CantidadHoras),0), p.Presupuesto
    FROM time_report.tbl_time_report_proyecto p
    LEFT JOIN administracion.tbl_administracion_cliente                c    ON p.IdCliente=c.Id
    LEFT JOIN administracion.tbl_administracion_catalogo_detalle       ep   ON p.IdEstadoProyecto=ep.Id
    LEFT JOIN administracion.tbl_administracion_lider                  l    ON p.IdLider=l.Id
    LEFT JOIN time_report.tbl_time_report_empleado_proyecto            ep2  ON p.Id=ep2.IdProyecto AND ep2.Activo=TRUE
    LEFT JOIN time_report.tbl_time_report_actividad_diaria             da   ON p.Id=da.IdProyecto  AND da.Activo=TRUE
    LEFT JOIN administracion.tbl_administracion_empleado               e    ON ep2.IdEmpleado=e.Id  AND e.Activo=TRUE
    LEFT JOIN administracion.tbl_administracion_persona                pers ON e.IdPersona=pers.Id
    WHERE p.Activo=TRUE
    GROUP BY COALESCE(c.NombreComercial,c.RazonSocial), p.Nombre,
             ep.Valor, pers.Nombres, pers.Apellidos, p.Presupuesto
    ORDER BY 1,2;
END; $$;


CREATE FUNCTION time_report.fn_time_report_quitar_tildes(p_texto text) RETURNS text
    LANGUAGE plpgsql IMMUTABLE
    AS $$
BEGIN
    RETURN translate(lower(p_texto),
        'áéíóúàèìòùäëïöüâêîôûãõñç',
        'aeiouaeiouaeiouaeiouaonc');
END; $$;


CREATE FUNCTION time_report.fn_time_report_recursos_pendientes_reporte(p_mes integer DEFAULT NULL::integer, p_anio integer DEFAULT NULL::integer, p_mes_completo boolean DEFAULT false) RETURNS TABLE(nombre_empleado text, cedula character varying, dias_habiles bigint, dias_registrados bigint, horas_registradas numeric, estado_reporte text)
    LANGUAGE plpgsql
    AS $$
DECLARE
    v_mes    INTEGER := COALESCE(p_mes,  EXTRACT(MONTH FROM NOW())::INTEGER);
    v_anio   INTEGER := COALESCE(p_anio, EXTRACT(YEAR  FROM NOW())::INTEGER);
    v_inicio DATE;
    v_fin    DATE;
BEGIN
    IF v_mes<1 OR v_mes>12 OR v_anio<2000 OR v_anio>2100 THEN
        RAISE EXCEPTION 'Parámetros inválidos. Mes 1-12, Año 2000-2100.';
    END IF;
    v_inicio := MAKE_DATE(v_anio, v_mes, 1);
    v_fin    := CASE WHEN p_mes_completo
                     THEN (DATE_TRUNC('MONTH',v_inicio)+INTERVAL '1 MONTH - 1 day')::DATE
                     ELSE MAKE_DATE(v_anio,v_mes,15) END;
    RETURN QUERY
    WITH habiles AS (
        SELECT COUNT(*) AS total
        FROM generate_series(v_inicio, v_fin, INTERVAL '1 day') d(fecha)
        WHERE EXTRACT(DOW FROM d.fecha) NOT IN (0,6)
          AND NOT EXISTS (SELECT 1 FROM time_report.tbl_time_report_feriado f
                          WHERE f.FechaFeriado=d.fecha::DATE AND f.Activo=TRUE)
    ),
    resumen AS (
        SELECT e.Id,
               (p.Nombres||' '||p.Apellidos)::TEXT AS nombre,
               p.NumeroIdentificacion,
               COUNT(DISTINCT da.FechaActividad)              AS dias_reg,
               COALESCE(SUM(da.CantidadHoras),0)              AS horas_reg
        FROM administracion.tbl_administracion_empleado e
        JOIN administracion.tbl_administracion_persona  p ON e.IdPersona=p.Id
        LEFT JOIN time_report.tbl_time_report_actividad_diaria da
               ON da.IdEmpleado=e.Id AND da.FechaActividad BETWEEN v_inicio AND v_fin AND da.Activo=TRUE
        WHERE e.Activo=TRUE
        GROUP BY e.Id, p.Nombres, p.Apellidos, p.NumeroIdentificacion
    )
    SELECT r.nombre, r.NumeroIdentificacion, h.total, r.dias_reg, r.horas_reg,
           CASE WHEN r.dias_reg>=h.total THEN 'COMPLETO'
                WHEN r.dias_reg>0        THEN 'PARCIAL'
                ELSE 'PENDIENTE' END::TEXT
    FROM resumen r, habiles h ORDER BY 6 DESC, r.nombre;
END; $$;


CREATE FUNCTION time_report.fn_time_report_reporte_recursos_por_proyecto() RETURNS TABLE(nombre_cliente character varying, fecha_inicio date, lider_proyecto text, nombre_recurso text, cargo character varying)
    LANGUAGE plpgsql
    AS $$
BEGIN
    RETURN QUERY
    SELECT COALESCE(c.NombreComercial,c.RazonSocial)::VARCHAR,
           p.FechaInicioPlaneada,
           COALESCE(lpers.Nombres||' '||lpers.Apellidos, 'Sin líder')::TEXT,
           (pers.Nombres||' '||pers.Apellidos)::TEXT,
           car.NombreCargo::VARCHAR
    FROM time_report.tbl_time_report_proyecto p
    JOIN administracion.tbl_administracion_cliente             c     ON p.IdCliente=c.Id
    LEFT JOIN administracion.tbl_administracion_lider          l     ON p.IdLider=l.Id
    LEFT JOIN administracion.tbl_administracion_persona        lpers ON l.IdPersona=lpers.Id
    JOIN time_report.tbl_time_report_empleado_proyecto         ep    ON ep.IdProyecto=p.Id  AND ep.Activo=TRUE
    JOIN administracion.tbl_administracion_empleado            e     ON ep.IdEmpleado=e.Id  AND e.Activo=TRUE
    JOIN administracion.tbl_administracion_cargo               car   ON e.IdCargo=car.Id
    JOIN administracion.tbl_administracion_persona             pers  ON e.IdPersona=pers.Id
    WHERE p.Activo=TRUE ORDER BY pers.Nombres, pers.Apellidos, p.Nombre;
END; $$;


CREATE FUNCTION time_report.fn_time_report_resumen_clientes_horas() RETURNS TABLE(cliente character varying, mes_numero integer, anio integer, total_recursos bigint, total_horas numeric)
    LANGUAGE plpgsql
    AS $$
BEGIN
    RETURN QUERY
    WITH act AS (
        SELECT da.IdProyecto, da.IdEmpleado, da.CantidadHoras,
               EXTRACT(YEAR  FROM da.FechaActividad)::INTEGER AS anio,
               EXTRACT(MONTH FROM da.FechaActividad)::INTEGER AS mes
        FROM time_report.tbl_time_report_actividad_diaria da WHERE da.Activo=TRUE
    ),
    cli_base AS (
        SELECT c.Id, COALESCE(c.NombreComercial,c.RazonSocial)::VARCHAR AS nombre,
               COUNT(DISTINCT ep.IdEmpleado) AS recursos
        FROM administracion.tbl_administracion_cliente c
        JOIN time_report.tbl_time_report_proyecto p ON c.Id=p.IdCliente AND p.Activo=TRUE
        JOIN time_report.tbl_time_report_empleado_proyecto ep ON p.Id=ep.IdProyecto AND ep.Activo=TRUE
        WHERE c.Activo=TRUE GROUP BY c.Id, COALESCE(c.NombreComercial,c.RazonSocial)
    )
    SELECT cb.nombre, per.mes, per.anio, cb.recursos,
           COALESCE(SUM(a.CantidadHoras),0)
    FROM cli_base cb
    CROSS JOIN (SELECT DISTINCT anio, mes FROM act) per(anio,mes)
    LEFT JOIN act a ON a.anio=per.anio AND a.mes=per.mes
    LEFT JOIN time_report.tbl_time_report_proyecto p ON a.IdProyecto=p.Id AND p.IdCliente=cb.Id
    GROUP BY cb.nombre, per.mes, per.anio, cb.recursos
    ORDER BY per.anio DESC, per.mes DESC, cb.nombre;
END; $$;


CREATE PROCEDURE time_report.sp_ins_time_report_homologacion_banco()
    LANGUAGE plpgsql
    AS $$
DECLARE
    v_count INTEGER;
BEGIN
    INSERT INTO time_report.tbl_time_report_homologacion_banco
        (IdEmpleado, NombreCompletoTR, CedulaTR, ProyectoTR, ClienteTR, NombreCompletoBanco, Observacion)
    SELECT DISTINCT e.Id,
           (pers.Nombres||' '||pers.Apellidos),
           pers.NumeroIdentificacion, p.Nombre,
           COALESCE(c.NombreComercial,c.RazonSocial),
           NULL::VARCHAR(200), 'Pendiente homologación manual'
    FROM administracion.tbl_administracion_empleado e
    JOIN administracion.tbl_administracion_persona         pers ON e.IdPersona=pers.Id
    JOIN time_report.tbl_time_report_empleado_proyecto     ep   ON ep.IdEmpleado=e.Id AND ep.Activo=TRUE
    JOIN time_report.tbl_time_report_proyecto              p    ON ep.IdProyecto=p.Id AND p.Activo=TRUE
    LEFT JOIN administracion.tbl_administracion_cliente    c    ON p.IdCliente=c.Id
    WHERE e.Activo=TRUE
    ON CONFLICT (IdEmpleado, ProyectoTR) DO UPDATE SET
        NombreCompletoTR = EXCLUDED.NombreCompletoTR,
        CedulaTR         = EXCLUDED.CedulaTR,
        ClienteTR        = EXCLUDED.ClienteTR;
    GET DIAGNOSTICS v_count = ROW_COUNT;
    RAISE NOTICE 'sp_ins_time_report_homologacion_banco: % registros procesados.', v_count;
EXCEPTION WHEN OTHERS THEN
    RAISE EXCEPTION 'Error en sp_ins_time_report_homologacion_banco: % — %', SQLSTATE, SQLERRM;
END; $$;


CREATE TABLE administracion.tbl_administracion_apariencia (
    id integer NOT NULL,
    fondologin character varying(25) NOT NULL,
    tipografia character varying(25) NOT NULL,
    encabezadofijo boolean NOT NULL,
    posicionmenu character varying(25) NOT NULL,
    menucolapsado boolean NOT NULL,
    colorfondo character varying(25) NOT NULL,
    bordercaja integer NOT NULL,
    fondocaja character varying(25) NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    usuariocreacion character varying(50) NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    usuariomodificacion character varying(50),
    fechamodificacion timestamp with time zone,
    ipcreacion character varying(45) NOT NULL,
    ipmodificacion character varying(45)
);


ALTER TABLE administracion.tbl_administracion_apariencia ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME administracion.tbl_administracion_apariencia_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE administracion.tbl_administracion_cargo (
    id integer NOT NULL,
    iddepartamento integer,
    nombrecargo character varying(100) NOT NULL,
    descripcion text,
    activo boolean DEFAULT true NOT NULL,
    usuariocreacion character varying(50) NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    usuariomodificacion character varying(50),
    fechamodificacion timestamp with time zone,
    ipcreacion character varying(45) NOT NULL,
    ipmodificacion character varying(45)
);


ALTER TABLE administracion.tbl_administracion_cargo ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME administracion.tbl_administracion_cargo_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE administracion.tbl_administracion_catalogo (
    id integer NOT NULL,
    tipocatalogo character varying(3) NOT NULL,
    codigo character varying(3) NOT NULL,
    descripcion character varying(255),
    activo boolean DEFAULT true NOT NULL,
    usuariocreacion character varying(50) NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    usuariomodificacion character varying(50),
    fechamodificacion timestamp with time zone,
    ipcreacion character varying(45) NOT NULL,
    ipmodificacion character varying(45),
    CONSTRAINT tbl_administracion_catalogo_tipocatalogo_check CHECK (((tipocatalogo)::text = ANY (ARRAY[('INV'::character varying)::text, ('TMR'::character varying)::text, ('AUT'::character varying)::text, ('AUD'::character varying)::text, ('ADM'::character varying)::text])))
);


CREATE TABLE administracion.tbl_administracion_catalogo_detalle (
    id integer NOT NULL,
    idcatalogo integer NOT NULL,
    codigovalor character varying(5) NOT NULL,
    valor character varying(100) NOT NULL,
    descripcion character varying(255),
    orden smallint,
    valorextra character varying(100),
    activo boolean DEFAULT true NOT NULL,
    usuariocreacion character varying(50) NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    usuariomodificacion character varying(50),
    fechamodificacion timestamp with time zone,
    ipcreacion character varying(45) NOT NULL,
    ipmodificacion character varying(45)
);


ALTER TABLE administracion.tbl_administracion_catalogo_detalle ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME administracion.tbl_administracion_catalogo_detalle_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


ALTER TABLE administracion.tbl_administracion_catalogo ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME administracion.tbl_administracion_catalogo_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE administracion.tbl_administracion_cliente (
    id integer NOT NULL,
    numeroidentificacion character varying(20) NOT NULL,
    idtipoidentificacion integer,
    nombrecomercial character varying(100),
    razonsocial character varying(150),
    nombres character varying(150),
    apellidos character varying(150),
    email character varying(100),
    telefono character varying(20),
    direccion character varying(255),
    activo boolean DEFAULT true NOT NULL,
    usuariocreacion character varying(50) NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    usuariomodificacion character varying(50),
    fechamodificacion timestamp with time zone,
    ipcreacion character varying(45) NOT NULL,
    ipmodificacion character varying(45)
);


ALTER TABLE administracion.tbl_administracion_cliente ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME administracion.tbl_administracion_cliente_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE administracion.tbl_administracion_cliente_usuario (
    id integer NOT NULL,
    idcliente integer NOT NULL,
    idusuario integer NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    usuariocreacion character varying(50) NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    usuariomodificacion character varying(50),
    fechamodificacion timestamp with time zone,
    ipcreacion character varying(45) NOT NULL,
    ipmodificacion character varying(45)
);


ALTER TABLE administracion.tbl_administracion_cliente_usuario ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME administracion.tbl_administracion_cliente_usuario_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE administracion.tbl_administracion_empleado (
    id integer NOT NULL,
    idpersona integer NOT NULL,
    idcargo integer,
    idmodotrabajo integer,
    idcategoriaempleado integer,
    idempresacatalogo integer,
    aniosexperiencia integer,
    codigoempleado character varying(20) NOT NULL,
    fechaingreso date,
    fechaterminacion date,
    idtipocontrato integer,
    emailcorporativo character varying(100),
    salario numeric(12,2),
    activo boolean DEFAULT true NOT NULL,
    usuariocreacion character varying(50) NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    usuariomodificacion character varying(50),
    fechamodificacion timestamp with time zone,
    ipcreacion character varying(45) NOT NULL,
    ipmodificacion character varying(45),
    idempleadoreemplazo integer,
    idtiposalida integer,
    idcausasalida integer,
    comentariosalida text
);


CREATE TABLE administracion.tbl_administracion_empleado_contrato (
    id integer NOT NULL,
    idempleado integer NOT NULL,
    idtipocontrato integer NOT NULL,
    fechadesde date NOT NULL,
    fechahasta date,
    activo boolean DEFAULT true NOT NULL,
    usuariocreacion character varying(50) NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    ipcreacion character varying(45) NOT NULL,
    usuariomodificacion character varying(50),
    fechamodificacion timestamp with time zone,
    ipmodificacion character varying(45),
    CONSTRAINT ck_empleado_contrato_fechas CHECK (((fechahasta IS NULL) OR (fechahasta >= fechadesde)))
);


ALTER TABLE administracion.tbl_administracion_empleado_contrato ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME administracion.tbl_administracion_empleado_contrato_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


ALTER TABLE administracion.tbl_administracion_empleado ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME administracion.tbl_administracion_empleado_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE administracion.tbl_administracion_lider (
    id integer NOT NULL,
    idpersona integer NOT NULL,
    idtipo integer,
    activo boolean DEFAULT true NOT NULL,
    usuariocreacion character varying(50) NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    usuariomodificacion character varying(50),
    fechamodificacion timestamp with time zone,
    ipcreacion character varying(45) NOT NULL,
    ipmodificacion character varying(45)
);


ALTER TABLE administracion.tbl_administracion_lider ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME administracion.tbl_administracion_lider_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE administracion.tbl_administracion_persona (
    id integer NOT NULL,
    numeroidentificacion character varying(20) NOT NULL,
    idtipoidentificacion integer,
    idgenero integer,
    idnacionalidad integer,
    tipopersona character varying(10) NOT NULL,
    nombres character varying(100) NOT NULL,
    apellidos character varying(100) NOT NULL,
    fechanacimiento date,
    email character varying(100),
    telefono character varying(20),
    direccion character varying(255),
    activo boolean DEFAULT true NOT NULL,
    usuariocreacion character varying(50) NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    usuariomodificacion character varying(50),
    fechamodificacion timestamp with time zone,
    ipcreacion character varying(45) NOT NULL,
    ipmodificacion character varying(45),
    CONSTRAINT tbl_administracion_persona_tipopersona_check CHECK (((tipopersona)::text = ANY (ARRAY[('NATURAL'::character varying)::text, ('JURIDICA'::character varying)::text])))
);


ALTER TABLE administracion.tbl_administracion_persona ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME administracion.tbl_administracion_persona_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE administracion.tbl_administracion_registro_asignacion (
    id integer NOT NULL,
    idempleado integer,
    fecharegistro timestamp with time zone,
    descripcion text,
    activo boolean DEFAULT true NOT NULL,
    usuariocreacion character varying(50) NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    usuariomodificacion character varying(50),
    fechamodificacion timestamp with time zone,
    ipcreacion character varying(45) NOT NULL,
    ipmodificacion character varying(45)
);


ALTER TABLE administracion.tbl_administracion_registro_asignacion ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME administracion.tbl_administracion_registro_asignacion_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE auditoria.tbl_auditoria_asignacion_equipo (
    id bigint NOT NULL,
    idasignacion integer NOT NULL,
    tipocambio character varying(10) NOT NULL,
    camposmodificados jsonb,
    valoresanteriores jsonb,
    valoresnuevos jsonb,
    cambiadopor character varying(50),
    direccionip character varying(45),
    agenteusuario character varying(255),
    usuariocreacion character varying(50),
    ipcreacion character varying(45),
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT tbl_auditoria_asignacion_equipo_tipocambio_check CHECK (((tipocambio)::text = ANY (ARRAY[('INSERT'::character varying)::text, ('UPDATE'::character varying)::text, ('DELETE'::character varying)::text])))
);


ALTER TABLE auditoria.tbl_auditoria_asignacion_equipo ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME auditoria.tbl_auditoria_asignacion_equipo_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE auditoria.tbl_auditoria_empleado (
    id bigint NOT NULL,
    idempleado integer NOT NULL,
    tipocambio character varying(10) NOT NULL,
    camposmodificados jsonb,
    valoresanteriores jsonb,
    valoresnuevos jsonb,
    cambiadopor character varying(50),
    direccionip character varying(45),
    agenteusuario character varying(255),
    usuariocreacion character varying(50),
    ipcreacion character varying(45),
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT tbl_auditoria_empleado_tipocambio_check CHECK (((tipocambio)::text = ANY (ARRAY[('INSERT'::character varying)::text, ('UPDATE'::character varying)::text, ('DELETE'::character varying)::text])))
);


ALTER TABLE auditoria.tbl_auditoria_empleado ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME auditoria.tbl_auditoria_empleado_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE auditoria.tbl_auditoria_equipo (
    id bigint NOT NULL,
    idequipo integer NOT NULL,
    tipocambio character varying(10) NOT NULL,
    camposmodificados jsonb,
    valoresanteriores jsonb,
    valoresnuevos jsonb,
    cambiadopor character varying(50),
    direccionip character varying(45),
    agenteusuario character varying(255),
    usuariocreacion character varying(50),
    ipcreacion character varying(45),
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT tbl_auditoria_equipo_tipocambio_check CHECK (((tipocambio)::text = ANY (ARRAY[('INSERT'::character varying)::text, ('UPDATE'::character varying)::text, ('DELETE'::character varying)::text])))
);


ALTER TABLE auditoria.tbl_auditoria_equipo ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME auditoria.tbl_auditoria_equipo_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE auditoria.tbl_auditoria_historico_general (
    id bigint NOT NULL,
    nombretabla character varying(100) NOT NULL,
    idregistro character varying(50) NOT NULL,
    tipooperacion character varying(10) NOT NULL,
    datosanteriores jsonb,
    datosnuevos jsonb,
    cambiadopor character varying(50),
    fechacambio timestamp with time zone DEFAULT now() NOT NULL,
    direccionip character varying(45),
    CONSTRAINT tbl_auditoria_historico_general_tipooperacion_check CHECK (((tipooperacion)::text = ANY (ARRAY[('INSERT'::character varying)::text, ('UPDATE'::character varying)::text, ('DELETE'::character varying)::text])))
);


ALTER TABLE auditoria.tbl_auditoria_historico_general ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME auditoria.tbl_auditoria_historico_general_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE auditoria.tbl_auditoria_sesion_usuario (
    id bigint NOT NULL,
    idusuario integer NOT NULL,
    tipocambio character varying(10) NOT NULL,
    direccionip character varying(45),
    agenteusuario character varying(255),
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT tbl_auditoria_sesion_usuario_tipocambio_check CHECK (((tipocambio)::text = ANY (ARRAY[('LOGIN'::character varying)::text, ('LOGOUT'::character varying)::text, ('FAILED'::character varying)::text])))
);


ALTER TABLE auditoria.tbl_auditoria_sesion_usuario ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME auditoria.tbl_auditoria_sesion_usuario_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE autenticacion.tbl_autenticacion_audit_log (
    id bigint NOT NULL,
    idusuario integer,
    evento autenticacion.audit_evento_enum NOT NULL,
    direccionip character varying(45),
    agenteusuario text,
    detalles jsonb,
    usuariocreacion character varying(50) NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL
);


ALTER TABLE autenticacion.tbl_autenticacion_audit_log ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME autenticacion.tbl_autenticacion_audit_log_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE autenticacion.tbl_autenticacion_modulo (
    id integer NOT NULL,
    nombremodulo character varying(100) NOT NULL,
    rutamodulo character varying(255),
    icono character varying(50),
    ordenvisualizacion integer,
    idmodulopadre integer,
    activo boolean DEFAULT true NOT NULL,
    usuariocreacion character varying(50) NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    usuariomodificacion character varying(50),
    fechamodificacion timestamp with time zone,
    ipcreacion character varying(45) NOT NULL,
    ipmodificacion character varying(45)
);


ALTER TABLE autenticacion.tbl_autenticacion_modulo ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME autenticacion.tbl_autenticacion_modulo_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE autenticacion.tbl_autenticacion_password_historial (
    id integer NOT NULL,
    idusuario integer NOT NULL,
    hashpassword character varying(255) NOT NULL,
    fechacambio timestamp with time zone DEFAULT now() NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    usuariocreacion character varying(50) NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    ipcreacion character varying(45) NOT NULL
);


ALTER TABLE autenticacion.tbl_autenticacion_password_historial ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME autenticacion.tbl_autenticacion_password_historial_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE autenticacion.tbl_autenticacion_password_reset (
    id integer NOT NULL,
    idusuario integer NOT NULL,
    tokenhash character varying(255) NOT NULL,
    fechaexpiracion timestamp with time zone NOT NULL,
    utilizado boolean DEFAULT false NOT NULL,
    fechautilizacion timestamp with time zone,
    activo boolean DEFAULT true NOT NULL,
    usuariocreacion character varying(50) NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    ipcreacion character varying(45) NOT NULL
);


COMMENT ON TABLE autenticacion.tbl_autenticacion_password_reset IS 'Tokens temporales para restablecimiento de contraseña con expiración de 30 minutos';


COMMENT ON COLUMN autenticacion.tbl_autenticacion_password_reset.tokenhash IS 'Hash del token aleatorio (no almacenar en plaintext)';


COMMENT ON COLUMN autenticacion.tbl_autenticacion_password_reset.fechaexpiracion IS 'Marca de tiempo de expiración del token (30 minutos desde creación)';


COMMENT ON COLUMN autenticacion.tbl_autenticacion_password_reset.utilizado IS 'Flag para marcar si el token ya fue utilizado (one-time use)';


ALTER TABLE autenticacion.tbl_autenticacion_password_reset ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME autenticacion.tbl_autenticacion_password_reset_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE autenticacion.tbl_autenticacion_permiso (
    id integer NOT NULL,
    idmodulo integer NOT NULL,
    codigo character varying(100) NOT NULL,
    accion character varying(40) NOT NULL,
    descripcion text NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    usuariocreacion character varying(50) NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    usuariomodificacion character varying(50),
    fechamodificacion timestamp with time zone,
    ipcreacion character varying(45) NOT NULL,
    ipmodificacion character varying(45)
);


ALTER TABLE autenticacion.tbl_autenticacion_permiso ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME autenticacion.tbl_autenticacion_permiso_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE autenticacion.tbl_autenticacion_pregunta_usuario (
    id integer NOT NULL,
    idusuario integer NOT NULL,
    pregunta character varying(255) NOT NULL,
    respuesta character varying(255) NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    usuariocreacion character varying(50) NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    usuariomodificacion character varying(50),
    fechamodificacion timestamp with time zone,
    ipcreacion character varying(45) NOT NULL,
    ipmodificacion character varying(45)
);


ALTER TABLE autenticacion.tbl_autenticacion_pregunta_usuario ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME autenticacion.tbl_autenticacion_pregunta_usuario_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE autenticacion.tbl_autenticacion_refresh_token (
    id bigint NOT NULL,
    idsesion bigint NOT NULL,
    idusuario integer NOT NULL,
    tokenhash text NOT NULL,
    familiatoken uuid NOT NULL,
    estausado boolean DEFAULT false NOT NULL,
    estarevocado boolean DEFAULT false NOT NULL,
    fechaexpiracion timestamp with time zone NOT NULL,
    fechausado timestamp with time zone,
    activo boolean DEFAULT true NOT NULL,
    usuariocreacion character varying(50) NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL
);


ALTER TABLE autenticacion.tbl_autenticacion_refresh_token ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME autenticacion.tbl_autenticacion_refresh_token_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE autenticacion.tbl_autenticacion_rol (
    id integer NOT NULL,
    nombre character varying(50) NOT NULL,
    descripcion text NOT NULL,
    essistema boolean DEFAULT false NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    usuariocreacion character varying(50) NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    usuariomodificacion character varying(50),
    fechamodificacion timestamp with time zone,
    ipcreacion character varying(45) NOT NULL,
    ipmodificacion character varying(45)
);


ALTER TABLE autenticacion.tbl_autenticacion_rol ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME autenticacion.tbl_autenticacion_rol_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE autenticacion.tbl_autenticacion_rol_permiso (
    idrol integer NOT NULL,
    idpermiso integer NOT NULL,
    otorgado timestamp with time zone DEFAULT now() NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    usuariocreacion character varying(50) NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    ipcreacion character varying(45) NOT NULL
);


CREATE TABLE autenticacion.tbl_autenticacion_sesion (
    id bigint NOT NULL,
    idusuario integer NOT NULL,
    dispositivoinfo character varying(255),
    direccionip character varying(45),
    agenteusuario text,
    ubicacioninfo character varying(255),
    estaactiva boolean DEFAULT true NOT NULL,
    ultimaactividad timestamp with time zone DEFAULT now() NOT NULL,
    revocadofecha timestamp with time zone,
    revocadorazon character varying(50),
    activo boolean DEFAULT true NOT NULL,
    fechaexpiracion timestamp with time zone NOT NULL,
    usuariocreacion character varying(50) NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    usuariomodificacion character varying(50),
    fechamodificacion timestamp with time zone,
    ipcreacion character varying(45) NOT NULL,
    ipmodificacion character varying(45)
);


ALTER TABLE autenticacion.tbl_autenticacion_sesion ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME autenticacion.tbl_autenticacion_sesion_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE autenticacion.tbl_autenticacion_token_blacklist (
    jti text NOT NULL,
    idusuario integer,
    fecharevocado timestamp with time zone DEFAULT now() NOT NULL,
    fechaexpiracion timestamp with time zone,
    activo boolean DEFAULT true NOT NULL,
    usuariocreacion character varying(50) NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL
);


CREATE TABLE autenticacion.tbl_autenticacion_usuario (
    id integer NOT NULL,
    idpersona integer,
    email character varying(255) NOT NULL,
    hashpassword text NOT NULL,
    ultimologin timestamp with time zone,
    emailverificado boolean DEFAULT false NOT NULL,
    intentosfallidos smallint DEFAULT 0 NOT NULL,
    bloqueadohasta timestamp with time zone,
    debecambiarpassword boolean DEFAULT false NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    usuariocreacion character varying(50) NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    usuariomodificacion character varying(50),
    fechamodificacion timestamp with time zone,
    ipcreacion character varying(45) NOT NULL,
    ipmodificacion character varying(45)
);


ALTER TABLE autenticacion.tbl_autenticacion_usuario ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME autenticacion.tbl_autenticacion_usuario_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE autenticacion.tbl_autenticacion_usuario_rol (
    idusuario integer NOT NULL,
    idrol integer NOT NULL,
    asignadopor integer,
    asignadoen timestamp with time zone DEFAULT now() NOT NULL,
    fechaexpiracion timestamp with time zone,
    activo boolean DEFAULT true NOT NULL,
    usuariocreacion character varying(50) NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    ipcreacion character varying(45) NOT NULL
);


CREATE TABLE inventario.tbl_inventario_asignacion_equipo (
    id integer NOT NULL,
    idequipo integer NOT NULL,
    idempleado integer NOT NULL,
    fechaasignacion timestamp with time zone DEFAULT now() NOT NULL,
    fechadevolucion timestamp with time zone,
    observacion character varying(500),
    activo boolean DEFAULT true NOT NULL,
    usuariocreacion character varying(50) NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    usuariomodificacion character varying(50),
    fechamodificacion timestamp with time zone,
    ipcreacion character varying(45) NOT NULL,
    ipmodificacion character varying(45)
);


ALTER TABLE inventario.tbl_inventario_asignacion_equipo ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME inventario.tbl_inventario_asignacion_equipo_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE inventario.tbl_inventario_baja_equipo (
    id integer NOT NULL,
    idequipo integer NOT NULL,
    idtipobaja integer,
    fechabaja date NOT NULL,
    motivobaja character varying(500),
    activo boolean DEFAULT true NOT NULL,
    usuariocreacion character varying(50) NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    usuariomodificacion character varying(50),
    fechamodificacion timestamp with time zone,
    ipcreacion character varying(45) NOT NULL,
    ipmodificacion character varying(45)
);


ALTER TABLE inventario.tbl_inventario_baja_equipo ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME inventario.tbl_inventario_baja_equipo_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE inventario.tbl_inventario_caracteristica_equipo (
    id integer NOT NULL,
    idequipo integer NOT NULL,
    idtipocomponente integer,
    nombrecaracteristica character varying(100) CONSTRAINT tbl_inventario_caracteristica_equ_nombrecaracteristica_not_null NOT NULL,
    valorcaracteristica character varying(255) CONSTRAINT tbl_inventario_caracteristica_equi_valorcaracteristica_not_null NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    usuariocreacion character varying(50) NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    usuariomodificacion character varying(50),
    fechamodificacion timestamp with time zone,
    ipcreacion character varying(45) NOT NULL,
    ipmodificacion character varying(45)
);


ALTER TABLE inventario.tbl_inventario_caracteristica_equipo ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME inventario.tbl_inventario_caracteristica_equipo_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE inventario.tbl_inventario_detalle_factura (
    id integer NOT NULL,
    idfactura integer NOT NULL,
    descripcion character varying(255),
    cantidad integer DEFAULT 1 NOT NULL,
    preciounitario numeric(18,2) DEFAULT 0 NOT NULL,
    subtotal numeric(18,2) DEFAULT 0 NOT NULL,
    iva numeric(18,2) DEFAULT 0 NOT NULL,
    total numeric(18,2) DEFAULT 0 NOT NULL,
    observacion character varying(500),
    activo boolean DEFAULT true NOT NULL,
    usuariocreacion character varying(50) NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    usuariomodificacion character varying(50),
    fechamodificacion timestamp with time zone,
    ipcreacion character varying(45) NOT NULL,
    ipmodificacion character varying(45)
);


ALTER TABLE inventario.tbl_inventario_detalle_factura ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME inventario.tbl_inventario_detalle_factura_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE inventario.tbl_inventario_empresa (
    id integer NOT NULL,
    nombreempresa character varying(100) NOT NULL,
    ruc character varying(13),
    direccion character varying(255),
    telefono character varying(20),
    email character varying(100),
    activo boolean DEFAULT true NOT NULL,
    usuariocreacion character varying(50) NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    usuariomodificacion character varying(50),
    fechamodificacion timestamp with time zone,
    ipcreacion character varying(45) NOT NULL,
    ipmodificacion character varying(45)
);


ALTER TABLE inventario.tbl_inventario_empresa ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME inventario.tbl_inventario_empresa_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE inventario.tbl_inventario_equipo (
    id integer NOT NULL,
    idcategoria integer NOT NULL,
    idestado integer NOT NULL,
    idcondicion integer,
    idtipogarantia integer,
    idfactura integer,
    marca character varying(100) NOT NULL,
    modelo character varying(100) NOT NULL,
    numeroserie character varying(100) NOT NULL,
    fechaadquisicion date,
    fechavencimientogarantia date,
    valoradquisicion numeric(18,2),
    descripcion character varying(255),
    datosadicionales jsonb,
    activo boolean DEFAULT true NOT NULL,
    usuariocreacion character varying(50) NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    usuariomodificacion character varying(50),
    fechamodificacion timestamp with time zone,
    ipcreacion character varying(45) NOT NULL,
    ipmodificacion character varying(45)
);


ALTER TABLE inventario.tbl_inventario_equipo ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME inventario.tbl_inventario_equipo_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE inventario.tbl_inventario_factura (
    id integer NOT NULL,
    numerofactura character varying(50) NOT NULL,
    idproveedor integer,
    fechafactura date NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    usuariocreacion character varying(50) NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    usuariomodificacion character varying(50),
    fechamodificacion timestamp with time zone,
    ipcreacion character varying(45) NOT NULL,
    ipmodificacion character varying(45)
);


ALTER TABLE inventario.tbl_inventario_factura ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME inventario.tbl_inventario_factura_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE inventario.tbl_inventario_proveedor (
    id integer NOT NULL,
    idtipoproveedor integer,
    nombreproveedor character varying(150) NOT NULL,
    ruc character varying(13),
    email character varying(100),
    telefono character varying(20),
    direccion character varying(255),
    activo boolean DEFAULT true NOT NULL,
    usuariocreacion character varying(50) NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    usuariomodificacion character varying(50),
    fechamodificacion timestamp with time zone,
    ipcreacion character varying(45) NOT NULL,
    ipmodificacion character varying(45)
);


ALTER TABLE inventario.tbl_inventario_proveedor ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME inventario.tbl_inventario_proveedor_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE inventario.tbl_inventario_reparacion_equipo (
    id integer NOT NULL,
    idequipo integer NOT NULL,
    fechainicio date NOT NULL,
    fechafin date,
    descripcion character varying(255),
    costoreparacion numeric(18,2),
    activo boolean DEFAULT true NOT NULL,
    usuariocreacion character varying(50) NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    usuariomodificacion character varying(50),
    fechamodificacion timestamp with time zone,
    ipcreacion character varying(45) NOT NULL,
    ipmodificacion character varying(45)
);


ALTER TABLE inventario.tbl_inventario_reparacion_equipo ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME inventario.tbl_inventario_reparacion_equipo_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE inventario.tbl_inventario_stock_categoria (
    id integer NOT NULL,
    idcategoria integer NOT NULL,
    stockminimo integer DEFAULT 0 NOT NULL,
    stockmaximo integer,
    activo boolean DEFAULT true NOT NULL,
    usuariocreacion character varying(50) NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    usuariomodificacion character varying(50),
    fechamodificacion timestamp with time zone,
    ipcreacion character varying(45) NOT NULL,
    ipmodificacion character varying(45)
);


ALTER TABLE inventario.tbl_inventario_stock_categoria ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME inventario.tbl_inventario_stock_categoria_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE inventario.tbl_inventario_tipo_proveedor (
    id integer NOT NULL,
    nombre character varying(150) NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    usuariocreacion character varying(100) DEFAULT 'MIGRACION'::character varying NOT NULL,
    usuariomodificacion character varying(100),
    fechacreacion timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    fechamodificacion timestamp with time zone
);


ALTER TABLE inventario.tbl_inventario_tipo_proveedor ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME inventario.tbl_inventario_tipo_proveedor_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE inventory.tbl_inventario_proveedor (
    id integer NOT NULL,
    business_name character varying(150) NOT NULL,
    address character varying(255),
    phone character varying(20),
    email character varying(100),
    ruc character varying(13),
    id_supplier_type integer NOT NULL,
    status boolean DEFAULT true NOT NULL,
    creation_user character varying(100),
    modification_user character varying(100),
    creation_date timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    modification_date timestamp with time zone,
    CONSTRAINT chk_tbl_inventario_proveedor_email CHECK (((email IS NULL) OR ((email)::text ~* '^[^@[:space:]]+@[^@[:space:]]+\.[^@[:space:]]+$'::text)))
);


ALTER TABLE inventory.tbl_inventario_proveedor ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME inventory.tbl_inventario_proveedor_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE inventory.tbl_inventario_tipo_proveedor (
    id integer NOT NULL,
    name character varying(150) NOT NULL,
    status boolean DEFAULT true NOT NULL,
    creation_user character varying(100),
    modification_user character varying(100),
    creation_date timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    modification_date timestamp with time zone
);


ALTER TABLE inventory.tbl_inventario_tipo_proveedor ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME inventory.tbl_inventario_tipo_proveedor_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE notificacion.evento_outbox (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    tipo_evento character varying(100) NOT NULL,
    payload jsonb NOT NULL,
    estado character varying(20) NOT NULL,
    intentos integer DEFAULT 0 NOT NULL,
    ultimo_error text,
    fecha_creacion timestamp with time zone DEFAULT now() NOT NULL,
    fecha_procesado timestamp with time zone
);


CREATE TABLE notificacion.notificacion_envio (
    id integer NOT NULL,
    id_empleado integer NOT NULL,
    inicio_periodo date NOT NULL,
    fin_periodo date NOT NULL,
    fecha_corte timestamp with time zone,
    origen character varying(20) NOT NULL,
    estado character varying(20) NOT NULL,
    horas_faltantes numeric(6,2) NOT NULL,
    id_usuario_ejecutor integer,
    fecha_envio timestamp with time zone DEFAULT now() NOT NULL,
    detalle_error text,
    id_proyecto integer
);


COMMENT ON COLUMN notificacion.notificacion_envio.id_proyecto IS 'Proyecto del aviso manual (fila de Seguimiento); NULL en automáticos y en avisos sin proyecto';


ALTER TABLE notificacion.notificacion_envio ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME notificacion.notificacion_envio_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE notificacion.notificacion_fecha_corte (
    id integer NOT NULL,
    anio integer NOT NULL,
    mes integer NOT NULL,
    tipo character varying(20) NOT NULL,
    inicio_periodo date NOT NULL,
    fin_periodo date NOT NULL,
    fecha_corte timestamp with time zone NOT NULL,
    ejecutada boolean DEFAULT false NOT NULL,
    fecha_ejecucion timestamp with time zone,
    id_usuario_creador integer NOT NULL,
    fecha_creacion timestamp with time zone DEFAULT now() NOT NULL,
    id_usuario_modificacion integer,
    fecha_modificacion timestamp with time zone,
    CONSTRAINT notificacion_fecha_corte_mes_check CHECK (((mes >= 1) AND (mes <= 12)))
);


COMMENT ON COLUMN notificacion.notificacion_fecha_corte.id_usuario_creador IS 'Usuario que creó la fila. 0 = creada automáticamente por el sistema con la fecha por defecto.';


ALTER TABLE notificacion.notificacion_fecha_corte ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME notificacion.notificacion_fecha_corte_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE notificacion.notificacion_usuario (
    id bigint NOT NULL,
    id_usuario_destino integer NOT NULL,
    id_solicitud integer,
    titulo character varying(160) NOT NULL,
    mensaje character varying(500) NOT NULL,
    url_destino character varying(255),
    leida boolean DEFAULT false NOT NULL,
    fecha_lectura timestamp with time zone,
    fecha_creacion timestamp with time zone DEFAULT now() NOT NULL
);


ALTER TABLE notificacion.notificacion_usuario ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME notificacion.notificacion_usuario_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE SEQUENCE time_report.seq_proyecto_codigo
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


CREATE TABLE time_report.tbl_time_report_actividad_diaria (
    id integer NOT NULL,
    idempleado integer NOT NULL,
    idproyecto integer,
    idtipoactividad integer NOT NULL,
    codigorequerimiento character varying(150),
    cantidadhoras numeric(5,2) NOT NULL,
    fechaactividad date NOT NULL,
    descripcionactividad text NOT NULL,
    notas text,
    esbillable boolean,
    aprobadopor integer,
    fechaaprobacion timestamp with time zone,
    activo boolean DEFAULT true NOT NULL,
    usuariocreacion character varying(50) NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    usuariomodificacion character varying(50),
    fechamodificacion timestamp with time zone,
    ipcreacion character varying(45) NOT NULL,
    ipmodificacion character varying(45),
    idliderregistro integer
);


ALTER TABLE time_report.tbl_time_report_actividad_diaria ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME time_report.tbl_time_report_actividad_diaria_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE time_report.tbl_time_report_asignacion_proyecto (
    id integer CONSTRAINT tbl_time_report_empleado_proyecto_id_not_null NOT NULL,
    idempleado integer,
    idproyecto integer CONSTRAINT tbl_time_report_empleado_proyecto_idproyecto_not_null NOT NULL,
    idproveedor integer,
    fechaasignacion date,
    fechafinasignacion date,
    rolasignado character varying(100),
    costoporhora numeric(10,2),
    horasasignadas numeric(10,2),
    activo boolean DEFAULT true CONSTRAINT tbl_time_report_empleado_proyecto_activo_not_null NOT NULL,
    usuariocreacion character varying(50) CONSTRAINT tbl_time_report_empleado_proyecto_usuariocreacion_not_null NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() CONSTRAINT tbl_time_report_empleado_proyecto_fechacreacion_not_null NOT NULL,
    usuariomodificacion character varying(50),
    fechamodificacion timestamp with time zone,
    ipcreacion character varying(45) CONSTRAINT tbl_time_report_empleado_proyecto_ipcreacion_not_null NOT NULL,
    ipmodificacion character varying(45),
    idlider integer,
    lidercosto numeric(15,2),
    liderhoras numeric(10,2),
    estadoasignacion boolean DEFAULT true NOT NULL
);


COMMENT ON COLUMN time_report.tbl_time_report_asignacion_proyecto.estadoasignacion IS 'Estado de asignación del recurso en el proyecto: true = Activo, false = Inactivo';


ALTER TABLE time_report.tbl_time_report_asignacion_proyecto ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME time_report.tbl_time_report_empleado_proyecto_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE time_report.tbl_time_report_estado_solicitud (
    id integer NOT NULL,
    codigo character varying(30) NOT NULL,
    codigo_corto character varying(5) NOT NULL,
    nombre character varying(60) NOT NULL,
    semaforo character varying(15) NOT NULL,
    orden smallint NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    CONSTRAINT ck_tr_estado_solicitud_semaforo CHECK (((semaforo)::text = ANY (ARRAY[('AMARILLO'::character varying)::text, ('VERDE'::character varying)::text, ('ROJO'::character varying)::text])))
);


ALTER TABLE time_report.tbl_time_report_estado_solicitud ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME time_report.tbl_time_report_estado_solicitud_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE time_report.tbl_time_report_feriado (
    id integer NOT NULL,
    nombreferiado character varying(150) NOT NULL,
    fechaferiado date NOT NULL,
    esrecurrente boolean,
    tipoferiado character varying(50),
    descripcion character varying(255),
    activo boolean DEFAULT true NOT NULL,
    usuariocreacion character varying(50) NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    usuariomodificacion character varying(50),
    fechamodificacion timestamp with time zone,
    ipcreacion character varying(45) NOT NULL,
    ipmodificacion character varying(45)
);


ALTER TABLE time_report.tbl_time_report_feriado ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME time_report.tbl_time_report_feriado_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE time_report.tbl_time_report_homologacion_banco (
    id integer NOT NULL,
    idempleado integer NOT NULL,
    nombrecompletotr character varying(200),
    cedulatr character varying(20),
    proyectotr character varying(200),
    clientetr character varying(200),
    nombrecompletobanco character varying(200),
    observacion character varying(100),
    fecharegistro timestamp with time zone DEFAULT now() NOT NULL
);


ALTER TABLE time_report.tbl_time_report_homologacion_banco ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME time_report.tbl_time_report_homologacion_banco_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE time_report.tbl_time_report_outbox_cargo (
    idoutbox uuid DEFAULT gen_random_uuid() NOT NULL,
    idagregado integer NOT NULL,
    operacion character(1) NOT NULL,
    payloadjson jsonb,
    intentos smallint DEFAULT 0 NOT NULL,
    proximointento timestamp with time zone DEFAULT now() NOT NULL,
    procesadoen timestamp with time zone,
    mensajeerror character varying(2000),
    creadoen timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT tbl_time_report_outbox_cargo_operacion_check CHECK ((operacion = ANY (ARRAY['I'::bpchar, 'U'::bpchar, 'D'::bpchar])))
);


CREATE TABLE time_report.tbl_time_report_permiso (
    id integer NOT NULL,
    idempleado integer NOT NULL,
    idtipopermiso integer NOT NULL,
    idestadoaprobacion integer NOT NULL,
    fechainicio date NOT NULL,
    fechafin date NOT NULL,
    totaldias numeric(6,2) NOT NULL,
    totalhoras numeric(6,2),
    espagado boolean,
    descripcion text,
    aprobadopor integer,
    fechaaprobacion timestamp with time zone,
    observacion text,
    activo boolean DEFAULT true NOT NULL,
    usuariocreacion character varying(50) NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    usuariomodificacion character varying(50),
    fechamodificacion timestamp with time zone,
    ipcreacion character varying(45) NOT NULL,
    ipmodificacion character varying(45)
);


ALTER TABLE time_report.tbl_time_report_permiso ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME time_report.tbl_time_report_permiso_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE time_report.tbl_time_report_proyeccion_horas (
    id integer NOT NULL,
    grupoproyeccion uuid,
    idtiporecurso integer NOT NULL,
    nombrerecurso character varying(255) NOT NULL,
    nombreproyeccion character varying(255) NOT NULL,
    costoporhora numeric(18,2) NOT NULL,
    cantidadrecurso integer NOT NULL,
    distribuciontiempo text NOT NULL,
    tiempototal numeric(18,2) NOT NULL,
    costorecurso numeric(18,2) NOT NULL,
    porcentajeparticipacion numeric(5,2) CONSTRAINT tbl_time_report_proyeccion_hor_porcentajeparticipacion_not_null NOT NULL,
    tipoperiodo boolean NOT NULL,
    cantidadperiodo integer NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    usuariocreacion character varying(50) NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    usuariomodificacion character varying(50),
    fechamodificacion timestamp with time zone,
    ipcreacion character varying(45) NOT NULL,
    ipmodificacion character varying(45)
);


ALTER TABLE time_report.tbl_time_report_proyeccion_horas ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME time_report.tbl_time_report_proyeccion_horas_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE time_report.tbl_time_report_proyeccion_horas_proyecto (
    id integer NOT NULL,
    idproyecto integer NOT NULL,
    idtiporecurso integer CONSTRAINT tbl_time_report_proyeccion_horas_proyect_idtiporecurso_not_null NOT NULL,
    nombrerecurso character varying(255) CONSTRAINT tbl_time_report_proyeccion_horas_proyect_nombrerecurso_not_null NOT NULL,
    costoporhora numeric(18,2) NOT NULL,
    cantidadrecurso integer CONSTRAINT tbl_time_report_proyeccion_horas_proye_cantidadrecurso_not_null NOT NULL,
    distribuciontiempo text CONSTRAINT tbl_time_report_proyeccion_horas_pr_distribuciontiempo_not_null NOT NULL,
    tiempototal numeric(18,2) NOT NULL,
    costorecurso numeric(18,2) NOT NULL,
    porcentajeparticipacion numeric(5,2) CONSTRAINT tbl_time_report_proyeccion_ho_porcentajeparticipacion_not_null1 NOT NULL,
    tipoperiodo boolean NOT NULL,
    cantidadperiodo integer CONSTRAINT tbl_time_report_proyeccion_horas_proye_cantidadperiodo_not_null NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    usuariocreacion character varying(50) CONSTRAINT tbl_time_report_proyeccion_horas_proye_usuariocreacion_not_null NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() CONSTRAINT tbl_time_report_proyeccion_horas_proyect_fechacreacion_not_null NOT NULL,
    usuariomodificacion character varying(50),
    fechamodificacion timestamp with time zone,
    ipcreacion character varying(45) NOT NULL,
    ipmodificacion character varying(45)
);


ALTER TABLE time_report.tbl_time_report_proyeccion_horas_proyecto ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME time_report.tbl_time_report_proyeccion_horas_proyecto_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE time_report.tbl_time_report_proyecto (
    id integer NOT NULL,
    idcliente integer,
    idestadoproyecto integer NOT NULL,
    idtipoproyecto integer,
    codigo character varying(50),
    nombre character varying(150) NOT NULL,
    descripcion character varying(255),
    fechainicioplaneada date,
    fechafinplaneada date,
    fechainicioreal date,
    fechafinreal date,
    fechainicioespera date,
    fechafinespera date,
    observacion character varying(255),
    presupuesto numeric(15,2),
    horasasignadas numeric(10,2),
    activo boolean DEFAULT true NOT NULL,
    usuariocreacion character varying(50) NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    usuariomodificacion character varying(50),
    fechamodificacion timestamp with time zone,
    ipcreacion character varying(45) NOT NULL,
    ipmodificacion character varying(45)
);


ALTER TABLE time_report.tbl_time_report_proyecto ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME time_report.tbl_time_report_proyecto_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE time_report.tbl_time_report_solicitud (
    id integer NOT NULL,
    codigo character varying(30) NOT NULL,
    id_empleado integer NOT NULL,
    id_asignacion_proyecto integer,
    id_proyecto integer,
    id_cliente integer,
    id_lider integer,
    id_tipo_permiso integer NOT NULL,
    id_estado integer NOT NULL,
    fecha_solicitud date DEFAULT CURRENT_DATE NOT NULL,
    motivo text NOT NULL,
    fecha_inicio date NOT NULL,
    fecha_fin date NOT NULL,
    fecha_regreso date NOT NULL,
    dias_solicitados numeric(5,2) NOT NULL,
    horario character varying(20) DEFAULT 'COMPLETA'::character varying,
    detalle_horario character varying(120),
    horas_solicitadas numeric(5,2),
    impacto_timereport character varying(30) DEFAULT 'RESTA_META'::character varying NOT NULL,
    token_externo character varying(120),
    token_externo_expira timestamp with time zone,
    token_externo_usado_en timestamp with time zone,
    activo boolean DEFAULT true NOT NULL,
    usuario_creacion character varying(50) NOT NULL,
    fecha_creacion timestamp with time zone DEFAULT now() NOT NULL,
    usuario_modificacion character varying(50),
    fecha_modificacion timestamp with time zone,
    ip_creacion character varying(45) NOT NULL,
    ip_modificacion character varying(45),
    CONSTRAINT ck_tr_solicitud_fechas CHECK ((fecha_fin >= fecha_inicio)),
    CONSTRAINT ck_tr_solicitud_horario CHECK (((horario IS NULL) OR ((horario)::text = ANY (ARRAY[('COMPLETA'::character varying)::text, ('MANANA'::character varying)::text, ('TARDE'::character varying)::text, ('OTROS'::character varying)::text])))),
    CONSTRAINT ck_tr_solicitud_impacto CHECK (((impacto_timereport)::text = ANY (ARRAY[('RESTA_META'::character varying)::text, ('SUMA_REPORTADAS'::character varying)::text, ('SIN_IMPACTO'::character varying)::text])))
);


CREATE TABLE time_report.tbl_time_report_solicitud_adjunto (
    id integer NOT NULL,
    id_solicitud integer NOT NULL,
    nombre_original character varying(255) NOT NULL,
    bucket_name character varying(63) NOT NULL,
    object_key character varying(255) NOT NULL,
    content_type character varying(100) NOT NULL,
    tamano_bytes bigint NOT NULL,
    sha256 character(64),
    subido_por_usuario_id integer CONSTRAINT tbl_time_report_solicitud_adjunt_subido_por_usuario_id_not_null NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    fecha_creacion timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT ck_tr_solicitud_adjunto_mime CHECK (((content_type)::text = ANY (ARRAY[('application/pdf'::character varying)::text, ('image/png'::character varying)::text, ('image/jpeg'::character varying)::text]))),
    CONSTRAINT ck_tr_solicitud_adjunto_tamano CHECK (((tamano_bytes >= 1) AND (tamano_bytes <= 5242880)))
);


ALTER TABLE time_report.tbl_time_report_solicitud_adjunto ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME time_report.tbl_time_report_solicitud_adjunto_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


ALTER TABLE time_report.tbl_time_report_solicitud ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME time_report.tbl_time_report_solicitud_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE time_report.tbl_time_report_solicitud_revision (
    id integer NOT NULL,
    id_solicitud integer NOT NULL,
    etapa_revision character varying(30) NOT NULL,
    id_usuario_revisor integer,
    metodo_revision character varying(30) DEFAULT 'CUENTA_TMR'::character varying NOT NULL,
    decision character varying(20) NOT NULL,
    fecha_revision timestamp with time zone DEFAULT now() NOT NULL,
    comentario text,
    CONSTRAINT ck_tr_solicitud_revision_comentario CHECK ((((decision)::text = 'APROBADO'::text) OR (NULLIF(btrim(comentario), ''::text) IS NOT NULL))),
    CONSTRAINT ck_tr_solicitud_revision_decision CHECK (((decision)::text = ANY (ARRAY[('APROBADO'::character varying)::text, ('OBSERVADO'::character varying)::text, ('RECHAZADO'::character varying)::text]))),
    CONSTRAINT ck_tr_solicitud_revision_etapa CHECK (((etapa_revision)::text = ANY (ARRAY[('JEFE_INMEDIATO'::character varying)::text, ('TALENTO_HUMANO'::character varying)::text]))),
    CONSTRAINT ck_tr_solicitud_revision_metodo CHECK (((metodo_revision)::text = ANY (ARRAY[('CUENTA_TMR'::character varying)::text, ('MAGIC_LINK_EXTERNO'::character varying)::text])))
);


ALTER TABLE time_report.tbl_time_report_solicitud_revision ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME time_report.tbl_time_report_solicitud_revision_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE time_report.tbl_time_report_tipo_actividad (
    id integer NOT NULL,
    nombretipo character varying(100) NOT NULL,
    descripcion character varying(255),
    codigocolor character varying(7),
    activo boolean DEFAULT true NOT NULL,
    usuariocreacion character varying(50) NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    usuariomodificacion character varying(50),
    fechamodificacion timestamp with time zone,
    ipcreacion character varying(45) NOT NULL,
    ipmodificacion character varying(45)
);


ALTER TABLE time_report.tbl_time_report_tipo_actividad ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME time_report.tbl_time_report_tipo_actividad_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE time_report.tbl_time_report_tipo_permiso (
    id integer NOT NULL,
    codigo character varying(30) NOT NULL,
    codigo_corto character varying(5) NOT NULL,
    nombre character varying(80) NOT NULL,
    descripcion character varying(200),
    requiere_adjunto boolean DEFAULT false NOT NULL,
    orden smallint DEFAULT 1 NOT NULL,
    activo boolean DEFAULT true NOT NULL
);


ALTER TABLE time_report.tbl_time_report_tipo_permiso ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME time_report.tbl_time_report_tipo_permiso_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


CREATE TABLE time_report.tbl_time_report_tipo_proyecto (
    id integer NOT NULL,
    nombretipo character varying(50) NOT NULL,
    essubtipo boolean,
    activo boolean DEFAULT true NOT NULL,
    usuariocreacion character varying(50) NOT NULL,
    fechacreacion timestamp with time zone DEFAULT now() NOT NULL,
    usuariomodificacion character varying(50),
    fechamodificacion timestamp with time zone,
    ipcreacion character varying(45) NOT NULL,
    ipmodificacion character varying(45)
);


ALTER TABLE time_report.tbl_time_report_tipo_proyecto ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME time_report.tbl_time_report_tipo_proyecto_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


ALTER TABLE ONLY administracion.tbl_administracion_apariencia
    ADD CONSTRAINT pk_administracion_apariencia PRIMARY KEY (id);


ALTER TABLE ONLY administracion.tbl_administracion_cargo
    ADD CONSTRAINT pk_administracion_cargo PRIMARY KEY (id);


ALTER TABLE ONLY administracion.tbl_administracion_catalogo
    ADD CONSTRAINT pk_administracion_catalogo PRIMARY KEY (id);


ALTER TABLE ONLY administracion.tbl_administracion_catalogo_detalle
    ADD CONSTRAINT pk_administracion_catalogo_detalle PRIMARY KEY (id);


ALTER TABLE ONLY administracion.tbl_administracion_cliente
    ADD CONSTRAINT pk_administracion_cliente PRIMARY KEY (id);


ALTER TABLE ONLY administracion.tbl_administracion_cliente_usuario
    ADD CONSTRAINT pk_administracion_cliente_usuario PRIMARY KEY (id);


ALTER TABLE ONLY administracion.tbl_administracion_empleado
    ADD CONSTRAINT pk_administracion_empleado PRIMARY KEY (id);


ALTER TABLE ONLY administracion.tbl_administracion_empleado_contrato
    ADD CONSTRAINT pk_administracion_empleado_contrato PRIMARY KEY (id);


ALTER TABLE ONLY administracion.tbl_administracion_lider
    ADD CONSTRAINT pk_administracion_lider PRIMARY KEY (id);


ALTER TABLE ONLY administracion.tbl_administracion_persona
    ADD CONSTRAINT pk_administracion_persona PRIMARY KEY (id);


ALTER TABLE ONLY administracion.tbl_administracion_registro_asignacion
    ADD CONSTRAINT pk_administracion_registro_asignacion PRIMARY KEY (id);


ALTER TABLE ONLY administracion.tbl_administracion_catalogo_detalle
    ADD CONSTRAINT uq_administracion_catalogo_detalle_id_codigo UNIQUE (idcatalogo, codigovalor);


ALTER TABLE ONLY administracion.tbl_administracion_catalogo
    ADD CONSTRAINT uq_administracion_catalogo_tipo_codigo UNIQUE (tipocatalogo, codigo);


ALTER TABLE ONLY administracion.tbl_administracion_empleado
    ADD CONSTRAINT uq_administracion_empleado_codigo UNIQUE (codigoempleado);


ALTER TABLE ONLY auditoria.tbl_auditoria_asignacion_equipo
    ADD CONSTRAINT pk_auditoria_asignacion_equipo PRIMARY KEY (id);


ALTER TABLE ONLY auditoria.tbl_auditoria_empleado
    ADD CONSTRAINT pk_auditoria_empleado PRIMARY KEY (id);


ALTER TABLE ONLY auditoria.tbl_auditoria_equipo
    ADD CONSTRAINT pk_auditoria_equipo PRIMARY KEY (id);


ALTER TABLE ONLY auditoria.tbl_auditoria_historico_general
    ADD CONSTRAINT pk_auditoria_historico_general PRIMARY KEY (id);


ALTER TABLE ONLY auditoria.tbl_auditoria_sesion_usuario
    ADD CONSTRAINT pk_auditoria_sesion_usuario PRIMARY KEY (id);


ALTER TABLE ONLY autenticacion.tbl_autenticacion_audit_log
    ADD CONSTRAINT pk_autenticacion_audit_log PRIMARY KEY (id);


ALTER TABLE ONLY autenticacion.tbl_autenticacion_modulo
    ADD CONSTRAINT pk_autenticacion_modulo PRIMARY KEY (id);


ALTER TABLE ONLY autenticacion.tbl_autenticacion_password_historial
    ADD CONSTRAINT pk_autenticacion_password_historial PRIMARY KEY (id);


ALTER TABLE ONLY autenticacion.tbl_autenticacion_password_reset
    ADD CONSTRAINT pk_autenticacion_password_reset PRIMARY KEY (id);


ALTER TABLE ONLY autenticacion.tbl_autenticacion_permiso
    ADD CONSTRAINT pk_autenticacion_permiso PRIMARY KEY (id);


ALTER TABLE ONLY autenticacion.tbl_autenticacion_pregunta_usuario
    ADD CONSTRAINT pk_autenticacion_pregunta_usuario PRIMARY KEY (id);


ALTER TABLE ONLY autenticacion.tbl_autenticacion_refresh_token
    ADD CONSTRAINT pk_autenticacion_refresh_token PRIMARY KEY (id);


ALTER TABLE ONLY autenticacion.tbl_autenticacion_rol
    ADD CONSTRAINT pk_autenticacion_rol PRIMARY KEY (id);


ALTER TABLE ONLY autenticacion.tbl_autenticacion_rol_permiso
    ADD CONSTRAINT pk_autenticacion_rol_permiso PRIMARY KEY (idrol, idpermiso);


ALTER TABLE ONLY autenticacion.tbl_autenticacion_sesion
    ADD CONSTRAINT pk_autenticacion_sesion PRIMARY KEY (id);


ALTER TABLE ONLY autenticacion.tbl_autenticacion_usuario
    ADD CONSTRAINT pk_autenticacion_usuario PRIMARY KEY (id);


ALTER TABLE ONLY autenticacion.tbl_autenticacion_usuario_rol
    ADD CONSTRAINT pk_autenticacion_usuario_rol PRIMARY KEY (idusuario, idrol);


ALTER TABLE ONLY autenticacion.tbl_autenticacion_permiso
    ADD CONSTRAINT uq_autenticacion_permiso_codigo UNIQUE (codigo);


ALTER TABLE ONLY autenticacion.tbl_autenticacion_refresh_token
    ADD CONSTRAINT uq_autenticacion_refresh_token_hash UNIQUE (tokenhash);


ALTER TABLE ONLY autenticacion.tbl_autenticacion_rol
    ADD CONSTRAINT uq_autenticacion_rol_nombre UNIQUE (nombre);


ALTER TABLE ONLY autenticacion.tbl_autenticacion_token_blacklist
    ADD CONSTRAINT uq_autenticacion_token_blacklist_jti UNIQUE (jti);


ALTER TABLE ONLY autenticacion.tbl_autenticacion_usuario
    ADD CONSTRAINT uq_autenticacion_usuario_email UNIQUE (email);


ALTER TABLE ONLY inventario.tbl_inventario_asignacion_equipo
    ADD CONSTRAINT pk_inventario_asignacion_equipo PRIMARY KEY (id);


ALTER TABLE ONLY inventario.tbl_inventario_baja_equipo
    ADD CONSTRAINT pk_inventario_baja_equipo PRIMARY KEY (id);


ALTER TABLE ONLY inventario.tbl_inventario_caracteristica_equipo
    ADD CONSTRAINT pk_inventario_caracteristica_equipo PRIMARY KEY (id);


ALTER TABLE ONLY inventario.tbl_inventario_detalle_factura
    ADD CONSTRAINT pk_inventario_detalle_factura PRIMARY KEY (id);


ALTER TABLE ONLY inventario.tbl_inventario_empresa
    ADD CONSTRAINT pk_inventario_empresa PRIMARY KEY (id);


ALTER TABLE ONLY inventario.tbl_inventario_equipo
    ADD CONSTRAINT pk_inventario_equipo PRIMARY KEY (id);


ALTER TABLE ONLY inventario.tbl_inventario_factura
    ADD CONSTRAINT pk_inventario_factura PRIMARY KEY (id);


ALTER TABLE ONLY inventario.tbl_inventario_proveedor
    ADD CONSTRAINT pk_inventario_proveedor PRIMARY KEY (id);


ALTER TABLE ONLY inventario.tbl_inventario_reparacion_equipo
    ADD CONSTRAINT pk_inventario_reparacion_equipo PRIMARY KEY (id);


ALTER TABLE ONLY inventario.tbl_inventario_stock_categoria
    ADD CONSTRAINT pk_inventario_stock_categoria PRIMARY KEY (id);


ALTER TABLE ONLY inventario.tbl_inventario_tipo_proveedor
    ADD CONSTRAINT tbl_inventario_tipo_proveedor_nombre_key UNIQUE (nombre);


ALTER TABLE ONLY inventario.tbl_inventario_tipo_proveedor
    ADD CONSTRAINT tbl_inventario_tipo_proveedor_pkey PRIMARY KEY (id);


ALTER TABLE ONLY inventario.tbl_inventario_equipo
    ADD CONSTRAINT uq_inventario_equipo_serie UNIQUE (numeroserie);


ALTER TABLE ONLY inventario.tbl_inventario_factura
    ADD CONSTRAINT uq_inventario_factura_numero UNIQUE (numerofactura);


ALTER TABLE ONLY inventory.tbl_inventario_proveedor
    ADD CONSTRAINT tbl_inventario_proveedor_pkey PRIMARY KEY (id);


ALTER TABLE ONLY inventory.tbl_inventario_tipo_proveedor
    ADD CONSTRAINT tbl_inventario_tipo_proveedor_pkey PRIMARY KEY (id);


ALTER TABLE ONLY inventory.tbl_inventario_tipo_proveedor
    ADD CONSTRAINT uq_tbl_inventario_tipo_proveedor_name UNIQUE (name);


ALTER TABLE ONLY notificacion.evento_outbox
    ADD CONSTRAINT evento_outbox_pkey PRIMARY KEY (id);


ALTER TABLE ONLY notificacion.notificacion_envio
    ADD CONSTRAINT notificacion_envio_pkey PRIMARY KEY (id);


ALTER TABLE ONLY notificacion.notificacion_fecha_corte
    ADD CONSTRAINT notificacion_fecha_corte_pkey PRIMARY KEY (id);


ALTER TABLE ONLY notificacion.notificacion_usuario
    ADD CONSTRAINT notificacion_usuario_pkey PRIMARY KEY (id);


ALTER TABLE ONLY notificacion.notificacion_fecha_corte
    ADD CONSTRAINT uq_notificacion_fecha_corte_periodo UNIQUE (anio, mes, tipo);


ALTER TABLE ONLY time_report.tbl_time_report_actividad_diaria
    ADD CONSTRAINT pk_time_report_actividad_diaria PRIMARY KEY (id);


ALTER TABLE ONLY time_report.tbl_time_report_asignacion_proyecto
    ADD CONSTRAINT pk_time_report_empleado_proyecto PRIMARY KEY (id);


ALTER TABLE ONLY time_report.tbl_time_report_feriado
    ADD CONSTRAINT pk_time_report_feriado PRIMARY KEY (id);


ALTER TABLE ONLY time_report.tbl_time_report_homologacion_banco
    ADD CONSTRAINT pk_time_report_homologacion_banco PRIMARY KEY (id);


ALTER TABLE ONLY time_report.tbl_time_report_outbox_cargo
    ADD CONSTRAINT pk_time_report_outbox_cargo PRIMARY KEY (idoutbox);


ALTER TABLE ONLY time_report.tbl_time_report_permiso
    ADD CONSTRAINT pk_time_report_permiso PRIMARY KEY (id);


ALTER TABLE ONLY time_report.tbl_time_report_proyeccion_horas
    ADD CONSTRAINT pk_time_report_proyeccion_horas PRIMARY KEY (id);


ALTER TABLE ONLY time_report.tbl_time_report_proyeccion_horas_proyecto
    ADD CONSTRAINT pk_time_report_proyeccion_horas_proyecto PRIMARY KEY (id);


ALTER TABLE ONLY time_report.tbl_time_report_proyecto
    ADD CONSTRAINT pk_time_report_proyecto PRIMARY KEY (id);


ALTER TABLE ONLY time_report.tbl_time_report_tipo_actividad
    ADD CONSTRAINT pk_time_report_tipo_actividad PRIMARY KEY (id);


ALTER TABLE ONLY time_report.tbl_time_report_tipo_proyecto
    ADD CONSTRAINT pk_time_report_tipo_proyecto PRIMARY KEY (id);


ALTER TABLE ONLY time_report.tbl_time_report_estado_solicitud
    ADD CONSTRAINT tbl_time_report_estado_solicitud_pkey PRIMARY KEY (id);


ALTER TABLE ONLY time_report.tbl_time_report_solicitud_adjunto
    ADD CONSTRAINT tbl_time_report_solicitud_adjunto_pkey PRIMARY KEY (id);


ALTER TABLE ONLY time_report.tbl_time_report_solicitud
    ADD CONSTRAINT tbl_time_report_solicitud_pkey PRIMARY KEY (id);


ALTER TABLE ONLY time_report.tbl_time_report_solicitud_revision
    ADD CONSTRAINT tbl_time_report_solicitud_revision_pkey PRIMARY KEY (id);


ALTER TABLE ONLY time_report.tbl_time_report_tipo_permiso
    ADD CONSTRAINT tbl_time_report_tipo_permiso_pkey PRIMARY KEY (id);


ALTER TABLE ONLY time_report.tbl_time_report_homologacion_banco
    ADD CONSTRAINT uq_time_report_homologacion_banco UNIQUE (idempleado, proyectotr);


ALTER TABLE ONLY time_report.tbl_time_report_estado_solicitud
    ADD CONSTRAINT uq_tr_estado_solicitud_codigo UNIQUE (codigo);


ALTER TABLE ONLY time_report.tbl_time_report_estado_solicitud
    ADD CONSTRAINT uq_tr_estado_solicitud_codigo_corto UNIQUE (codigo_corto);


ALTER TABLE ONLY time_report.tbl_time_report_solicitud
    ADD CONSTRAINT uq_tr_solicitud_codigo UNIQUE (codigo);


ALTER TABLE ONLY time_report.tbl_time_report_tipo_permiso
    ADD CONSTRAINT uq_tr_tipo_permiso_codigo UNIQUE (codigo);


ALTER TABLE ONLY time_report.tbl_time_report_tipo_permiso
    ADD CONSTRAINT uq_tr_tipo_permiso_codigo_corto UNIQUE (codigo_corto);


CREATE INDEX idx_adm_empleado_causa_salida ON administracion.tbl_administracion_empleado USING btree (idcausasalida);


CREATE INDEX idx_adm_empleado_contrato_empleado ON administracion.tbl_administracion_empleado_contrato USING btree (idempleado, fechadesde);


CREATE INDEX idx_adm_empleado_persona ON administracion.tbl_administracion_empleado USING btree (idpersona);


CREATE INDEX idx_adm_empleado_reemplazo ON administracion.tbl_administracion_empleado USING btree (idempleadoreemplazo);


CREATE INDEX idx_adm_empleado_terminacion ON administracion.tbl_administracion_empleado USING btree (fechaterminacion);


CREATE INDEX idx_adm_empleado_tipo_salida ON administracion.tbl_administracion_empleado USING btree (idtiposalida);


CREATE INDEX idx_adm_persona_identificacion ON administracion.tbl_administracion_persona USING btree (numeroidentificacion);


CREATE INDEX idx_aud_empleado_fecha ON auditoria.tbl_auditoria_empleado USING btree (fechacreacion);


CREATE INDEX idx_aud_empleado_id ON auditoria.tbl_auditoria_empleado USING btree (idempleado);


CREATE INDEX idx_aud_historico_fecha ON auditoria.tbl_auditoria_historico_general USING btree (fechacambio);


CREATE INDEX idx_aud_historico_tabla ON auditoria.tbl_auditoria_historico_general USING btree (nombretabla);


CREATE INDEX idx_aud_sesion_usuario ON auditoria.tbl_auditoria_sesion_usuario USING btree (idusuario);


CREATE INDEX idx_aut_audit_log_evento ON autenticacion.tbl_autenticacion_audit_log USING btree (evento);


CREATE INDEX idx_aut_audit_log_fecha ON autenticacion.tbl_autenticacion_audit_log USING btree (fechacreacion);


CREATE INDEX idx_aut_audit_log_usuario ON autenticacion.tbl_autenticacion_audit_log USING btree (idusuario);


CREATE INDEX idx_aut_refresh_token_exp ON autenticacion.tbl_autenticacion_refresh_token USING btree (fechaexpiracion);


CREATE INDEX idx_aut_refresh_token_familia ON autenticacion.tbl_autenticacion_refresh_token USING btree (familiatoken);


CREATE INDEX idx_aut_refresh_token_usuario ON autenticacion.tbl_autenticacion_refresh_token USING btree (idusuario);


CREATE INDEX idx_aut_sesion_activa ON autenticacion.tbl_autenticacion_sesion USING btree (estaactiva);


CREATE INDEX idx_aut_sesion_usuario ON autenticacion.tbl_autenticacion_sesion USING btree (idusuario);


CREATE INDEX idx_aut_token_blacklist_exp ON autenticacion.tbl_autenticacion_token_blacklist USING btree (fechaexpiracion);


CREATE INDEX idx_autenticacion_password_reset_expiracion ON autenticacion.tbl_autenticacion_password_reset USING btree (fechaexpiracion);


CREATE INDEX idx_autenticacion_password_reset_token ON autenticacion.tbl_autenticacion_password_reset USING btree (tokenhash);


CREATE INDEX idx_autenticacion_password_reset_usuario ON autenticacion.tbl_autenticacion_password_reset USING btree (idusuario);


CREATE INDEX idx_inv_asignacion_devolucion ON inventario.tbl_inventario_asignacion_equipo USING btree (fechadevolucion);


CREATE INDEX idx_inv_asignacion_empleado ON inventario.tbl_inventario_asignacion_equipo USING btree (idempleado);


CREATE INDEX idx_inv_asignacion_equipo ON inventario.tbl_inventario_asignacion_equipo USING btree (idequipo);


CREATE INDEX idx_inv_baja_equipo ON inventario.tbl_inventario_baja_equipo USING btree (idequipo);


CREATE INDEX idx_inv_baja_tipo ON inventario.tbl_inventario_baja_equipo USING btree (idtipobaja);


CREATE INDEX idx_inv_detalle_factura ON inventario.tbl_inventario_detalle_factura USING btree (idfactura);


CREATE INDEX idx_inv_equipo_activo ON inventario.tbl_inventario_equipo USING btree (activo);


CREATE INDEX idx_inv_equipo_categoria ON inventario.tbl_inventario_equipo USING btree (idcategoria);


CREATE INDEX idx_inv_equipo_estado ON inventario.tbl_inventario_equipo USING btree (idestado);


CREATE INDEX idx_inv_factura_proveedor ON inventario.tbl_inventario_factura USING btree (idproveedor);


CREATE INDEX idx_inv_reparacion_equipo ON inventario.tbl_inventario_reparacion_equipo USING btree (idequipo);


CREATE INDEX ix_tbl_inventario_proveedor_razon_social ON inventory.tbl_inventario_proveedor USING btree (business_name);


CREATE INDEX ix_tbl_inventario_proveedor_tipo_estado ON inventory.tbl_inventario_proveedor USING btree (id_supplier_type, status);


CREATE UNIQUE INDEX ux_tbl_inventario_proveedor_ruc ON inventory.tbl_inventario_proveedor USING btree (ruc) WHERE (ruc IS NOT NULL);


CREATE INDEX idx_evento_outbox_estado_fecha ON notificacion.evento_outbox USING btree (estado, fecha_creacion);


CREATE INDEX idx_notificacion_envio_manual_proyecto ON notificacion.notificacion_envio USING btree (id_empleado, id_proyecto, fecha_envio) WHERE ((origen)::text = 'Manual'::text);


CREATE INDEX idx_notificacion_usuario_destino_fecha ON notificacion.notificacion_usuario USING btree (id_usuario_destino, fecha_creacion DESC);


CREATE INDEX idx_notificacion_usuario_no_leidas ON notificacion.notificacion_usuario USING btree (id_usuario_destino) WHERE (leida = false);


CREATE UNIQUE INDEX uq_idx_notificacion_envio_auto ON notificacion.notificacion_envio USING btree (id_empleado, fecha_corte) WHERE ((origen)::text = 'Automatico'::text);


CREATE INDEX idx_tr_actividad_empleado ON time_report.tbl_time_report_actividad_diaria USING btree (idempleado);


CREATE INDEX idx_tr_actividad_fecha ON time_report.tbl_time_report_actividad_diaria USING btree (fechaactividad);


CREATE INDEX idx_tr_actividad_proyecto ON time_report.tbl_time_report_actividad_diaria USING btree (idproyecto);


CREATE INDEX idx_tr_asignacion_proyecto_lider ON time_report.tbl_time_report_asignacion_proyecto USING btree (idlider) WITH (fillfactor='100', deduplicate_items='true');


CREATE INDEX idx_tr_empleado_proyecto_emp ON time_report.tbl_time_report_asignacion_proyecto USING btree (idempleado);


CREATE INDEX idx_tr_outbox_procesamiento ON time_report.tbl_time_report_outbox_cargo USING btree (proximointento, procesadoen) WHERE (procesadoen IS NULL);


CREATE INDEX idx_tr_permiso_empleado ON time_report.tbl_time_report_permiso USING btree (idempleado);


CREATE INDEX idx_tr_permiso_estado ON time_report.tbl_time_report_permiso USING btree (idestadoaprobacion);


CREATE INDEX idx_tr_proyeccion_grupo ON time_report.tbl_time_report_proyeccion_horas USING btree (grupoproyeccion);


CREATE INDEX idx_tr_proyecto_cliente ON time_report.tbl_time_report_proyecto USING btree (idcliente);


CREATE INDEX idx_tr_proyecto_tipo ON time_report.tbl_time_report_proyecto USING btree (idtipoproyecto);


CREATE INDEX idx_tr_solicitud_adjunto_solicitud ON time_report.tbl_time_report_solicitud_adjunto USING btree (id_solicitud) WHERE (activo = true);


CREATE INDEX idx_tr_solicitud_empleado ON time_report.tbl_time_report_solicitud USING btree (id_empleado, fecha_creacion DESC);


CREATE INDEX idx_tr_solicitud_estado ON time_report.tbl_time_report_solicitud USING btree (id_estado, fecha_creacion DESC);


CREATE INDEX idx_tr_solicitud_fechas ON time_report.tbl_time_report_solicitud USING btree (fecha_inicio, fecha_fin);


CREATE INDEX idx_tr_solicitud_lider_estado ON time_report.tbl_time_report_solicitud USING btree (id_lider, id_estado);


CREATE INDEX idx_tr_solicitud_revision_solicitud ON time_report.tbl_time_report_solicitud_revision USING btree (id_solicitud, etapa_revision, fecha_revision DESC);


CREATE UNIQUE INDEX uq_idx_tr_solicitud_adjunto_objeto ON time_report.tbl_time_report_solicitud_adjunto USING btree (bucket_name, object_key);


CREATE UNIQUE INDEX uq_idx_tr_solicitud_token_externo ON time_report.tbl_time_report_solicitud USING btree (token_externo) WHERE (token_externo IS NOT NULL);


CREATE UNIQUE INDEX uq_tr_proyecto_codigo ON time_report.tbl_time_report_proyecto USING btree (upper(TRIM(BOTH FROM codigo))) WHERE ((codigo IS NOT NULL) AND (TRIM(BOTH FROM codigo) <> ''::text));


CREATE TRIGGER trg_tbl_administracion_empleado_ins AFTER INSERT ON administracion.tbl_administracion_empleado FOR EACH ROW EXECUTE FUNCTION auditoria.fn_auditoria_registrar_empleado_insert();


CREATE TRIGGER trg_tbl_administracion_empleado_upd AFTER UPDATE ON administracion.tbl_administracion_empleado FOR EACH ROW EXECUTE FUNCTION auditoria.fn_auditoria_registrar_empleado_update();


CREATE TRIGGER trg_tbl_autenticacion_sesion_ins AFTER INSERT ON autenticacion.tbl_autenticacion_sesion FOR EACH ROW EXECUTE FUNCTION auditoria.fn_auditoria_registrar_login();


CREATE TRIGGER trg_tbl_autenticacion_sesion_upd AFTER UPDATE ON autenticacion.tbl_autenticacion_sesion FOR EACH ROW EXECUTE FUNCTION auditoria.fn_auditoria_registrar_logout();


CREATE TRIGGER trg_tbl_inventario_asignacion_equipo_ins AFTER INSERT ON inventario.tbl_inventario_asignacion_equipo FOR EACH ROW EXECUTE FUNCTION auditoria.fn_auditoria_registrar_asignacion_equipo_insert();


CREATE TRIGGER trg_tbl_inventario_asignacion_equipo_upd AFTER UPDATE ON inventario.tbl_inventario_asignacion_equipo FOR EACH ROW EXECUTE FUNCTION auditoria.fn_auditoria_registrar_asignacion_equipo_update();


CREATE TRIGGER trg_tbl_inventario_equipo_ins AFTER INSERT ON inventario.tbl_inventario_equipo FOR EACH ROW EXECUTE FUNCTION auditoria.fn_auditoria_registrar_equipo_insert();


CREATE TRIGGER trg_tbl_inventario_equipo_upd AFTER UPDATE ON inventario.tbl_inventario_equipo FOR EACH ROW EXECUTE FUNCTION auditoria.fn_auditoria_registrar_equipo_update();


ALTER TABLE ONLY administracion.tbl_administracion_cargo
    ADD CONSTRAINT fk_administracion_cargo_departamento FOREIGN KEY (iddepartamento) REFERENCES administracion.tbl_administracion_catalogo_detalle(id);


ALTER TABLE ONLY administracion.tbl_administracion_catalogo_detalle
    ADD CONSTRAINT fk_administracion_catalogo_detalle_catalogo FOREIGN KEY (idcatalogo) REFERENCES administracion.tbl_administracion_catalogo(id);


ALTER TABLE ONLY administracion.tbl_administracion_cliente
    ADD CONSTRAINT fk_administracion_cliente_tipo_identificacion FOREIGN KEY (idtipoidentificacion) REFERENCES administracion.tbl_administracion_catalogo_detalle(id);


ALTER TABLE ONLY administracion.tbl_administracion_cliente_usuario
    ADD CONSTRAINT fk_administracion_cliente_usuario_cliente FOREIGN KEY (idcliente) REFERENCES administracion.tbl_administracion_cliente(id);


ALTER TABLE ONLY administracion.tbl_administracion_cliente_usuario
    ADD CONSTRAINT fk_administracion_cliente_usuario_usuario FOREIGN KEY (idusuario) REFERENCES autenticacion.tbl_autenticacion_usuario(id);


ALTER TABLE ONLY administracion.tbl_administracion_empleado
    ADD CONSTRAINT fk_administracion_empleado_cargo FOREIGN KEY (idcargo) REFERENCES administracion.tbl_administracion_cargo(id);


ALTER TABLE ONLY administracion.tbl_administracion_empleado
    ADD CONSTRAINT fk_administracion_empleado_categoria FOREIGN KEY (idcategoriaempleado) REFERENCES administracion.tbl_administracion_catalogo_detalle(id);


ALTER TABLE ONLY administracion.tbl_administracion_empleado
    ADD CONSTRAINT fk_administracion_empleado_contrato FOREIGN KEY (idtipocontrato) REFERENCES administracion.tbl_administracion_catalogo_detalle(id);


ALTER TABLE ONLY administracion.tbl_administracion_empleado
    ADD CONSTRAINT fk_administracion_empleado_empresa FOREIGN KEY (idempresacatalogo) REFERENCES administracion.tbl_administracion_catalogo_detalle(id);


ALTER TABLE ONLY administracion.tbl_administracion_empleado
    ADD CONSTRAINT fk_administracion_empleado_modo_trabajo FOREIGN KEY (idmodotrabajo) REFERENCES administracion.tbl_administracion_catalogo_detalle(id);


ALTER TABLE ONLY administracion.tbl_administracion_empleado
    ADD CONSTRAINT fk_administracion_empleado_persona FOREIGN KEY (idpersona) REFERENCES administracion.tbl_administracion_persona(id);


ALTER TABLE ONLY administracion.tbl_administracion_lider
    ADD CONSTRAINT fk_administracion_lider_persona FOREIGN KEY (idpersona) REFERENCES administracion.tbl_administracion_persona(id);


ALTER TABLE ONLY administracion.tbl_administracion_lider
    ADD CONSTRAINT fk_administracion_lider_tipo FOREIGN KEY (idtipo) REFERENCES administracion.tbl_administracion_catalogo_detalle(id);


ALTER TABLE ONLY administracion.tbl_administracion_persona
    ADD CONSTRAINT fk_administracion_persona_genero FOREIGN KEY (idgenero) REFERENCES administracion.tbl_administracion_catalogo_detalle(id);


ALTER TABLE ONLY administracion.tbl_administracion_persona
    ADD CONSTRAINT fk_administracion_persona_nacionalidad FOREIGN KEY (idnacionalidad) REFERENCES administracion.tbl_administracion_catalogo_detalle(id);


ALTER TABLE ONLY administracion.tbl_administracion_persona
    ADD CONSTRAINT fk_administracion_persona_tipo_identificacion FOREIGN KEY (idtipoidentificacion) REFERENCES administracion.tbl_administracion_catalogo_detalle(id);


ALTER TABLE ONLY administracion.tbl_administracion_registro_asignacion
    ADD CONSTRAINT fk_administracion_registro_asignacion_empleado FOREIGN KEY (idempleado) REFERENCES administracion.tbl_administracion_empleado(id);


ALTER TABLE ONLY administracion.tbl_administracion_empleado
    ADD CONSTRAINT fk_empleado_causa_salida FOREIGN KEY (idcausasalida) REFERENCES administracion.tbl_administracion_catalogo_detalle(id);


ALTER TABLE ONLY administracion.tbl_administracion_empleado_contrato
    ADD CONSTRAINT fk_empleado_contrato_empleado FOREIGN KEY (idempleado) REFERENCES administracion.tbl_administracion_empleado(id);


ALTER TABLE ONLY administracion.tbl_administracion_empleado_contrato
    ADD CONSTRAINT fk_empleado_contrato_tipo FOREIGN KEY (idtipocontrato) REFERENCES administracion.tbl_administracion_catalogo_detalle(id);


ALTER TABLE ONLY administracion.tbl_administracion_empleado
    ADD CONSTRAINT fk_empleado_reemplazo FOREIGN KEY (idempleadoreemplazo) REFERENCES administracion.tbl_administracion_empleado(id);


ALTER TABLE ONLY administracion.tbl_administracion_empleado
    ADD CONSTRAINT fk_empleado_tipo_salida FOREIGN KEY (idtiposalida) REFERENCES administracion.tbl_administracion_catalogo_detalle(id);


ALTER TABLE ONLY autenticacion.tbl_autenticacion_audit_log
    ADD CONSTRAINT fk_autenticacion_audit_log_usuario FOREIGN KEY (idusuario) REFERENCES autenticacion.tbl_autenticacion_usuario(id) ON DELETE CASCADE;


ALTER TABLE ONLY autenticacion.tbl_autenticacion_password_historial
    ADD CONSTRAINT fk_autenticacion_password_historial_usuario FOREIGN KEY (idusuario) REFERENCES autenticacion.tbl_autenticacion_usuario(id);


ALTER TABLE ONLY autenticacion.tbl_autenticacion_password_reset
    ADD CONSTRAINT fk_autenticacion_password_reset_usuario FOREIGN KEY (idusuario) REFERENCES autenticacion.tbl_autenticacion_usuario(id) ON DELETE CASCADE;


ALTER TABLE ONLY autenticacion.tbl_autenticacion_permiso
    ADD CONSTRAINT fk_autenticacion_permiso_modulo FOREIGN KEY (idmodulo) REFERENCES autenticacion.tbl_autenticacion_modulo(id) ON DELETE RESTRICT;


ALTER TABLE ONLY autenticacion.tbl_autenticacion_pregunta_usuario
    ADD CONSTRAINT fk_autenticacion_pregunta_usuario_usuario FOREIGN KEY (idusuario) REFERENCES autenticacion.tbl_autenticacion_usuario(id);


ALTER TABLE ONLY autenticacion.tbl_autenticacion_refresh_token
    ADD CONSTRAINT fk_autenticacion_refresh_token_sesion FOREIGN KEY (idsesion) REFERENCES autenticacion.tbl_autenticacion_sesion(id) ON DELETE CASCADE;


ALTER TABLE ONLY autenticacion.tbl_autenticacion_refresh_token
    ADD CONSTRAINT fk_autenticacion_refresh_token_usuario FOREIGN KEY (idusuario) REFERENCES autenticacion.tbl_autenticacion_usuario(id) ON DELETE CASCADE;


ALTER TABLE ONLY autenticacion.tbl_autenticacion_rol_permiso
    ADD CONSTRAINT fk_autenticacion_rol_permiso_permiso FOREIGN KEY (idpermiso) REFERENCES autenticacion.tbl_autenticacion_permiso(id) ON DELETE CASCADE;


ALTER TABLE ONLY autenticacion.tbl_autenticacion_rol_permiso
    ADD CONSTRAINT fk_autenticacion_rol_permiso_rol FOREIGN KEY (idrol) REFERENCES autenticacion.tbl_autenticacion_rol(id) ON DELETE CASCADE;


ALTER TABLE ONLY autenticacion.tbl_autenticacion_sesion
    ADD CONSTRAINT fk_autenticacion_sesion_usuario FOREIGN KEY (idusuario) REFERENCES autenticacion.tbl_autenticacion_usuario(id) ON DELETE CASCADE;


ALTER TABLE ONLY autenticacion.tbl_autenticacion_token_blacklist
    ADD CONSTRAINT fk_autenticacion_token_blacklist_usuario FOREIGN KEY (idusuario) REFERENCES autenticacion.tbl_autenticacion_usuario(id) ON DELETE CASCADE;


ALTER TABLE ONLY autenticacion.tbl_autenticacion_usuario
    ADD CONSTRAINT fk_autenticacion_usuario_persona FOREIGN KEY (idpersona) REFERENCES administracion.tbl_administracion_persona(id) ON DELETE RESTRICT;


ALTER TABLE ONLY autenticacion.tbl_autenticacion_usuario_rol
    ADD CONSTRAINT fk_autenticacion_usuario_rol_asignado_por FOREIGN KEY (asignadopor) REFERENCES autenticacion.tbl_autenticacion_usuario(id) ON DELETE SET NULL;


ALTER TABLE ONLY autenticacion.tbl_autenticacion_usuario_rol
    ADD CONSTRAINT fk_autenticacion_usuario_rol_rol FOREIGN KEY (idrol) REFERENCES autenticacion.tbl_autenticacion_rol(id) ON DELETE RESTRICT;


ALTER TABLE ONLY autenticacion.tbl_autenticacion_usuario_rol
    ADD CONSTRAINT fk_autenticacion_usuario_rol_usuario FOREIGN KEY (idusuario) REFERENCES autenticacion.tbl_autenticacion_usuario(id) ON DELETE CASCADE;


ALTER TABLE ONLY inventario.tbl_inventario_asignacion_equipo
    ADD CONSTRAINT fk_inventario_asignacion_equipo_empleado FOREIGN KEY (idempleado) REFERENCES administracion.tbl_administracion_empleado(id);


ALTER TABLE ONLY inventario.tbl_inventario_asignacion_equipo
    ADD CONSTRAINT fk_inventario_asignacion_equipo_equipo FOREIGN KEY (idequipo) REFERENCES inventario.tbl_inventario_equipo(id);


ALTER TABLE ONLY inventario.tbl_inventario_baja_equipo
    ADD CONSTRAINT fk_inventario_baja_equipo_equipo FOREIGN KEY (idequipo) REFERENCES inventario.tbl_inventario_equipo(id);


ALTER TABLE ONLY inventario.tbl_inventario_baja_equipo
    ADD CONSTRAINT fk_inventario_baja_equipo_tipo_baja FOREIGN KEY (idtipobaja) REFERENCES administracion.tbl_administracion_catalogo_detalle(id);


ALTER TABLE ONLY inventario.tbl_inventario_caracteristica_equipo
    ADD CONSTRAINT fk_inventario_caracteristica_equipo_equipo FOREIGN KEY (idequipo) REFERENCES inventario.tbl_inventario_equipo(id);


ALTER TABLE ONLY inventario.tbl_inventario_caracteristica_equipo
    ADD CONSTRAINT fk_inventario_caracteristica_equipo_tipo_componente FOREIGN KEY (idtipocomponente) REFERENCES administracion.tbl_administracion_catalogo_detalle(id);


ALTER TABLE ONLY inventario.tbl_inventario_detalle_factura
    ADD CONSTRAINT fk_inventario_detalle_factura_factura FOREIGN KEY (idfactura) REFERENCES inventario.tbl_inventario_factura(id);


ALTER TABLE ONLY inventario.tbl_inventario_equipo
    ADD CONSTRAINT fk_inventario_equipo_categoria FOREIGN KEY (idcategoria) REFERENCES administracion.tbl_administracion_catalogo_detalle(id);


ALTER TABLE ONLY inventario.tbl_inventario_equipo
    ADD CONSTRAINT fk_inventario_equipo_condicion FOREIGN KEY (idcondicion) REFERENCES administracion.tbl_administracion_catalogo_detalle(id);


ALTER TABLE ONLY inventario.tbl_inventario_equipo
    ADD CONSTRAINT fk_inventario_equipo_estado FOREIGN KEY (idestado) REFERENCES administracion.tbl_administracion_catalogo_detalle(id);


ALTER TABLE ONLY inventario.tbl_inventario_equipo
    ADD CONSTRAINT fk_inventario_equipo_factura FOREIGN KEY (idfactura) REFERENCES inventario.tbl_inventario_factura(id);


ALTER TABLE ONLY inventario.tbl_inventario_equipo
    ADD CONSTRAINT fk_inventario_equipo_tipo_garantia FOREIGN KEY (idtipogarantia) REFERENCES administracion.tbl_administracion_catalogo_detalle(id);


ALTER TABLE ONLY inventario.tbl_inventario_factura
    ADD CONSTRAINT fk_inventario_factura_proveedor FOREIGN KEY (idproveedor) REFERENCES inventario.tbl_inventario_proveedor(id);


ALTER TABLE ONLY inventario.tbl_inventario_proveedor
    ADD CONSTRAINT fk_inventario_proveedor_tipo_proveedor FOREIGN KEY (idtipoproveedor) REFERENCES administracion.tbl_administracion_catalogo_detalle(id);


ALTER TABLE ONLY inventario.tbl_inventario_reparacion_equipo
    ADD CONSTRAINT fk_inventario_reparacion_equipo_equipo FOREIGN KEY (idequipo) REFERENCES inventario.tbl_inventario_equipo(id);


ALTER TABLE ONLY inventario.tbl_inventario_stock_categoria
    ADD CONSTRAINT fk_inventario_stock_categoria_categoria FOREIGN KEY (idcategoria) REFERENCES administracion.tbl_administracion_catalogo_detalle(id);


ALTER TABLE ONLY inventory.tbl_inventario_proveedor
    ADD CONSTRAINT fk_tbl_inventario_proveedor_tipo FOREIGN KEY (id_supplier_type) REFERENCES inventory.tbl_inventario_tipo_proveedor(id) ON UPDATE RESTRICT ON DELETE RESTRICT;


ALTER TABLE ONLY notificacion.notificacion_envio
    ADD CONSTRAINT fk_notificacion_envio_empleado FOREIGN KEY (id_empleado) REFERENCES administracion.tbl_administracion_empleado(id);


ALTER TABLE ONLY notificacion.notificacion_envio
    ADD CONSTRAINT fk_notificacion_envio_proyecto FOREIGN KEY (id_proyecto) REFERENCES time_report.tbl_time_report_proyecto(id) ON DELETE SET NULL;


ALTER TABLE ONLY notificacion.notificacion_usuario
    ADD CONSTRAINT fk_notificacion_usuario_destino FOREIGN KEY (id_usuario_destino) REFERENCES autenticacion.tbl_autenticacion_usuario(id) ON DELETE CASCADE;


ALTER TABLE ONLY notificacion.notificacion_usuario
    ADD CONSTRAINT fk_notificacion_usuario_solicitud FOREIGN KEY (id_solicitud) REFERENCES time_report.tbl_time_report_solicitud(id) ON DELETE CASCADE;


ALTER TABLE ONLY time_report.tbl_time_report_actividad_diaria
    ADD CONSTRAINT fk_time_report_actividad_diaria_aprobador FOREIGN KEY (aprobadopor) REFERENCES administracion.tbl_administracion_empleado(id);


ALTER TABLE ONLY time_report.tbl_time_report_actividad_diaria
    ADD CONSTRAINT fk_time_report_actividad_diaria_empleado FOREIGN KEY (idempleado) REFERENCES administracion.tbl_administracion_empleado(id);


ALTER TABLE ONLY time_report.tbl_time_report_actividad_diaria
    ADD CONSTRAINT fk_time_report_actividad_diaria_lider_registro FOREIGN KEY (idliderregistro) REFERENCES administracion.tbl_administracion_lider(id);


ALTER TABLE ONLY time_report.tbl_time_report_actividad_diaria
    ADD CONSTRAINT fk_time_report_actividad_diaria_proyecto FOREIGN KEY (idproyecto) REFERENCES time_report.tbl_time_report_proyecto(id);


ALTER TABLE ONLY time_report.tbl_time_report_actividad_diaria
    ADD CONSTRAINT fk_time_report_actividad_diaria_tipo FOREIGN KEY (idtipoactividad) REFERENCES time_report.tbl_time_report_tipo_actividad(id);


ALTER TABLE ONLY time_report.tbl_time_report_asignacion_proyecto
    ADD CONSTRAINT fk_time_report_asignacion_proyecto_lider FOREIGN KEY (idlider) REFERENCES administracion.tbl_administracion_lider(id);


ALTER TABLE ONLY time_report.tbl_time_report_asignacion_proyecto
    ADD CONSTRAINT fk_time_report_empleado_proyecto_empleado FOREIGN KEY (idempleado) REFERENCES administracion.tbl_administracion_empleado(id);


ALTER TABLE ONLY time_report.tbl_time_report_asignacion_proyecto
    ADD CONSTRAINT fk_time_report_empleado_proyecto_proyecto FOREIGN KEY (idproyecto) REFERENCES time_report.tbl_time_report_proyecto(id);


ALTER TABLE ONLY time_report.tbl_time_report_homologacion_banco
    ADD CONSTRAINT fk_time_report_homologacion_banco_empleado FOREIGN KEY (idempleado) REFERENCES administracion.tbl_administracion_empleado(id);


ALTER TABLE ONLY time_report.tbl_time_report_permiso
    ADD CONSTRAINT fk_time_report_permiso_aprobador FOREIGN KEY (aprobadopor) REFERENCES administracion.tbl_administracion_empleado(id);


ALTER TABLE ONLY time_report.tbl_time_report_permiso
    ADD CONSTRAINT fk_time_report_permiso_empleado FOREIGN KEY (idempleado) REFERENCES administracion.tbl_administracion_empleado(id);


ALTER TABLE ONLY time_report.tbl_time_report_permiso
    ADD CONSTRAINT fk_time_report_permiso_estado_aprobacion FOREIGN KEY (idestadoaprobacion) REFERENCES administracion.tbl_administracion_catalogo_detalle(id);


ALTER TABLE ONLY time_report.tbl_time_report_permiso
    ADD CONSTRAINT fk_time_report_permiso_tipo FOREIGN KEY (idtipopermiso) REFERENCES administracion.tbl_administracion_catalogo_detalle(id);


ALTER TABLE ONLY time_report.tbl_time_report_proyeccion_horas_proyecto
    ADD CONSTRAINT fk_time_report_proyeccion_horas_proyecto_proyecto FOREIGN KEY (idproyecto) REFERENCES time_report.tbl_time_report_proyecto(id);


ALTER TABLE ONLY time_report.tbl_time_report_proyeccion_horas_proyecto
    ADD CONSTRAINT fk_time_report_proyeccion_horas_proyecto_tipo_recurso FOREIGN KEY (idtiporecurso) REFERENCES administracion.tbl_administracion_cargo(id);


ALTER TABLE ONLY time_report.tbl_time_report_proyeccion_horas
    ADD CONSTRAINT fk_time_report_proyeccion_horas_tipo_recurso FOREIGN KEY (idtiporecurso) REFERENCES administracion.tbl_administracion_cargo(id);


ALTER TABLE ONLY time_report.tbl_time_report_proyecto
    ADD CONSTRAINT fk_time_report_proyecto_cliente FOREIGN KEY (idcliente) REFERENCES administracion.tbl_administracion_cliente(id);


ALTER TABLE ONLY time_report.tbl_time_report_proyecto
    ADD CONSTRAINT fk_time_report_proyecto_estado FOREIGN KEY (idestadoproyecto) REFERENCES administracion.tbl_administracion_catalogo_detalle(id);


ALTER TABLE ONLY time_report.tbl_time_report_proyecto
    ADD CONSTRAINT fk_time_report_proyecto_tipo FOREIGN KEY (idtipoproyecto) REFERENCES time_report.tbl_time_report_tipo_proyecto(id);


ALTER TABLE ONLY time_report.tbl_time_report_solicitud_adjunto
    ADD CONSTRAINT fk_tr_solicitud_adjunto_solicitud FOREIGN KEY (id_solicitud) REFERENCES time_report.tbl_time_report_solicitud(id) ON DELETE CASCADE;


ALTER TABLE ONLY time_report.tbl_time_report_solicitud_adjunto
    ADD CONSTRAINT fk_tr_solicitud_adjunto_usuario FOREIGN KEY (subido_por_usuario_id) REFERENCES autenticacion.tbl_autenticacion_usuario(id);


ALTER TABLE ONLY time_report.tbl_time_report_solicitud
    ADD CONSTRAINT fk_tr_solicitud_asignacion_proyecto FOREIGN KEY (id_asignacion_proyecto) REFERENCES time_report.tbl_time_report_asignacion_proyecto(id) ON DELETE SET NULL;


ALTER TABLE ONLY time_report.tbl_time_report_solicitud
    ADD CONSTRAINT fk_tr_solicitud_cliente FOREIGN KEY (id_cliente) REFERENCES administracion.tbl_administracion_cliente(id) ON DELETE SET NULL;


ALTER TABLE ONLY time_report.tbl_time_report_solicitud
    ADD CONSTRAINT fk_tr_solicitud_empleado FOREIGN KEY (id_empleado) REFERENCES administracion.tbl_administracion_empleado(id);


ALTER TABLE ONLY time_report.tbl_time_report_solicitud
    ADD CONSTRAINT fk_tr_solicitud_estado FOREIGN KEY (id_estado) REFERENCES time_report.tbl_time_report_estado_solicitud(id);


ALTER TABLE ONLY time_report.tbl_time_report_solicitud
    ADD CONSTRAINT fk_tr_solicitud_lider FOREIGN KEY (id_lider) REFERENCES administracion.tbl_administracion_lider(id) ON DELETE SET NULL;


ALTER TABLE ONLY time_report.tbl_time_report_solicitud
    ADD CONSTRAINT fk_tr_solicitud_proyecto FOREIGN KEY (id_proyecto) REFERENCES time_report.tbl_time_report_proyecto(id) ON DELETE SET NULL;


ALTER TABLE ONLY time_report.tbl_time_report_solicitud_revision
    ADD CONSTRAINT fk_tr_solicitud_revision_solicitud FOREIGN KEY (id_solicitud) REFERENCES time_report.tbl_time_report_solicitud(id) ON DELETE CASCADE;


ALTER TABLE ONLY time_report.tbl_time_report_solicitud_revision
    ADD CONSTRAINT fk_tr_solicitud_revision_usuario FOREIGN KEY (id_usuario_revisor) REFERENCES autenticacion.tbl_autenticacion_usuario(id);


ALTER TABLE ONLY time_report.tbl_time_report_solicitud
    ADD CONSTRAINT fk_tr_solicitud_tipo_permiso FOREIGN KEY (id_tipo_permiso) REFERENCES time_report.tbl_time_report_tipo_permiso(id);
